using System;
using System.Threading;
using CalmSpace.Analytics;
using CalmSpace.Audio;
using CalmSpace.Demo;
using CalmSpace.Haptics;
using CalmSpace.Workshop;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace CalmSpace.UI
{
    public enum WorkshopHomeEntryReason
    {
        ColdStart = 0,
        ReturnFromLevel = 1,
        CatalogBack = 2,
        ExplicitWorkshopView = 3
    }

    public interface IWorkshopHomeController
    {
        event Action<LevelLaunchRequest> LevelLaunchRequested;
        event Action CatalogRequested;
        UniTask InitializeAsync(CancellationToken cancellationToken);
        UniTask PrepareEntryAsync(
            WorkshopHomeEntryReason reason,
            CancellationToken cancellationToken);
        UniTask NotifyVisibleAsync(
            WorkshopHomeEntryReason reason,
            CancellationToken cancellationToken);
        void NotifyHidden();
    }

    /// <summary>
    /// Narrow companion boundary for navigation recovery. The primary home
    /// contract remains stable and does not own level loading.
    /// </summary>
    public interface IWorkshopHomeRecovery
    {
        void ShowLoadFailure(
            LevelLaunchRequest request,
            Action<LevelLaunchRequest> retry);
    }

    [DisallowMultipleComponent]
    public sealed class WorkshopHomeController :
        MonoBehaviour,
        IWorkshopHomeController,
        IWorkshopHomeRecovery
    {
        [SerializeField] private WorkshopHomeView _view;
        [SerializeField] private RectTransform _roomParent;

        private IWorkshopRoomLoader _roomLoader;
        private WorkshopRoomPresenter _room;
        private CancellationTokenSource _roomCancellation;
        private CancellationTokenSource _revealCancellation;
        private bool _roomUnavailable;
        private bool _roomLoading;
        private bool _revealPlaying;
        private string _revealFallbackBeatId = string.Empty;
        private string _memoryCardId = string.Empty;
        private IDemoProgressStore _store;
        private IWorkshopFlowCoordinator _flow;
        private IWorkshopProgressProjector _projector;
        private IWorkshopTextService _text;
        private IProductAnalytics _analytics;
        private IHapticService _haptics;
        private IDemoLocalizationService _localization;
        private IBackgroundMusicService _music;
        private IAsmrAudioService _audioService;
        private WorkshopRuntimeAvailability _availability;
        private bool _initialized;
        private bool _eventsBound;
        private bool _visible;
        private bool _hasLoadFailure;
        private LevelLaunchRequest _failedRequest;
        private Action<LevelLaunchRequest> _retryLoad;

        public event Action<LevelLaunchRequest> LevelLaunchRequested;
        public event Action CatalogRequested;

        public WorkshopHomeView View => _view;
        public bool IsInitialized => _initialized;

        [Inject]
        public void Construct(
            IDemoProgressStore store,
            IWorkshopFlowCoordinator flow,
            IWorkshopProgressProjector projector,
            IWorkshopTextService text,
            IProductAnalytics analytics,
            IHapticService haptics,
            IDemoLocalizationService localization,
            IBackgroundMusicService music,
            IAsmrAudioService audioService,
            WorkshopRuntimeAvailability availability,
            IWorkshopRoomLoader roomLoader)
        {
            _roomLoader = roomLoader ??
                throw new ArgumentNullException(nameof(roomLoader));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _projector = projector ??
                throw new ArgumentNullException(nameof(projector));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _analytics = analytics ??
                throw new ArgumentNullException(nameof(analytics));
            _haptics = haptics ??
                throw new ArgumentNullException(nameof(haptics));
            _localization = localization ??
                throw new ArgumentNullException(nameof(localization));
            _music = music ?? throw new ArgumentNullException(nameof(music));
            _audioService = audioService ??
                throw new ArgumentNullException(nameof(audioService));
            _availability = availability ??
                throw new ArgumentNullException(nameof(availability));
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_initialized)
            {
                return UniTask.CompletedTask;
            }

            if (_view == null || _store == null || _flow == null ||
                _projector == null || _text == null || _analytics == null ||
                _haptics == null || _localization == null || _music == null ||
                _audioService == null || _availability == null ||
                _roomLoader == null)
            {
                throw new InvalidOperationException(
                    "WorkshopHomeController is not fully configured.");
            }

            BindEvents();
            _initialized = true;
            BeginRoomLoad();
            Refresh();
            return UniTask.CompletedTask;
        }

        public UniTask PrepareEntryAsync(
            WorkshopHomeEntryReason reason,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_initialized)
            {
                return UniTask.CompletedTask;
            }

            BeginRoomLoad();
            DrainUnpresentableQueue();
            _room?.SetVisible(false);
            Refresh();
            _view.CloseBottomSheet();
            return UniTask.CompletedTask;
        }

        public UniTask NotifyVisibleAsync(
            WorkshopHomeEntryReason reason,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_initialized)
            {
                return UniTask.CompletedTask;
            }

            _room?.SetVisible(true);
            if (!_visible)
            {
                _visible = true;
                _analytics.Track(ProductAnalyticsEvent.WorkshopViewed(
                    WorkshopContentIds.CozyWorkshopChapterId));
            }

            BeginPendingReveal();
            return UniTask.CompletedTask;
        }

        public void NotifyHidden()
        {
            _visible = false;
            _hasLoadFailure = false;
            _retryLoad = null;
            CancelReveal();
            _revealFallbackBeatId = string.Empty;
            _memoryCardId = string.Empty;
            _room?.SetVisible(false);
            _view?.CloseBottomSheet();
        }

        /// <summary>
        /// Plays the reveal for the oldest unseen room presentation. The queue
        /// is read from the latest persisted profile on every entry, never
        /// from a side list, so an interrupted reveal is simply still pending
        /// the next time the player comes home.
        /// </summary>
        private void BeginPendingReveal()
        {
            if (!_initialized ||
                !_visible ||
                _revealPlaying ||
                _hasLoadFailure ||
                !string.IsNullOrEmpty(_revealFallbackBeatId) ||
                !string.IsNullOrEmpty(_memoryCardId))
            {
                return;
            }

            DrainUnpresentableQueue();
            if (!TryGetPendingRevealBeatId(out string beatId))
            {
                // A restored zone may have uncovered a family memory.
                TryShowPendingMemory();
                return;
            }

            if (_room == null)
            {
                if (!_roomUnavailable)
                {
                    // The room is still arriving. Wait for it rather than
                    // degrading a recoverable reveal into the skip card;
                    // LoadRoomAsync retries this once the room is adopted.
                    return;
                }

                ShowRevealFallback(beatId);
                return;
            }

            _revealPlaying = true;
            Refresh();
            CancelReveal();
            _revealCancellation = new CancellationTokenSource();
            PlayPendingRevealAsync(beatId, _revealCancellation.Token).Forget();
        }

        private async UniTaskVoid PlayPendingRevealAsync(
            string beatId,
            CancellationToken cancellationToken)
        {
            var result = WorkshopRevealPlaybackResult.Cancelled;
            try
            {
                UniTask<WorkshopRevealPlaybackResult> playback =
                    _room.PlayRevealAsync(beatId, cancellationToken);
                if (_room.IsRevealPlaying)
                {
                    PlayAudioCue(AsmrAudioCue.RoomReveal);
                }

                result = await playback;
            }
            catch (OperationCanceledException)
            {
                result = WorkshopRevealPlaybackResult.Cancelled;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                _revealPlaying = false;
            }

            if (this == null || !_initialized)
            {
                return;
            }

            switch (result)
            {
                case WorkshopRevealPlaybackResult.Completed:
                    MarkRevealSeen(beatId);
                    break;
                case WorkshopRevealPlaybackResult.MissingVisual:
                    Refresh();
                    ShowRevealFallback(beatId);
                    break;
                default:
                    // Interrupted playback persists nothing, so the same
                    // presentation is replayed on the next entry.
                    Refresh();
                    break;
            }
        }

        /// <summary>
        /// Retires the presentation exactly once. Replay of an already
        /// completed level enqueues nothing, so no reward or story repeats.
        /// </summary>
        private void MarkRevealSeen(string beatId)
        {
            if (_store == null || !_store.IsInitialized)
            {
                return;
            }

            _store.MarkPresentationSeen(
                PendingPresentationEntry.RoomReveal(beatId));
            // Retiring a reveal can expose a memory or finale entry that this
            // build cannot present; leaving it at the head would block the
            // next level.
            DrainUnpresentableQueue();
            Refresh();

            if (TryGetPendingRevealBeatId(out string next) &&
                string.Equals(next, beatId, StringComparison.Ordinal))
            {
                // The same reveal is still at the head, so retiring it did
                // not take. Stop rather than replay it forever.
                return;
            }

            // The queue may now offer another reveal or a family memory.
            BeginPendingReveal();
        }

        /// <summary>
        /// A presentation can only be retired from the head of the persisted
        /// queue, so the reveal we offer to play must be the head itself.
        /// </summary>
        private bool TryGetPendingRevealBeatId(out string beatId)
        {
            beatId = string.Empty;
            if (_store == null ||
                !_store.IsInitialized ||
                _availability == null ||
                !_availability.HomeMetaAvailable)
            {
                return false;
            }

            DemoProgressSnapshot snapshot = _store.Current;
            if (!snapshot.TryGetPendingPresentation(
                    0, out PendingPresentationEntry head) ||
                head.Kind != PendingPresentationKind.RoomReveal ||
                !_projector.TryProject(
                    snapshot, out WorkshopProgressProjection projection) ||
                !string.Equals(
                    projection.PendingRevealBeatId,
                    head.StableId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            beatId = head.StableId;
            return true;
        }

        /// <summary>
        /// Retires queued presentations this build cannot show. Album cameos
        /// and the finale sequence are deferred, but a presentation left at
        /// the head of the queue blocks every later recommendation, including
        /// the next level, and would strand the player on a home screen with
        /// no way forward.
        /// </summary>
        private void DrainUnpresentableQueue()
        {
            if (_store == null || !_store.IsInitialized)
            {
                return;
            }

            // Bounded by the queue length observed on entry: every iteration
            // must retire one entry or stop, so this cannot spin.
            int guardLimit = _store.Current.PendingPresentationCount;
            for (var guard = 0; guard < guardLimit; guard++)
            {
                if (!_store.Current.TryGetPendingPresentation(
                        0, out PendingPresentationEntry head))
                {
                    return;
                }

                ProfileMutationStatus status;
                switch (head.Kind)
                {
                    case PendingPresentationKind.Memory:
                        if (CanPresentMemory(head.StableId))
                        {
                            // Presentable: the caller shows the card and the
                            // player retires it by closing.
                            return;
                        }

                        status = _store.MarkMemoryViewed(head.StableId).Status;
                        break;
                    case PendingPresentationKind.Finale:
                        // The finale reads as permanent sunlight applied from
                        // the projection, not as a queued sequence.
                        status = _store.MarkPresentationSeen(head).Status;
                        break;
                    default:
                        return;
                }

                if (status != ProfileMutationStatus.Applied)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Shows the family memory a restored zone uncovered. The memory is
        /// only retired when the player closes the card, so leaving the
        /// workshop first simply shows it again next time.
        /// </summary>
        private bool TryShowPendingMemory()
        {
            if (!string.IsNullOrEmpty(_memoryCardId))
            {
                return true;
            }

            if (_store == null ||
                !_store.IsInitialized ||
                !_store.Current.TryGetPendingPresentation(
                    0, out PendingPresentationEntry head) ||
                head.Kind != PendingPresentationKind.Memory ||
                !CanPresentMemory(head.StableId) ||
                !WorkshopContentIds.TryGetMemoryTextKey(
                    head.StableId, out string textKey))
            {
                return false;
            }

            _memoryCardId = head.StableId;
            Refresh();
            _view.ShowRetry(
                _text.Get(textKey),
                GetTextOrFallback(
                    "common.close", "Close", "Закрити", "Закрыть"),
                CloseMemoryCard);
            return true;
        }

        private void CloseMemoryCard()
        {
            string memoryId = _memoryCardId;
            _memoryCardId = string.Empty;
            _view.CloseBottomSheet();
            if (!string.IsNullOrEmpty(memoryId))
            {
                PlayAudioCue(AsmrAudioCue.UiTap);
            }

            if (!string.IsNullOrEmpty(memoryId) &&
                _store != null &&
                _store.IsInitialized)
            {
                _store.MarkMemoryViewed(memoryId);
            }

            Refresh();
            BeginPendingReveal();
        }

        private bool CanPresentMemory(string memoryId)
        {
            if (_availability == null ||
                !_availability.TextAvailable ||
                _text == null ||
                !WorkshopContentIds.TryGetMemoryTextKey(
                    memoryId, out string textKey))
            {
                return false;
            }

            string copy = _text.Get(textKey);
            return !string.IsNullOrWhiteSpace(copy) &&
                !string.Equals(copy, textKey, StringComparison.Ordinal);
        }

        private void ShowRevealFallback(string beatId)
        {
            _revealFallbackBeatId = beatId;
            _view.ShowRetry(
                RevealFallbackTitle(),
                RevealFallbackAction(),
                SkipRevealFallback);
        }

        private void SkipRevealFallback()
        {
            string beatId = _revealFallbackBeatId;
            _revealFallbackBeatId = string.Empty;
            _view.CloseBottomSheet();
            if (!string.IsNullOrEmpty(beatId))
            {
                PlayAudioCue(AsmrAudioCue.UiTap);
                MarkRevealSeen(beatId);
            }
        }

        private string RevealFallbackTitle()
        {
            return GetTextOrFallback(
                "reveal.fallback.title",
                "The workshop changed while you were away.",
                "Майстерня змінилася, поки вас не було.",
                "Мастерская изменилась, пока вас не было.");
        }

        private string RevealFallbackAction()
        {
            return GetTextOrFallback(
                "common.skip", "Skip", "Пропустити", "Пропустить");
        }

        private void CancelReveal()
        {
            if (_revealCancellation == null)
            {
                return;
            }

            _revealCancellation.Cancel();
            _revealCancellation.Dispose();
            _revealCancellation = null;
        }

        /// <summary>
        /// Starts loading the illustrated room without gating the first
        /// screen. The home is fully usable before the room arrives, and a
        /// missing or broken room visual is never fatal: the Task 8 localized
        /// card keeps every level reachable.
        /// </summary>
        private void BeginRoomLoad()
        {
            if (_room != null ||
                _roomLoading ||
                _roomUnavailable ||
                _roomLoader == null ||
                _roomParent == null ||
                !_availability.HomeMetaAvailable)
            {
                return;
            }

            _roomLoading = true;
            _roomCancellation ??= new CancellationTokenSource();
            LoadRoomAsync(_roomCancellation.Token).Forget();
        }

        private async UniTaskVoid LoadRoomAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                WorkshopRoomPresenter room = await _roomLoader.LoadAsync(
                    WorkshopContentIds.CozyWorkshopChapterId,
                    _roomParent,
                    cancellationToken);
                if (cancellationToken.IsCancellationRequested || this == null)
                {
                    return;
                }

                if (room == null)
                {
                    _roomUnavailable = true;
                    return;
                }

                _room = room;
                _room.HotspotPressed += HandleRoomHotspotPressed;
                _room.SetVisible(_visible);
                Refresh();
                BeginPendingReveal();
            }
            catch (OperationCanceledException)
            {
                // The home was torn down while the room was loading.
            }
            catch (Exception exception)
            {
                _roomUnavailable = true;
                Debug.LogWarning(
                    "The illustrated workshop room is unavailable; the " +
                    "localized task card remains in use. " + exception.Message,
                    this);
            }
            finally
            {
                _roomLoading = false;
            }
        }

        private void HandleRoomHotspotPressed(string beatId)
        {
            HandlePrimaryRequested();
        }

        public void ShowLoadFailure(
            LevelLaunchRequest request,
            Action<LevelLaunchRequest> retry)
        {
            if (!_initialized)
            {
                return;
            }

            _failedRequest = request;
            _retryLoad = retry;
            _hasLoadFailure = true;
            _view.ShowRetry(
                GetTextOrFallback(
                    "load.failure.title",
                    "This space needs one calm moment.",
                    "Цьому простору потрібна спокійна мить.",
                    "Этому пространству нужна спокойная минута."),
                GetTextOrFallback(
                    "load.failure.retry",
                    "Try again",
                    "Спробувати ще раз",
                    "Попробовать снова"),
                RetryFailedLoad);
        }

        private void BindEvents()
        {
            if (_eventsBound)
            {
                return;
            }

            _view.Bind(
                HandlePrimaryRequested,
                HandleCatalogRequested,
                HandleSettingsRequested,
                HandlePrimaryRequested);
            _view.BindSettings(
                HandleSettingsMusicRequested,
                HandleSettingsLocaleRequested,
                HandleSettingsHapticRequested);
            _store.ProgressChanged += HandleProgressChanged;
            _localization.LocaleChanged += HandleLocaleChanged;
            _music.EnabledChanged += HandleMusicChanged;
            _eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound)
            {
                return;
            }

            _view?.Unbind();
            if (_store != null)
            {
                _store.ProgressChanged -= HandleProgressChanged;
            }

            if (_localization != null)
            {
                _localization.LocaleChanged -= HandleLocaleChanged;
            }

            if (_music != null)
            {
                _music.EnabledChanged -= HandleMusicChanged;
            }

            _eventsBound = false;
        }

        private void HandlePrimaryRequested()
        {
            WorkshopRecommendedAction? action =
                _flow.GetRecommendedAction();
            if (!action.HasValue ||
                !_flow.TryCreateLaunchRequest(
                    action.Value,
                    out LevelLaunchRequest request))
            {
                return;
            }

            _analytics.Track(
                ProductAnalyticsEvent.RestorationTaskSelected(request));
            PlayAudioCue(AsmrAudioCue.UiTap);
            LevelLaunchRequested?.Invoke(request);
        }

        private void PlayAudioCue(AsmrAudioCue cue)
        {
            try
            {
                _audioService.PlayCue(cue);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void HandleCatalogRequested()
        {
            CatalogRequested?.Invoke();
        }

        private void HandleSettingsRequested()
        {
            _hasLoadFailure = false;
            _retryLoad = null;
            _revealFallbackBeatId = string.Empty;
            _memoryCardId = string.Empty;
            _view.OpenSettings();
        }

        private void HandleSettingsMusicRequested()
        {
            _music.Toggle();
        }

        private void HandleSettingsLocaleRequested()
        {
            _localization.ToggleLocale();
        }

        private void HandleSettingsHapticRequested()
        {
            _haptics.PlaySnap();
        }

        private void HandleLocaleChanged(DemoLocale locale)
        {
            Refresh();
            if (!string.IsNullOrEmpty(_memoryCardId) &&
                _view.BottomSheet != null &&
                _view.BottomSheet.IsOpen &&
                WorkshopContentIds.TryGetMemoryTextKey(
                    _memoryCardId, out string memoryTextKey))
            {
                _view.RenderRecoveryCopy(
                    _text.Get(memoryTextKey),
                    GetTextOrFallback(
                        "common.close", "Close", "Закрити", "Закрыть"));
                return;
            }

            if (!string.IsNullOrEmpty(_revealFallbackBeatId) &&
                _view.BottomSheet != null &&
                _view.BottomSheet.IsOpen)
            {
                _view.RenderRecoveryCopy(
                    RevealFallbackTitle(),
                    RevealFallbackAction());
                return;
            }

            if (_hasLoadFailure &&
                _view.BottomSheet != null &&
                _view.BottomSheet.IsOpen)
            {
                _view.RenderRecoveryCopy(
                    GetTextOrFallback(
                        "load.failure.title",
                        "This space needs one calm moment.",
                        "Цьому простору потрібна спокійна мить.",
                        "Этому пространству нужна спокойная минута."),
                    GetTextOrFallback(
                        "load.failure.retry",
                        "Try again",
                        "Спробувати ще раз",
                        "Попробовать снова"));
            }
        }

        private void HandleMusicChanged(bool enabled)
        {
            RefreshSettings();
        }

        private void HandleProgressChanged(DemoProgressSnapshot snapshot)
        {
            Refresh();
        }

        private void Refresh()
        {
            _view.RenderChrome(GetTextOrFallback(
                "home.catalog",
                "All spaces",
                "Усі простори",
                "Все пространства"));
            RefreshSettings();

            if (!_store.IsInitialized ||
                !_availability.HomeMetaAvailable ||
                !_projector.TryProject(
                    _store.Current,
                    out WorkshopProgressProjection projection))
            {
                _room?.ApplyState(default);
                _view.Render(new WorkshopHomeViewState(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    false));
                return;
            }

            WorkshopRecommendedAction? action =
                _flow.GetRecommendedAction();
            bool hasLevel =
                action.HasValue &&
                action.Value.Kind ==
                    WorkshopRecommendedActionKind.StartLevel &&
                _flow.TryCreateLaunchRequest(action.Value, out _);
            string taskTitle = projection.IsComplete
                ? _text.Get("home.chapter-complete")
                : ResolveTaskTitle(action);
            string progress = projection.CompletedBeatCount + " / " +
                projection.BeatCount;
            _room?.ApplyState(
                WorkshopRoomVisualState.FromProjection(projection));
            _view.Render(new WorkshopHomeViewState(
                taskTitle,
                _text.Get("home.start"),
                progress,
                hasLevel && !_revealPlaying &&
                    string.IsNullOrEmpty(_memoryCardId),
                // The illustrated room owns the real hotspot when it is
                // present, so exactly one hotspot is ever actionable.
                hasLevel && !_revealPlaying &&
                    string.IsNullOrEmpty(_memoryCardId) &&
                    _room == null));
        }

        private string ResolveTaskTitle(WorkshopRecommendedAction? action)
        {
            if (!action.HasValue ||
                action.Value.Kind != WorkshopRecommendedActionKind.StartLevel ||
                !_projector.TryResolveStartLevel(
                    action.Value.StableId,
                    out _,
                    out _,
                    out WorkshopBeatDefinition beat))
            {
                return string.Empty;
            }

            return _text.Get(beat.TitleTextKey);
        }

        private void RefreshSettings()
        {
            if (_view == null || _localization == null || _music == null)
            {
                return;
            }

            string hapticLabel;
            switch (_localization.CurrentLocale)
            {
                case DemoLocale.Ukrainian:
                    hapticLabel = "Перевірити вібровідгук";
                    break;
                case DemoLocale.Russian:
                    hapticLabel = "Проверить виброотклик";
                    break;
                default:
                    hapticLabel = "Test haptic feedback";
                    break;
            }

            _view.RenderSettings(
                _localization.Get(
                    _music.IsEnabled
                        ? DemoTextKey.SoundOn
                        : DemoTextKey.SoundOff),
                GetTextOrFallback(
                    "home.settings",
                    "Settings",
                    "Налаштування",
                    "Настройки") + " · " +
                    _localization.CurrentLocaleShortLabel,
                hapticLabel);
        }

        private void RetryFailedLoad()
        {
            Action<LevelLaunchRequest> retry = _retryLoad;
            LevelLaunchRequest request = _failedRequest;
            bool accepted = _hasLoadFailure && retry != null;
            _hasLoadFailure = false;
            _retryLoad = null;
            if (!accepted)
            {
                return;
            }

            PlayAudioCue(AsmrAudioCue.UiTap);
            retry.Invoke(request);
        }

        private string GetTextOrFallback(
            string key,
            string english,
            string ukrainian,
            string russian)
        {
            if (_availability != null && _availability.TextAvailable)
            {
                return _text.Get(key);
            }

            switch (_localization.CurrentLocale)
            {
                case DemoLocale.Ukrainian:
                    return ukrainian;
                case DemoLocale.Russian:
                    return russian;
                default:
                    return english;
            }
        }

        private void OnDestroy()
        {
            CancelReveal();
            if (_roomCancellation != null)
            {
                _roomCancellation.Cancel();
                _roomCancellation.Dispose();
                _roomCancellation = null;
            }

            NotifyHidden();
            if (_room != null)
            {
                _room.HotspotPressed -= HandleRoomHotspotPressed;
                _room = null;
            }

            UnbindEvents();
            _initialized = false;
        }
    }
}
