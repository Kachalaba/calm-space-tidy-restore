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

        private IDemoProgressStore _store;
        private IWorkshopFlowCoordinator _flow;
        private IWorkshopProgressProjector _projector;
        private IWorkshopTextService _text;
        private IProductAnalytics _analytics;
        private IHapticService _haptics;
        private IDemoLocalizationService _localization;
        private IBackgroundMusicService _music;
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
            WorkshopRuntimeAvailability availability)
        {
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
                _availability == null)
            {
                throw new InvalidOperationException(
                    "WorkshopHomeController is not fully configured.");
            }

            BindEvents();
            _initialized = true;
            Refresh();
            return UniTask.CompletedTask;
        }

        public UniTask PrepareEntryAsync(
            WorkshopHomeEntryReason reason,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_initialized)
            {
                Refresh();
                _view.CloseBottomSheet();
            }

            return UniTask.CompletedTask;
        }

        public UniTask NotifyVisibleAsync(
            WorkshopHomeEntryReason reason,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_initialized && !_visible)
            {
                _visible = true;
                _analytics.Track(ProductAnalyticsEvent.WorkshopViewed(
                    WorkshopContentIds.CozyWorkshopChapterId));
            }

            return UniTask.CompletedTask;
        }

        public void NotifyHidden()
        {
            _visible = false;
            _hasLoadFailure = false;
            _retryLoad = null;
            _view?.CloseBottomSheet();
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
            LevelLaunchRequested?.Invoke(request);
        }

        private void HandleCatalogRequested()
        {
            CatalogRequested?.Invoke();
        }

        private void HandleSettingsRequested()
        {
            _hasLoadFailure = false;
            _retryLoad = null;
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
            _view.Render(new WorkshopHomeViewState(
                taskTitle,
                _text.Get("home.start"),
                progress,
                hasLevel,
                hasLevel));
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
            _hasLoadFailure = false;
            _retryLoad = null;
            retry?.Invoke(request);
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
            NotifyHidden();
            UnbindEvents();
            _initialized = false;
        }
    }
}
