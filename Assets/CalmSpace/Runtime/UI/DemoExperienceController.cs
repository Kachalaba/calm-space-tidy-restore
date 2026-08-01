using System;
using System.Threading;
using CalmSpace.Analytics;
using CalmSpace.Audio;
using CalmSpace.Cleaning;
using CalmSpace.Core;
using CalmSpace.Demo;
using CalmSpace.Levels;
using CalmSpace.Workshop;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;

namespace CalmSpace.UI
{
    public interface IDemoExperienceController
    {
        UniTask InitializeAsync(
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Owns the single-scene demo shell: home, level selection, gameplay HUD,
    /// completion presentation, local progression, themes, and music controls.
    /// All Addressable transitions are serialized behind a main-thread gate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoExperienceController :
        MonoBehaviour,
        IDemoExperienceController
    {
        private const float ScreenFadeDurationSeconds = 0.2f;
        private const int MinimumDemoLevelCount = 1;
        private const int ExpectedThemeCount = 3;

        [Header("Screens")]
        [SerializeField]
        private CanvasGroup _homeScreen;

        [SerializeField]
        private CanvasGroup _levelSelectScreen;

        [SerializeField]
        private CanvasGroup _hudScreen;

        [SerializeField]
        private CanvasGroup _completionScreen;

        [SerializeField]
        private CanvasGroup _loadingOverlay;

        [SerializeField]
        private LevelTransitionCurtain _levelTransitionCurtain;

        [Header("Primary navigation")]
        [SerializeField]
        private Button _playButton;

        [SerializeField]
        private Button _levelsButton;

        [SerializeField]
        private Button _levelSelectBackButton;

        [SerializeField]
        private Button _hudHomeButton;

        [SerializeField]
        private Button _hudUndoButton;

        [SerializeField]
        private Button _completionHomeButton;

        [SerializeField]
        private Button _completionUndoButton;

        [SerializeField]
        private Button _nextButton;

        [SerializeField]
        private Button _musicButton;

        [SerializeField]
        private Button _languageButton;

        [SerializeField]
        private Button _retrySaveButton;

        [SerializeField]
        private Button _returnWithoutSavingButton;

        [Header("Copy")]
        [SerializeField]
        private Text _homeProgressText;

        [SerializeField]
        private Text _hudLevelNameText;

        [SerializeField]
        private Text _hudProgressText;

        [SerializeField]
        private Text _completionTitleText;

        [SerializeField]
        private Text _completionBodyText;

        [SerializeField]
        private Text _nextButtonText;

        [SerializeField]
        private Text _musicButtonText;

        [SerializeField]
        private Text _languageButtonText;

        [SerializeField]
        private Text _retrySaveButtonText;

        [SerializeField]
        private Text _returnWithoutSavingButtonText;

        [SerializeField]
        private LocalizedTextBinding[] _localizedTexts =
            Array.Empty<LocalizedTextBinding>();

        [Header("Level selection (one entry per catalog level)")]
        [SerializeField]
        private LevelButtonBinding[] _levelButtons =
            Array.Empty<LevelButtonBinding>();

        [Header("Themes (three entries)")]
        [SerializeField]
        private ThemeButtonBinding[] _themeButtons =
            new ThemeButtonBinding[ExpectedThemeCount];

        [Header("Home room")]
        [SerializeField]
        private Text _roomCurrencyText;

        [SerializeField]
        private Text _completionRewardText;

        [SerializeField]
        private DecorationButtonBinding[] _decorationButtons =
            Array.Empty<DecorationButtonBinding>();

        [SerializeField]
        private DemoRoomPresenter _roomPresenter;

        [Header("Theme targets")]
        [SerializeField]
        private LevelThemeApplicator _levelThemeApplicator;

        [SerializeField]
        private Image _backgroundWash;

        [SerializeField]
        private Image _musicStateImage;

        [SerializeField]
        private Image[] _accentImages = Array.Empty<Image>();

        [SerializeField]
        private Image[] _panelImages = Array.Empty<Image>();

        [SerializeField]
        private Text[] _primaryTexts = Array.Empty<Text>();

        [SerializeField]
        private Text[] _secondaryTexts = Array.Empty<Text>();

        private ILevelFlowController _levelFlow;
        private LevelCatalog _levelCatalog;
        private DemoDecorationCatalog _decorationCatalog;
        private IDemoProgressStore _progressStore;
        private IDemoThemeService _themeService;
        private IDemoLocalizationService _localization;
        private IBackgroundMusicService _musicService;
        private IUndoHistory _undoHistory;
        private IProductAnalytics _analytics;
        private IMonotonicClock _clock;
        private IWorkshopHomeController _workshopHomeController;
        private IWorkshopHomeRecovery _workshopHomeRecovery;
        private IWorkshopFlowCoordinator _workshopFlowCoordinator;
        private IWorkshopTextService _workshopText;
        private LivingWorkshopCatalog _livingWorkshopCatalog;
        private WorkshopAnalyticsSessionState _workshopAnalyticsSession;
        private WorkshopRuntimeAvailability _workshopAvailability;

        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        private UnityAction[] _levelButtonActions;
        private UnityAction[] _themeButtonActions;
        private UnityAction[] _decorationButtonActions;
        private CanvasGroup _activeScreen;
        private LevelBase _boundLevel;
        private CleaningLevel _boundCleaningLevel;
        private RenderTextureCleaner _boundCleaner;
        private int _currentLevelIndex = -1;
        private int _lastCompletionReward;
        private int _levelUndoCount;
        private double _levelStartedAtSeconds;
        private LevelLaunchSource _currentLaunchSource =
            LevelLaunchSource.Catalog;
        private string _pendingCompletionLevelId = string.Empty;
        private int _pendingCompletionLevelIndex = -1;
        private int _pendingCompletionReward;
        private string _pendingCompletionBeatId = string.Empty;
        private string _pendingCompletionMemoryId = string.Empty;
        private string _pendingCompletionChapterId = string.Empty;
        private bool _pendingCompletionIsFinale;
        private ProfileMutationStatus _lastCompletionStatus =
            ProfileMutationStatus.Invalid;
        private bool _invalidCompletionLogged;
        private bool _eventsBound;
        private bool _initialized;
        private bool _initializing;
        private bool _isNavigating;
        private bool _destroying;

        public bool IsInitialized => _initialized;

        public bool IsNavigating => _isNavigating;

        public int CurrentLevelIndex => _currentLevelIndex;

        public ProfileMutationStatus LastCompletionStatus =>
            _lastCompletionStatus;

        [Inject]
        public void Construct(
            ILevelFlowController levelFlow,
            LevelCatalog levelCatalog,
            DemoDecorationCatalog decorationCatalog,
            IDemoProgressStore progressStore,
            IDemoThemeService themeService,
            IDemoLocalizationService localization,
            IBackgroundMusicService musicService,
            IUndoHistory undoHistory,
            IProductAnalytics analytics,
            IMonotonicClock clock,
            IWorkshopHomeController workshopHomeController,
            IWorkshopHomeRecovery workshopHomeRecovery,
            IWorkshopFlowCoordinator workshopFlowCoordinator,
            IWorkshopTextService workshopText,
            LivingWorkshopCatalog livingWorkshopCatalog,
            WorkshopAnalyticsSessionState workshopAnalyticsSession,
            WorkshopRuntimeAvailability workshopAvailability)
        {
            _levelFlow = levelFlow ??
                throw new ArgumentNullException(nameof(levelFlow));
            _levelCatalog = levelCatalog ??
                throw new ArgumentNullException(nameof(levelCatalog));
            _decorationCatalog = decorationCatalog ??
                throw new ArgumentNullException(
                    nameof(decorationCatalog));
            _progressStore = progressStore ??
                throw new ArgumentNullException(nameof(progressStore));
            _themeService = themeService ??
                throw new ArgumentNullException(nameof(themeService));
            _localization = localization ??
                throw new ArgumentNullException(nameof(localization));
            _musicService = musicService ??
                throw new ArgumentNullException(nameof(musicService));
            _undoHistory = undoHistory ??
                throw new ArgumentNullException(nameof(undoHistory));
            _analytics = analytics ??
                throw new ArgumentNullException(nameof(analytics));
            _clock = clock ??
                throw new ArgumentNullException(nameof(clock));
            _workshopHomeController = workshopHomeController ??
                throw new ArgumentNullException(
                    nameof(workshopHomeController));
            _workshopHomeRecovery = workshopHomeRecovery ??
                throw new ArgumentNullException(
                    nameof(workshopHomeRecovery));
            _workshopFlowCoordinator = workshopFlowCoordinator ??
                throw new ArgumentNullException(
                    nameof(workshopFlowCoordinator));
            _workshopText = workshopText ??
                throw new ArgumentNullException(nameof(workshopText));
            _livingWorkshopCatalog = livingWorkshopCatalog ??
                throw new ArgumentNullException(
                    nameof(livingWorkshopCatalog));
            _workshopAnalyticsSession = workshopAnalyticsSession ??
                throw new ArgumentNullException(
                    nameof(workshopAnalyticsSession));
            _workshopAvailability = workshopAvailability ??
                throw new ArgumentNullException(
                    nameof(workshopAvailability));
        }

        private void Awake()
        {
            SetScreenImmediate(_homeScreen, true);
            SetScreenImmediate(_levelSelectScreen, false);
            SetScreenImmediate(_hudScreen, false);
            SetScreenImmediate(_completionScreen, false);
            SetScreenImmediate(_loadingOverlay, false);
            _activeScreen = _homeScreen;
            _roomPresenter?.SetVisible(false);
        }

        /// <summary>
        /// Called by the application bootstrapper after policy services are
        /// ready. Safe to call more than once; only the first call performs
        /// scene initialization.
        /// </summary>
        public async UniTask InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            if (_initialized || _initializing)
            {
                return;
            }

            _initializing = true;
            try
            {
                ValidateDependencies();
                ValidateSerializedReferences();

                _progressStore.Initialize(
                    _levelCatalog.Count,
                    _themeService.DefaultThemeId,
                    _decorationCatalog.CompletionReward);
                _themeService.Initialize();
                _localization.Initialize();
                BindEvents();
                _musicService.Initialize(
                    _progressStore.Current.MusicEnabled);

                ApplyTheme(_themeService.Current);
                RefreshLocalizedUi();
                RefreshProgressUi();
                RefreshLevelButtons();
                RefreshDecorationUi();
                RefreshMusicUi(_musicService.IsEnabled);
                SetCompletionRecoveryVisible(false);

                if (_levelFlow.CurrentLevel != null)
                {
                    await _levelFlow.UnloadCurrentLevelAsync(
                        cancellationToken);
                }

                _currentLevelIndex = -1;
                _levelThemeApplicator?.ClearCurrentLevel();
                SetScreenImmediate(_homeScreen, true);
                SetScreenImmediate(_levelSelectScreen, false);
                SetScreenImmediate(_hudScreen, false);
                SetScreenImmediate(_completionScreen, false);
                SetScreenImmediate(_loadingOverlay, false);
                _activeScreen = _homeScreen;
                _initialized = true;
                _analytics.Track(
                    ProductAnalyticsEvent.SessionStarted(
                        _localization.CurrentLocale.ToString(),
                        Application.version,
                        DemoProgressRules.CountCompleted(
                            _progressStore.Current,
                            _levelCatalog.Count),
                        _levelCatalog.Count));
                RefreshNavigationAvailability();
                RefreshRoomVisibility();
            }
            catch
            {
                UnbindEvents();
                throw;
            }
            finally
            {
                _initializing = false;
            }
        }

        public UniTask<bool> PlayRecommendedLevelAsync(
            CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                return UniTask.FromResult(false);
            }

            WorkshopRecommendedAction? action =
                _workshopFlowCoordinator.GetRecommendedAction();
            if (action.HasValue)
            {
                return _workshopFlowCoordinator.TryCreateLaunchRequest(
                        action.Value,
                        out LevelLaunchRequest request)
                    ? PlayLevelAsync(request, cancellationToken)
                    : UniTask.FromResult(false);
            }

            int recommended = DemoProgressRules.GetRecommendedLevel(
                _progressStore.Current,
                _levelCatalog.Count);
            return PlayLevelAsync(recommended, cancellationToken);
        }

        public UniTask<bool> PlayLevelAsync(
            int levelIndex,
            CancellationToken cancellationToken = default)
        {
            if (!_levelCatalog.TryGetEntry(
                    levelIndex,
                    out LevelCatalogEntry entry))
            {
                return UniTask.FromResult(false);
            }

            return PlayLevelAsync(
                new LevelLaunchRequest(
                    entry.Definition.LevelId,
                    levelIndex,
                    LevelLaunchSource.Catalog,
                    string.Empty,
                    string.Empty),
                cancellationToken);
        }

        public async UniTask<bool> PlayLevelAsync(
            LevelLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!CanNavigate() ||
                !_progressStore.IsLevelUnlocked(request.LevelIndex) ||
                !_levelCatalog.TryGetEntry(
                    request.LevelIndex,
                    out LevelCatalogEntry entry) ||
                !string.Equals(
                    entry.Definition.LevelId,
                    request.LevelId,
                    StringComparison.Ordinal) ||
                !Enum.IsDefined(
                    typeof(LevelLaunchSource),
                    request.Source))
            {
                return false;
            }

            BeginNavigation(true);
            bool restoredWorkshop = false;
            try
            {
                await _levelTransitionCurtain.FadeToOpaqueAsync(
                    cancellationToken);
                bool loaded = await LoadLevelCoreAsync(
                    entry.Definition.LevelId,
                    request.LevelIndex,
                    false,
                    request.Source,
                    cancellationToken);
                if (!loaded)
                {
                    restoredWorkshop = true;
                    await PrepareWorkshopRecoveryAsync(
                        WorkshopHomeEntryReason.ReturnFromLevel);
                    ShowLoadRetry(request);
                }
                else
                {
                    _workshopHomeController.NotifyHidden();
                }

                return loaded;
            }
            catch (OperationCanceledException)
            {
                RestorePresentationAfterInterruptedLoad();
                restoredWorkshop = _activeScreen == _homeScreen;
                if (restoredWorkshop)
                {
                    await PrepareWorkshopRecoveryAsync(
                        WorkshopHomeEntryReason.ReturnFromLevel);
                }
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                RestorePresentationAfterInterruptedLoad();
                restoredWorkshop = _activeScreen == _homeScreen;
                if (restoredWorkshop)
                {
                    await PrepareWorkshopRecoveryAsync(
                        WorkshopHomeEntryReason.ReturnFromLevel);
                    ShowLoadRetry(request);
                }
                return false;
            }
            finally
            {
                await RevealAfterLevelTransitionAsync();
                EndNavigation();
                if (restoredWorkshop && !_destroying)
                {
                    await NotifyWorkshopVisibleRecoveryAsync(
                        WorkshopHomeEntryReason.ReturnFromLevel);
                }
            }
        }

        public async UniTask<bool> LoadNextLevelAsync(
            CancellationToken cancellationToken = default)
        {
            if (!CanNavigate() || _currentLevelIndex < 0)
            {
                return false;
            }

            await ReturnToWorkshopAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                cancellationToken);
            return _currentLevelIndex < 0;
        }

        public async UniTask ReturnHomeAsync(
            CancellationToken cancellationToken = default)
        {
            await ReturnToWorkshopAsync(
                WorkshopHomeEntryReason.ExplicitWorkshopView,
                cancellationToken);
        }

        public async UniTask ReturnToWorkshopAsync(
            WorkshopHomeEntryReason reason,
            CancellationToken cancellationToken = default)
        {
            if (!CanNavigate())
            {
                return;
            }

            BeginNavigation(true);
            bool restoredWorkshop = false;
            try
            {
                await _levelTransitionCurtain.FadeToOpaqueAsync(
                    cancellationToken);
                await ReturnHomeCoreAsync(
                    cancellationToken,
                    true);
                await _workshopHomeController.PrepareEntryAsync(
                    reason,
                    cancellationToken);
                restoredWorkshop = true;
            }
            catch (OperationCanceledException)
            {
                restoredWorkshop =
                    await RecoverInterruptedReturnAsync(reason);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                restoredWorkshop =
                    await RecoverInterruptedReturnAsync(reason);
            }
            finally
            {
                await RevealAfterLevelTransitionAsync();
                EndNavigation();
                if (restoredWorkshop &&
                    _activeScreen == _homeScreen &&
                    !_destroying)
                {
                    await NotifyWorkshopVisibleRecoveryAsync(reason);
                }
            }
        }

        private async UniTask<bool> LoadLevelCoreAsync(
            string levelId,
            int requestedIndex,
            bool loadSequentially,
            LevelLaunchSource launchSource,
            CancellationToken cancellationToken)
        {
            UnbindLevel();

            bool loaded;
            try
            {
                if (loadSequentially &&
                    _levelFlow.CurrentLevel != null &&
                    requestedIndex ==
                        _levelFlow.CurrentLevelIndex + 1)
                {
                    loaded =
                        await _levelFlow.LoadNextLevelAsync(
                            cancellationToken);
                }
                else
                {
                    loaded =
                        await _levelFlow.LoadLevelAsync(
                            levelId,
                            cancellationToken);
                }
            }
            catch
            {
                RestorePresentationAfterInterruptedLoad();
                throw;
            }

            if (!loaded || _levelFlow.CurrentLevel == null)
            {
                RestorePresentationAfterInterruptedLoad();
                return false;
            }

            _currentLevelIndex = _levelFlow.CurrentLevelIndex;
            _currentLaunchSource = launchSource;
            BindLevel(_levelFlow.CurrentLevel);
            _levelUndoCount = 0;
            _levelStartedAtSeconds = _clock.NowSeconds;
            _analytics.Track(
                ProductAnalyticsEvent.LevelStarted(
                    _boundLevel.Definition,
                    _currentLevelIndex,
                    _progressStore.Current.CozyTokens,
                    launchSource,
                    HasValidRestorationChapter(
                        _boundLevel.Definition)));
            ApplyCurrentThemeToLevel();
            RefreshHud(
                _boundLevel.ProgressCurrent,
                _boundLevel.ProgressTotal);
            if (_boundCleaner != null)
            {
                RefreshCleaningProgress(
                    _boundCleaner.CleanedFraction);
            }
            SetActiveScreenBehindCurtain(_hudScreen);
            return true;
        }

        private void RestorePresentationAfterInterruptedLoad()
        {
            LevelBase current = _levelFlow.CurrentLevel;
            if (current == null)
            {
                _currentLevelIndex = -1;
                _levelUndoCount = 0;
                _levelStartedAtSeconds = 0d;
                _levelThemeApplicator?.ClearCurrentLevel();
                SetActiveScreenBehindCurtain(_homeScreen);
                return;
            }

            _currentLevelIndex = _levelFlow.CurrentLevelIndex;
            BindLevel(current);
            ApplyCurrentThemeToLevel();
            RefreshHud(
                current.ProgressCurrent,
                current.ProgressTotal);
            if (_boundCleaner != null)
            {
                RefreshCleaningProgress(
                    _boundCleaner.CleanedFraction);
            }

            SetActiveScreenBehindCurtain(_hudScreen);
        }

        private async UniTask<bool> RecoverInterruptedReturnAsync(
            WorkshopHomeEntryReason reason)
        {
            LevelBase current = _levelFlow.CurrentLevel;
            if (current != null)
            {
                _currentLevelIndex = _levelFlow.CurrentLevelIndex;
                BindLevel(current);
                ApplyCurrentThemeToLevel();
                RefreshHud(
                    current.ProgressCurrent,
                    current.ProgressTotal);
                if (_boundCleaner != null)
                {
                    RefreshCleaningProgress(
                        _boundCleaner.CleanedFraction);
                }

                SetActiveScreenBehindCurtain(_hudScreen);
                return false;
            }

            UnbindLevel();
            _currentLevelIndex = -1;
            _levelUndoCount = 0;
            _levelStartedAtSeconds = 0d;
            _levelThemeApplicator?.ClearCurrentLevel();
            RefreshProgressUi();
            RefreshLevelButtons();
            RefreshDecorationUi();
            SetActiveScreenBehindCurtain(_homeScreen);
            await PrepareWorkshopRecoveryAsync(reason);
            return true;
        }

        private async UniTask ReturnHomeCoreAsync(
            CancellationToken cancellationToken,
            bool screenBehindCurtain = false)
        {
            TrackLevelAbandonedIfActive();
            UnbindLevel();
            if (_levelFlow.CurrentLevel != null)
            {
                await _levelFlow.UnloadCurrentLevelAsync(
                    cancellationToken);
            }

            _currentLevelIndex = -1;
            _levelThemeApplicator?.ClearCurrentLevel();
            RefreshProgressUi();
            RefreshLevelButtons();
            RefreshDecorationUi();
            if (screenBehindCurtain)
            {
                SetActiveScreenBehindCurtain(_homeScreen);
            }
            else
            {
                await ShowScreenAsync(
                    _homeScreen,
                    cancellationToken);
            }
        }

        private void TrackLevelAbandonedIfActive()
        {
            if (!_initialized ||
                _boundLevel == null ||
                _boundLevel.State != LevelState.Active ||
                _currentLevelIndex < 0)
            {
                return;
            }

            _analytics.Track(
                ProductAnalyticsEvent.LevelAbandoned(
                    _boundLevel.Definition,
                    _currentLevelIndex,
                    GetCurrentLevelDurationSeconds(),
                    _levelUndoCount,
                    _boundLevel.ProgressCurrent,
                    _boundLevel.ProgressTotal,
                    HasValidRestorationChapter(
                        _boundLevel.Definition)));
        }

        private double GetCurrentLevelDurationSeconds()
        {
            if (_clock == null)
            {
                return 0d;
            }

            double duration =
                _clock.NowSeconds - _levelStartedAtSeconds;
            return
                double.IsNaN(duration) ||
                double.IsInfinity(duration) ||
                duration < 0d
                    ? 0d
                    : duration;
        }

        private async UniTask ShowLevelSelectAsync(
            CancellationToken cancellationToken)
        {
            if (!CanNavigate())
            {
                return;
            }

            BeginNavigation(false);
            try
            {
                RefreshLevelButtons();
                await ShowScreenAsync(
                    _levelSelectScreen,
                    cancellationToken);
                _workshopHomeController.NotifyHidden();
            }
            catch (OperationCanceledException)
            {
                // Expected while the scene is closing.
            }
            finally
            {
                EndNavigation();
                if (_activeScreen == _homeScreen && !_destroying)
                {
                    await NotifyWorkshopVisibleRecoveryAsync(
                        WorkshopHomeEntryReason.CatalogBack);
                }
            }
        }

        private async UniTask ShowHomeFromMenuAsync(
            CancellationToken cancellationToken)
        {
            await ReturnToWorkshopAsync(
                WorkshopHomeEntryReason.CatalogBack,
                cancellationToken);
        }

        private async UniTask ShowScreenAsync(
            CanvasGroup target,
            CancellationToken cancellationToken)
        {
            if (target == null || _activeScreen == target)
            {
                return;
            }

            var outgoing = _activeScreen;
            target.gameObject.SetActive(true);
            target.alpha = 0f;
            target.interactable = false;
            target.blocksRaycasts = false;
            if (outgoing != null)
            {
                outgoing.interactable = false;
                outgoing.blocksRaycasts = false;
            }

            try
            {
                var elapsed = 0f;
                while (elapsed < ScreenFadeDurationSeconds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _lifetimeCancellation.Token
                        .ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    var linear = Mathf.Clamp01(
                        elapsed / ScreenFadeDurationSeconds);
                    var eased =
                        linear * linear * (3f - 2f * linear);
                    target.alpha = eased;
                    if (outgoing != null)
                    {
                        outgoing.alpha = 1f - eased;
                    }

                    await UniTask.Yield(
                        PlayerLoopTiming.Update,
                        _lifetimeCancellation.Token);
                }

                if (outgoing != null)
                {
                    SetScreenImmediate(outgoing, false);
                }

                SetScreenImmediate(target, true);
                _activeScreen = target;
            }
            catch
            {
                SetScreenImmediate(target, false);
                if (outgoing != null)
                {
                    SetScreenImmediate(outgoing, true);
                }

                throw;
            }
        }

        private void BindLevel(LevelBase level)
        {
            UnbindLevel();
            _boundLevel = level;
            if (_boundLevel == null)
            {
                return;
            }

            _boundLevel.LevelCompleted += HandleLevelCompleted;
            _boundLevel.ProgressChanged += HandleLevelProgressChanged;
            _boundLevel.StateChanged += HandleLevelStateChanged;
            _boundCleaningLevel = _boundLevel as CleaningLevel;

            if (_boundCleaningLevel != null)
            {
                // A staged level moves the player from surface to surface, so
                // follow the active one instead of the first cleaner found.
                _boundCleaningLevel.ActiveCleanerChanged +=
                    HandleActiveCleanerChanged;
                BindCleaner(_boundCleaningLevel.ActiveCleaner);
                return;
            }

            BindCleaner(
                _boundLevel.GetComponentInChildren<
                    RenderTextureCleaner>(true));
        }

        private void UnbindLevel()
        {
            BindCleaner(null);

            if (_boundCleaningLevel != null)
            {
                _boundCleaningLevel.ActiveCleanerChanged -=
                    HandleActiveCleanerChanged;
                _boundCleaningLevel = null;
            }

            if (_boundLevel != null)
            {
                _boundLevel.LevelCompleted -= HandleLevelCompleted;
                _boundLevel.ProgressChanged -=
                    HandleLevelProgressChanged;
                _boundLevel.StateChanged -= HandleLevelStateChanged;
            }

            _boundLevel = null;
        }

        private void BindCleaner(RenderTextureCleaner cleaner)
        {
            if (_boundCleaner == cleaner)
            {
                return;
            }

            if (_boundCleaner != null)
            {
                _boundCleaner.CleanProgressChanged -=
                    HandleCleanProgressChanged;
            }

            _boundCleaner = cleaner;

            if (_boundCleaner == null)
            {
                return;
            }

            _boundCleaner.CleanProgressChanged +=
                HandleCleanProgressChanged;
            RefreshCleaningProgress(_boundCleaner.CleanedFraction);
        }

        private void HandleActiveCleanerChanged(
            RenderTextureCleaner cleaner)
        {
            BindCleaner(cleaner);
            if (cleaner == null)
            {
                RefreshCleaningProgress(0f);
            }
        }

        private void HandleLevelProgressChanged(
            int placed,
            int total)
        {
            RefreshHud(placed, total);
        }

        private void HandleLevelStateChanged(LevelState state)
        {
            RefreshNavigationAvailability();
            if (state == LevelState.Active &&
                _activeScreen == _completionScreen)
            {
                ReturnToHudAfterUndoAsync(
                    _lifetimeCancellation.Token).Forget();
            }
        }

        private async UniTask ReturnToHudAfterUndoAsync(
            CancellationToken cancellationToken)
        {
            if (!CanNavigate() ||
                _activeScreen != _completionScreen)
            {
                return;
            }

            BeginNavigation(false);
            try
            {
                await ShowScreenAsync(_hudScreen, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected while the scene is closing.
            }
            finally
            {
                EndNavigation();
            }
        }

        private void HandleCleanProgressChanged(float fraction)
        {
            RefreshCleaningProgress(fraction);
        }

        private void HandleLevelCompleted(LevelBase level)
        {
            if (_destroying ||
                level == null ||
                level != _boundLevel ||
                _currentLevelIndex < 0)
            {
                return;
            }

            _pendingCompletionLevelId = level.Definition.LevelId;
            _pendingCompletionLevelIndex = _currentLevelIndex;
            _pendingCompletionReward =
                _decorationCatalog.CompletionReward;
            CapturePendingWorkshopBeat();
            CompleteCapturedLevel();
        }

        private void CapturePendingWorkshopBeat()
        {
            _pendingCompletionBeatId = string.Empty;
            _pendingCompletionMemoryId = string.Empty;
            _pendingCompletionChapterId = string.Empty;
            _pendingCompletionIsFinale = false;
            if (!_workshopAvailability.HomeMetaAvailable ||
                _livingWorkshopCatalog == null ||
                !_levelCatalog.TryGetRestorationStage(
                    _pendingCompletionLevelIndex,
                    out RestorationStageInfo stage) ||
                !_livingWorkshopCatalog.TryFindBeat(
                    stage.ChapterId,
                    stage.StageIndex,
                    out WorkshopBeatDefinition beat))
            {
                return;
            }

            _pendingCompletionBeatId = beat.BeatId;
            _pendingCompletionMemoryId = beat.MemoryId;
            _pendingCompletionChapterId = beat.ChapterId;
            _pendingCompletionIsFinale = beat.IsFinale;
        }

        private void CompleteCapturedLevel()
        {
            if (_destroying ||
                _boundLevel == null ||
                string.IsNullOrEmpty(_pendingCompletionLevelId) ||
                _pendingCompletionLevelIndex < 0)
            {
                return;
            }

            ProfileMutationResult<LevelCompletionMutation> result =
                _workshopFlowCoordinator.CompleteLevel(
                    _pendingCompletionLevelId,
                    _pendingCompletionLevelIndex,
                    _pendingCompletionReward);
            _lastCompletionStatus = result.Status;
            switch (result.Status)
            {
                case ProfileMutationStatus.Invalid:
                    if (!_invalidCompletionLogged)
                    {
                        _invalidCompletionLogged = true;
                        Debug.LogError(
                            "Calm Space ignored an invalid workshop completion.",
                            this);
                    }
                    return;
                case ProfileMutationStatus.PersistFailed:
                    ShowPersistFailure();
                    return;
                case ProfileMutationStatus.Applied:
                case ProfileMutationStatus.AlreadyApplied:
                    PresentSuccessfulCompletion(result);
                    return;
            }
        }

        private void PresentSuccessfulCompletion(
            ProfileMutationResult<LevelCompletionMutation> result)
        {
            LevelCompletionMutation completion = result.Payload;
            _lastCompletionReward = Mathf.Max(
                0,
                completion.RewardAmount);
            SetCompletionRecoveryVisible(false);
            _analytics.Track(
                ProductAnalyticsEvent.LevelCompleted(
                    _boundLevel.Definition,
                    _pendingCompletionLevelIndex,
                    GetCurrentLevelDurationSeconds(),
                    _levelUndoCount,
                    _lastCompletionReward,
                    completion.TokenBalance,
                    completion.FirstCompletion,
                    HasValidRestorationChapter(
                        _boundLevel.Definition)));
            TrackWorkshopCompletionMilestones(result, completion);
            RefreshProgressUi();
            RefreshLevelButtons();
            RefreshDecorationUi();
            RefreshCompletionRewardUi();

            if (_completionTitleText != null)
            {
                DemoTextKey titleKey =
                    DemoTextKey.CompletionTitle;
                if (_levelCatalog.TryGetRestorationStage(
                        _pendingCompletionLevelIndex,
                        out var stage))
                {
                    titleKey =
                        stage.IsFinalStage
                            ? DemoTextKey.RestorationComplete
                            : DemoTextKey
                                .RestorationStageComplete;
                }

                _completionTitleText.text =
                    _localization.Get(titleKey);
            }

            if (_completionBodyText != null)
            {
                _completionBodyText.text =
                    _localization.FormatCompletionBody(
                        _boundLevel.Definition,
                        HasValidRestorationChapter(
                            _boundLevel.Definition));
            }

            if (_completionHomeButton != null)
            {
                _completionHomeButton.gameObject.SetActive(false);
            }

            _nextButton?.gameObject.SetActive(true);
            if (_nextButtonText != null)
            {
                _nextButtonText.text = _localization.Get(
                    DemoTextKey.BackHome);
            }

            PresentCompletionAsync(
                _lifetimeCancellation.Token).Forget();
        }

        private void TrackWorkshopCompletionMilestones(
            ProfileMutationResult<LevelCompletionMutation> result,
            LevelCompletionMutation completion)
        {
            if (result.Status != ProfileMutationStatus.Applied ||
                !completion.FirstCompletion)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingCompletionMemoryId) &&
                _workshopAnalyticsSession.TryAdmitMemoryUnlocked(
                    _pendingCompletionMemoryId,
                    result.Status))
            {
                _analytics.Track(ProductAnalyticsEvent.MemoryUnlocked(
                    _pendingCompletionBeatId,
                    _pendingCompletionMemoryId));
            }

            if (_pendingCompletionIsFinale &&
                _workshopAnalyticsSession.TryAdmitChapterCompleted(
                    _pendingCompletionChapterId,
                    result.Status))
            {
                _analytics.Track(ProductAnalyticsEvent.ChapterCompleted(
                    _pendingCompletionChapterId,
                    _pendingCompletionBeatId));
            }
        }

        private void ShowPersistFailure()
        {
            _lastCompletionReward = 0;
            RefreshCompletionRewardUi();
            SetCompletionRecoveryVisible(true);
            _nextButton?.gameObject.SetActive(false);
            _completionHomeButton?.gameObject.SetActive(false);
            PresentCompletionAsync(
                _lifetimeCancellation.Token).Forget();
        }

        private void SetCompletionRecoveryVisible(bool visible)
        {
            if (_retrySaveButton != null)
            {
                _retrySaveButton.gameObject.SetActive(visible);
            }

            if (_returnWithoutSavingButton != null)
            {
                _returnWithoutSavingButton.gameObject.SetActive(visible);
            }

            if (_retrySaveButtonText != null)
            {
                _retrySaveButtonText.text = _workshopText.Get("save.retry");
            }

            if (_returnWithoutSavingButtonText != null)
            {
                _returnWithoutSavingButtonText.text =
                    _workshopText.Get("save.return-without");
            }
        }

        private async UniTask PresentCompletionAsync(
            CancellationToken cancellationToken)
        {
            if (!CanNavigate())
            {
                return;
            }

            BeginNavigation(false);
            try
            {
                await ShowScreenAsync(
                    _completionScreen,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected while leaving the scene.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                EndNavigation();
            }
        }

        private void HandleThemeChanged(ThemePalette palette)
        {
            ApplyTheme(palette);
        }

        private void HandleProgressChanged(
            DemoProgressSnapshot snapshot)
        {
            RefreshProgressUi();
            RefreshLevelButtons();
            RefreshDecorationUi();
        }

        private void HandleMusicEnabledChanged(bool enabled)
        {
            if (_progressStore.IsInitialized &&
                _progressStore.Current.MusicEnabled != enabled)
            {
                _progressStore.SetMusicEnabled(enabled);
            }

            RefreshMusicUi(enabled);
            if (_initialized)
            {
                _analytics.Track(
                    ProductAnalyticsEvent.MusicChanged(enabled));
            }
        }

        private void ApplyTheme(ThemePalette palette)
        {
            if (palette == null)
            {
                return;
            }

            if (_backgroundWash != null)
            {
                var wash = palette.Background;
                wash.a = _backgroundWash.color.a;
                _backgroundWash.color = wash;
            }

            ApplyColor(_accentImages, palette.Accent);
            ApplyColor(_panelImages, palette.Panel);
            ApplyColor(_primaryTexts, palette.PrimaryText);
            ApplyColor(_secondaryTexts, palette.SecondaryText);
            _levelThemeApplicator?.ApplyScenePalette(palette);
            _levelThemeApplicator?.ApplyRootPalette(
                _roomPresenter?.RoomRoot,
                palette);
            ApplyCurrentThemeToLevel();
            RefreshThemeButtons();
            RefreshDecorationUi();
            RefreshMusicUi(
                _musicService != null &&
                _musicService.IsEnabled);
            RefreshLevelButtons();
        }

        private void ApplyCurrentThemeToLevel()
        {
            if (_boundLevel == null ||
                _themeService?.Current == null)
            {
                return;
            }

            _levelThemeApplicator?.ApplyLevelPalette(
                _boundLevel.gameObject,
                _themeService.Current);
        }

        private void RefreshProgressUi()
        {
            if (!_progressStore.IsInitialized)
            {
                return;
            }

            if (_homeProgressText != null)
            {
                var completed =
                    DemoProgressRules.CountCompleted(
                        _progressStore.Current,
                        _levelCatalog.Count);
                _homeProgressText.text =
                    _localization.FormatHomeProgress(
                        completed,
                        _levelCatalog.Count);
            }

            if (_roomCurrencyText != null)
            {
                _roomCurrencyText.text =
                    _localization.FormatRoomCurrency(
                        _progressStore.Current.CozyTokens);
            }
        }

        private void RefreshDecorationUi()
        {
            if (!_progressStore.IsInitialized ||
                _decorationCatalog == null)
            {
                return;
            }

            DemoProgressSnapshot progress = _progressStore.Current;
            _roomPresenter?.SelectDecoration(
                progress.SelectedDecorationIndex);

            if (_decorationButtons == null)
            {
                return;
            }

            ThemePalette palette = _themeService?.Current;
            bool navigationEnabled =
                _initialized && !_isNavigating;
            for (var index = 0;
                 index < _decorationButtons.Length;
                 index++)
            {
                DecorationButtonBinding binding =
                    _decorationButtons[index];
                if (binding == null)
                {
                    continue;
                }

                if (!_decorationCatalog.TryGetDefinition(
                        index,
                        out DemoDecorationDefinition decoration))
                {
                    binding.SetVisible(false);
                    continue;
                }

                bool owned =
                    index < 32 &&
                    (progress.OwnedDecorationMask &
                        (1 << index)) != 0;
                bool selected =
                    progress.SelectedDecorationIndex == index;
                bool canAfford =
                    progress.CozyTokens >= decoration.Cost;
                string state = selected
                    ? _localization.Get(
                        DemoTextKey.DecorationSelected)
                    : owned
                        ? _localization.Get(
                            DemoTextKey.DecorationOwned)
                        : _localization.Get(
                              DemoTextKey.DecorationBuy) +
                          " · " +
                          _localization.FormatDecorationCost(
                              decoration.Cost);

                binding.SetVisible(true);
                binding.SetContent(
                    _localization.GetDecorationName(decoration),
                    state,
                    selected,
                    navigationEnabled && (owned || canAfford),
                    palette);
            }
        }

        private void RefreshCompletionRewardUi()
        {
            if (_completionRewardText == null)
            {
                return;
            }

            bool visible = _lastCompletionReward > 0;
            _completionRewardText.text = visible
                ? _localization.FormatCompletionReward(
                    _lastCompletionReward)
                : string.Empty;
            if (_completionRewardText.gameObject.activeSelf != visible)
            {
                _completionRewardText.gameObject.SetActive(visible);
            }
        }

        private void RefreshRoomVisibility()
        {
            if (_roomPresenter == null)
            {
                return;
            }

            bool loadingVisible =
                _loadingOverlay != null &&
                _loadingOverlay.gameObject.activeSelf;
            bool visible =
                _initialized &&
                !_destroying &&
                !_isNavigating &&
                !loadingVisible &&
                _activeScreen == _homeScreen;
            _roomPresenter.SetVisible(visible);
        }

        private void RefreshHud(int placed, int total)
        {
            if (_hudLevelNameText != null)
            {
                RectTransform titleRect =
                    _hudLevelNameText.rectTransform;
                titleRect.sizeDelta =
                    new Vector2(370f, 104f);
                _hudLevelNameText.fontSize = 26;
                _hudLevelNameText.resizeTextForBestFit = true;
                _hudLevelNameText.resizeTextMinSize = 21;
                _hudLevelNameText.resizeTextMaxSize = 26;
                _hudLevelNameText.horizontalOverflow =
                    HorizontalWrapMode.Wrap;
                _hudLevelNameText.verticalOverflow =
                    VerticalWrapMode.Truncate;
                _hudLevelNameText.lineSpacing = 0.9f;
                _hudLevelNameText.text =
                    _localization.FormatRestorationStageTitle(
                        _boundLevel?.Definition,
                        HasValidRestorationChapter(
                            _boundLevel?.Definition));
            }

            if (_hudProgressText == null)
            {
                return;
            }

            // On a cleaning level the item counter is meaningless — stage and
            // coverage are what the player is watching.
            if (_boundCleaningLevel != null)
            {
                _hudProgressText.fontSize = 29;
                RefreshCleaningProgress(
                    _boundCleaningLevel.ActiveStageFraction);
                return;
            }

            if (_boundLevel is ScrewPuzzleLevel)
            {
                _hudProgressText.fontSize = 22;
                _hudProgressText.text =
                    _localization.Get(DemoTextKey.ScrewPrompt) +
                    "\n" +
                    placed +
                    " / " +
                    total;
                return;
            }

            _hudProgressText.fontSize = 29;
            _hudProgressText.text =
                total <= 0
                    ? _localization.Get(
                        DemoTextKey.CleaningPrompt)
                    : placed + " / " + total;
        }

        private void RefreshCleaningProgress(float fraction)
        {
            if (_hudProgressText == null)
            {
                return;
            }

            int stageCount = _boundCleaningLevel == null
                ? 0
                : _boundCleaningLevel.StageCount;
            float displayFraction = _boundCleaningLevel == null
                ? fraction
                : _boundCleaningLevel.ActiveStageFraction;
            int percentage = Mathf.RoundToInt(
                Mathf.Clamp01(displayFraction) * 100f);

            if (stageCount > 0)
            {
                _hudProgressText.text =
                    _localization.FormatStageProgress(
                        _boundCleaningLevel.ActiveTool,
                        _boundCleaningLevel.ActiveStageIndex + 1,
                        stageCount,
                        percentage);
                return;
            }

            _hudProgressText.text =
                _localization.FormatCleaningProgress(
                    percentage);
        }

        private void RefreshLevelButtons()
        {
            if (_levelButtons == null ||
                !_progressStore.IsInitialized)
            {
                return;
            }

            var palette = _themeService?.Current;
            for (var index = 0;
                 index < _levelButtons.Length;
                 index++)
            {
                var binding = _levelButtons[index];
                if (binding == null)
                {
                    continue;
                }

                if (!_levelCatalog.TryGetEntry(
                        index,
                        out var entry))
                {
                    binding.SetVisible(false);
                    continue;
                }

                var unlocked =
                    _progressStore.IsLevelUnlocked(index);
                var completed =
                    _progressStore.IsLevelCompleted(index);
                binding.SetVisible(true);
                binding.SetContent(
                    index + 1,
                    _localization.FormatRestorationStageTitle(
                        entry.Definition,
                        HasValidRestorationChapter(
                            entry.Definition)));
                binding.SetState(
                    unlocked,
                    completed,
                    !_isNavigating,
                    palette);
            }
        }

        private void RefreshThemeButtons()
        {
            if (_themeButtons == null ||
                _themeService == null ||
                !_themeService.IsInitialized)
            {
                return;
            }

            for (var index = 0;
                 index < _themeButtons.Length;
                 index++)
            {
                var binding = _themeButtons[index];
                if (binding == null ||
                    !_themeService.TryGetTheme(
                        index,
                        out var palette))
                {
                    binding?.SetVisible(false);
                    continue;
                }

                binding.SetVisible(true);
                binding.SetPalette(
                    palette,
                    _localization.GetThemeName(palette),
                    index == _themeService.CurrentIndex,
                    !_isNavigating);
            }
        }

        private void RefreshMusicUi(bool enabled)
        {
            if (_musicButtonText != null)
            {
                _musicButtonText.text =
                    _localization.Get(
                        enabled
                            ? DemoTextKey.SoundOn
                            : DemoTextKey.SoundOff);
            }

            if (_musicStateImage != null &&
                _themeService?.Current != null)
            {
                _musicStateImage.color =
                    enabled
                        ? _themeService.Current.SecondaryAccent
                        : _themeService.Current.SecondaryText;
            }
        }

        private void RefreshLocalizedUi()
        {
            if (_localization == null ||
                !_localization.IsInitialized)
            {
                return;
            }

            if (_localizedTexts != null)
            {
                for (var index = 0;
                     index < _localizedTexts.Length;
                     index++)
                {
                    _localizedTexts[index]?.Apply(_localization);
                }
            }

            if (_languageButtonText != null)
            {
                _languageButtonText.text =
                    _localization.CurrentLocaleShortLabel;
            }

            RefreshProgressUi();
            RefreshLevelButtons();
            RefreshThemeButtons();
            RefreshDecorationUi();
            RefreshCompletionRewardUi();
            RefreshMusicUi(
                _musicService != null &&
                _musicService.IsEnabled);

            if (_boundCleaner != null)
            {
                RefreshCleaningProgress(
                    _boundCleaner.CleanedFraction);
            }
            else if (_boundLevel != null)
            {
                RefreshHud(
                    _boundLevel.ProgressCurrent,
                    _boundLevel.ProgressTotal);
            }
        }

        private void RefreshNavigationAvailability()
        {
            var enabled = _initialized && !_isNavigating;
            SetButtonInteractable(_playButton, enabled);
            SetButtonInteractable(_levelsButton, enabled);
            SetButtonInteractable(
                _levelSelectBackButton,
                enabled);
            SetButtonInteractable(_hudHomeButton, enabled);
            bool undoEnabled =
                enabled &&
                _undoHistory != null &&
                _undoHistory.CanUndo;
            SetButtonInteractable(_hudUndoButton, undoEnabled);
            SetButtonInteractable(
                _completionHomeButton,
                enabled);
            SetButtonInteractable(
                _completionUndoButton,
                undoEnabled);
            SetButtonInteractable(_nextButton, enabled);
            SetButtonInteractable(_musicButton, enabled);
            SetButtonInteractable(_languageButton, enabled);
            bool recoveryEnabled = enabled &&
                _lastCompletionStatus ==
                    ProfileMutationStatus.PersistFailed;
            SetButtonInteractable(_retrySaveButton, recoveryEnabled);
            SetButtonInteractable(
                _returnWithoutSavingButton,
                recoveryEnabled);
            RefreshLevelButtons();
            RefreshThemeButtons();
            RefreshDecorationUi();
        }

        private void BeginNavigation(bool showLoading)
        {
            _isNavigating = true;
            _roomPresenter?.SetVisible(false);

            RefreshNavigationAvailability();
        }

        private void EndNavigation()
        {
            _isNavigating = false;
            if (_destroying)
            {
                return;
            }

            RefreshNavigationAvailability();
            RefreshRoomVisibility();
        }

        private async UniTask RevealAfterLevelTransitionAsync()
        {
            if (_levelTransitionCurtain == null ||
                _destroying)
            {
                return;
            }

            try
            {
                await _levelTransitionCurtain.FadeToClearAsync(
                    _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected while the scene is closing.
            }
        }

        private async UniTask PrepareWorkshopRecoveryAsync(
            WorkshopHomeEntryReason reason)
        {
            if (_destroying)
            {
                return;
            }

            try
            {
                await _workshopHomeController.PrepareEntryAsync(
                    reason,
                    _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Scene shutdown owns cancellation; screen recovery is
                // already synchronous and remains on the workshop.
            }
        }

        private async UniTask NotifyWorkshopVisibleRecoveryAsync(
            WorkshopHomeEntryReason reason)
        {
            if (_destroying)
            {
                return;
            }

            try
            {
                await _workshopHomeController.NotifyVisibleAsync(
                    reason,
                    _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected while the scene is closing.
            }
        }

        private void ShowLoadRetry(LevelLaunchRequest request)
        {
            _workshopHomeRecovery.ShowLoadFailure(
                request,
                retry => PlayLevelAsync(
                    retry,
                    _lifetimeCancellation.Token).Forget());
        }

        private void SetActiveScreenBehindCurtain(
            CanvasGroup target)
        {
            if (target == null)
            {
                return;
            }

            if (_activeScreen != null &&
                _activeScreen != target)
            {
                SetScreenImmediate(_activeScreen, false);
            }

            SetScreenImmediate(target, true);
            _activeScreen = target;
        }

        private bool CanNavigate()
        {
            return
                _initialized &&
                !_isNavigating &&
                !_destroying;
        }

        private void BindEvents()
        {
            if (_eventsBound)
            {
                return;
            }

            _playButton.onClick.AddListener(
                HandlePlayPressed);
            _levelsButton.onClick.AddListener(
                HandleLevelsPressed);
            _levelSelectBackButton.onClick.AddListener(
                HandleLevelSelectBackPressed);
            _hudHomeButton.onClick.AddListener(
                HandleHomePressed);
            _hudUndoButton.onClick.AddListener(
                HandleUndoPressed);
            _completionHomeButton.onClick.AddListener(
                HandleHomePressed);
            _completionUndoButton.onClick.AddListener(
                HandleUndoPressed);
            _nextButton.onClick.AddListener(
                HandleNextPressed);
            _musicButton.onClick.AddListener(
                HandleMusicPressed);
            _languageButton.onClick.AddListener(
                HandleLanguagePressed);
            _retrySaveButton.onClick.AddListener(
                HandleRetrySavePressed);
            _returnWithoutSavingButton.onClick.AddListener(
                HandleReturnWithoutSavingPressed);

            _levelButtonActions =
                new UnityAction[_levelButtons.Length];
            for (var index = 0;
                 index < _levelButtons.Length;
                 index++)
            {
                var capturedIndex = index;
                UnityAction action =
                    () => HandleLevelPressed(capturedIndex);
                _levelButtonActions[index] = action;
                _levelButtons[index].Button.onClick.AddListener(
                    action);
            }

            _themeButtonActions =
                new UnityAction[_themeButtons.Length];
            for (var index = 0;
                 index < _themeButtons.Length;
                 index++)
            {
                var capturedIndex = index;
                UnityAction action =
                    () => HandleThemePressed(capturedIndex);
                _themeButtonActions[index] = action;
                _themeButtons[index].Button.onClick.AddListener(
                    action);
            }

            _decorationButtonActions =
                new UnityAction[_decorationButtons.Length];
            for (var index = 0;
                 index < _decorationButtons.Length;
                 index++)
            {
                var capturedIndex = index;
                UnityAction action =
                    () => HandleDecorationPressed(capturedIndex);
                _decorationButtonActions[index] = action;
                _decorationButtons[index].Button.onClick.AddListener(
                    action);
            }

            _progressStore.ProgressChanged +=
                HandleProgressChanged;
            _themeService.ThemeChanged += HandleThemeChanged;
            _musicService.EnabledChanged +=
                HandleMusicEnabledChanged;
            _localization.LocaleChanged +=
                HandleLocaleChanged;
            _roomPresenter.DecorationPlacementRequested +=
                HandleRoomDecorationPlacementRequested;
            _undoHistory.AvailabilityChanged +=
                HandleUndoAvailabilityChanged;
            _workshopHomeController.LevelLaunchRequested +=
                HandleWorkshopLevelLaunchRequested;
            _workshopHomeController.CatalogRequested +=
                HandleWorkshopCatalogRequested;
            _eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound)
            {
                return;
            }

            _playButton?.onClick.RemoveListener(
                HandlePlayPressed);
            _levelsButton?.onClick.RemoveListener(
                HandleLevelsPressed);
            _levelSelectBackButton?.onClick.RemoveListener(
                HandleLevelSelectBackPressed);
            _hudHomeButton?.onClick.RemoveListener(
                HandleHomePressed);
            _hudUndoButton?.onClick.RemoveListener(
                HandleUndoPressed);
            _completionHomeButton?.onClick.RemoveListener(
                HandleHomePressed);
            _completionUndoButton?.onClick.RemoveListener(
                HandleUndoPressed);
            _nextButton?.onClick.RemoveListener(
                HandleNextPressed);
            _musicButton?.onClick.RemoveListener(
                HandleMusicPressed);
            _languageButton?.onClick.RemoveListener(
                HandleLanguagePressed);
            _retrySaveButton?.onClick.RemoveListener(
                HandleRetrySavePressed);
            _returnWithoutSavingButton?.onClick.RemoveListener(
                HandleReturnWithoutSavingPressed);

            if (_levelButtonActions != null &&
                _levelButtons != null)
            {
                var count = Mathf.Min(
                    _levelButtonActions.Length,
                    _levelButtons.Length);
                for (var index = 0; index < count; index++)
                {
                    if (_levelButtons[index]?.Button != null &&
                        _levelButtonActions[index] != null)
                    {
                        _levelButtons[index].Button.onClick
                            .RemoveListener(
                                _levelButtonActions[index]);
                    }
                }
            }

            if (_themeButtonActions != null &&
                _themeButtons != null)
            {
                var count = Mathf.Min(
                    _themeButtonActions.Length,
                    _themeButtons.Length);
                for (var index = 0; index < count; index++)
                {
                    if (_themeButtons[index]?.Button != null &&
                        _themeButtonActions[index] != null)
                    {
                        _themeButtons[index].Button.onClick
                            .RemoveListener(
                                _themeButtonActions[index]);
                    }
                }
            }

            if (_decorationButtonActions != null &&
                _decorationButtons != null)
            {
                var count = Mathf.Min(
                    _decorationButtonActions.Length,
                    _decorationButtons.Length);
                for (var index = 0; index < count; index++)
                {
                    if (_decorationButtons[index]?.Button != null &&
                        _decorationButtonActions[index] != null)
                    {
                        _decorationButtons[index].Button.onClick
                            .RemoveListener(
                                _decorationButtonActions[index]);
                    }
                }
            }

            if (_progressStore != null)
            {
                _progressStore.ProgressChanged -=
                    HandleProgressChanged;
            }

            if (_themeService != null)
            {
                _themeService.ThemeChanged -=
                    HandleThemeChanged;
            }

            if (_musicService != null)
            {
                _musicService.EnabledChanged -=
                    HandleMusicEnabledChanged;
            }

            if (_localization != null)
            {
                _localization.LocaleChanged -=
                    HandleLocaleChanged;
            }

            if (_roomPresenter != null)
            {
                _roomPresenter.DecorationPlacementRequested -=
                    HandleRoomDecorationPlacementRequested;
            }

            if (_undoHistory != null)
            {
                _undoHistory.AvailabilityChanged -=
                    HandleUndoAvailabilityChanged;
            }

            if (_workshopHomeController != null)
            {
                _workshopHomeController.LevelLaunchRequested -=
                    HandleWorkshopLevelLaunchRequested;
                _workshopHomeController.CatalogRequested -=
                    HandleWorkshopCatalogRequested;
            }

            _eventsBound = false;
        }

        private void HandlePlayPressed()
        {
            PlayRecommendedLevelAsync(
                _lifetimeCancellation.Token).Forget();
        }

        private void HandleLevelsPressed()
        {
            ShowLevelSelectAsync(
                _lifetimeCancellation.Token).Forget();
        }

        private void HandleLevelSelectBackPressed()
        {
            ShowHomeFromMenuAsync(
                _lifetimeCancellation.Token).Forget();
        }

        private void HandleHomePressed()
        {
            ReturnHomeAsync(
                _lifetimeCancellation.Token).Forget();
        }

        private void HandleNextPressed()
        {
            ReturnToWorkshopAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                _lifetimeCancellation.Token).Forget();
        }

        private void HandleRetrySavePressed()
        {
            if (CanNavigate() &&
                _lastCompletionStatus ==
                    ProfileMutationStatus.PersistFailed)
            {
                CompleteCapturedLevel();
            }
        }

        private void HandleReturnWithoutSavingPressed()
        {
            if (CanNavigate() &&
                _lastCompletionStatus ==
                    ProfileMutationStatus.PersistFailed)
            {
                ReturnToWorkshopAsync(
                    WorkshopHomeEntryReason.ReturnFromLevel,
                    _lifetimeCancellation.Token).Forget();
            }
        }

        private void HandleWorkshopLevelLaunchRequested(
            LevelLaunchRequest request)
        {
            PlayLevelAsync(
                request,
                _lifetimeCancellation.Token).Forget();
        }

        private void HandleWorkshopCatalogRequested()
        {
            ShowLevelSelectAsync(
                _lifetimeCancellation.Token).Forget();
        }

        private void HandleUndoPressed()
        {
            if (CanNavigate() && _undoHistory.Undo())
            {
                _levelUndoCount++;
                if (_boundLevel != null &&
                    _currentLevelIndex >= 0)
                {
                    _analytics.Track(
                        ProductAnalyticsEvent.UndoUsed(
                            _boundLevel.Definition,
                            _currentLevelIndex,
                            _levelUndoCount,
                            _boundLevel.ProgressCurrent,
                            _boundLevel.ProgressTotal,
                            HasValidRestorationChapter(
                                _boundLevel.Definition)));
                }

                RefreshNavigationAvailability();
            }
        }

        private void HandleUndoAvailabilityChanged(bool canUndo)
        {
            RefreshNavigationAvailability();
        }

        private bool HasValidRestorationChapter(
            LevelDefinition definition)
        {
            return
                definition != null &&
                _levelCatalog != null &&
                _levelCatalog.IsRestorationChapterValid(
                    definition.RestorationChapterId);
        }

        private void HandleMusicPressed()
        {
            if (CanNavigate())
            {
                _musicService.Toggle();
            }
        }

        private void HandleLanguagePressed()
        {
            if (CanNavigate())
            {
                _localization.ToggleLocale();
            }
        }

        private void HandleLocaleChanged(DemoLocale locale)
        {
            if (_initialized)
            {
                _analytics.Track(
                    ProductAnalyticsEvent.LocaleChanged(
                        locale.ToString()));
            }

            RefreshLocalizedUi();
        }

        private void HandleLevelPressed(int levelIndex)
        {
            PlayLevelAsync(
                levelIndex,
                _lifetimeCancellation.Token).Forget();
        }

        private void HandleThemePressed(int themeIndex)
        {
            if (CanNavigate() &&
                _themeService.SelectTheme(themeIndex) &&
                _themeService.Current != null)
            {
                _analytics.Track(
                    ProductAnalyticsEvent.ThemeSelected(
                        _themeService.Current.Id));
            }
        }

        private void HandleDecorationPressed(int decorationIndex)
        {
            if (CanNavigate())
            {
                _roomPresenter.RequestDecorationPlacement(
                    decorationIndex);
            }
        }

        private void HandleRoomDecorationPlacementRequested(
            int decorationIndex)
        {
            if (!CanNavigate() ||
                !_decorationCatalog.TryGetDefinition(
                    decorationIndex,
                    out DemoDecorationDefinition decoration))
            {
                return;
            }

            bool owned =
                decorationIndex >= 0 &&
                decorationIndex < 32 &&
                (_progressStore.Current.OwnedDecorationMask &
                    (1 << decorationIndex)) != 0;
            if (owned)
            {
                if (_progressStore.TrySelectDecoration(
                        decorationIndex))
                {
                    _analytics.Track(
                        ProductAnalyticsEvent.DecorationSelected(
                            decoration.Id,
                            "owned",
                            _progressStore.Current.CozyTokens));
                }

                return;
            }

            if (_progressStore.TryPurchaseAndSelectDecoration(
                    decorationIndex,
                    decoration.Cost))
            {
                _analytics.Track(
                    ProductAnalyticsEvent.DecorationPurchased(
                        decoration.Id,
                        decoration.Cost,
                        _progressStore.Current.CozyTokens));
                _analytics.Track(
                    ProductAnalyticsEvent.DecorationSelected(
                        decoration.Id,
                        "purchase",
                        _progressStore.Current.CozyTokens));
            }
        }

        private void ValidateDependencies()
        {
            if (_levelFlow == null ||
                _levelCatalog == null ||
                _decorationCatalog == null ||
                _progressStore == null ||
                _themeService == null ||
                _localization == null ||
                _musicService == null ||
                _undoHistory == null ||
                _analytics == null ||
                _clock == null ||
                _workshopHomeController == null ||
                _workshopHomeRecovery == null ||
                _workshopFlowCoordinator == null ||
                _workshopText == null ||
                _livingWorkshopCatalog == null ||
                _workshopAnalyticsSession == null ||
                _workshopAvailability == null)
            {
                throw new InvalidOperationException(
                    "DemoExperienceController was not injected.");
            }
        }

        private void ValidateSerializedReferences()
        {
            if (_homeScreen == null ||
                _levelSelectScreen == null ||
                _hudScreen == null ||
                _completionScreen == null ||
                _loadingOverlay == null ||
                _levelTransitionCurtain == null ||
                _playButton == null ||
                _levelsButton == null ||
                _levelSelectBackButton == null ||
                _hudHomeButton == null ||
                _hudUndoButton == null ||
                _completionHomeButton == null ||
                _completionUndoButton == null ||
                _nextButton == null ||
                _musicButton == null ||
                _languageButton == null ||
                _retrySaveButton == null ||
                _returnWithoutSavingButton == null ||
                _languageButtonText == null ||
                _retrySaveButtonText == null ||
                _returnWithoutSavingButtonText == null ||
                _roomCurrencyText == null ||
                _completionRewardText == null ||
                _roomPresenter == null ||
                _levelThemeApplicator == null)
            {
                throw new InvalidOperationException(
                    "DemoExperienceController has missing screen, button, " +
                    "room, copy, or theme-applicator references.");
            }

            // One card per catalog level. Tying this to the catalog rather
            // than a fixed number is what lets the level set grow.
            if (_levelButtons == null ||
                _levelButtons.Length < MinimumDemoLevelCount ||
                _levelButtons.Length != _levelCatalog.Count)
            {
                throw new InvalidOperationException(
                    "The level select needs one button binding per " +
                    "catalog level, but has " +
                    (_levelButtons?.Length ?? 0) +
                    " for " +
                    _levelCatalog.Count +
                    " levels.");
            }

            for (var index = 0;
                 index < _levelButtons.Length;
                 index++)
            {
                if (_levelButtons[index] == null ||
                    !_levelButtons[index].IsConfigured)
                {
                    throw new InvalidOperationException(
                        $"Demo level button {index} is incomplete.");
                }
            }

            if (_themeButtons == null ||
                (_themeButtons.Length != 0 &&
                 _themeButtons.Length != ExpectedThemeCount))
            {
                throw new InvalidOperationException(
                    "Theme bindings must be hidden or provide exactly " +
                    "three configured controls.");
            }

            for (var index = 0;
                 index < _themeButtons.Length;
                 index++)
            {
                if (_themeButtons[index] == null ||
                    !_themeButtons[index].IsConfigured)
                {
                    throw new InvalidOperationException(
                        $"Demo theme button {index} is incomplete.");
                }
            }

            if (_decorationButtons == null ||
                (_decorationButtons.Length != 0 &&
                 _decorationButtons.Length !=
                    _decorationCatalog.Count))
            {
                throw new InvalidOperationException(
                    "The home room needs one decoration button binding per " +
                    "catalog entry, but has " +
                    (_decorationButtons?.Length ?? 0) +
                    " for " +
                    _decorationCatalog.Count +
                    " decorations.");
            }

            for (var index = 0;
                 index < _decorationButtons.Length;
                 index++)
            {
                if (_decorationButtons[index] == null ||
                    !_decorationButtons[index].IsConfigured)
                {
                    throw new InvalidOperationException(
                        $"Demo decoration button {index} is incomplete.");
                }
            }

            if (_roomPresenter.RoomRoot == null ||
                (_roomPresenter.DecorationCount != 0 &&
                 _roomPresenter.DecorationCount !=
                    _decorationCatalog.Count))
            {
                throw new InvalidOperationException(
                    "The home room presenter must provide one visual per " +
                    "decoration catalog entry.");
            }

            if (_localizedTexts == null ||
                _localizedTexts.Length == 0)
            {
                throw new InvalidOperationException(
                    "Demo localized text bindings are required.");
            }

            for (var index = 0;
                 index < _localizedTexts.Length;
                 index++)
            {
                if (_localizedTexts[index] == null ||
                    !_localizedTexts[index].IsConfigured)
                {
                    throw new InvalidOperationException(
                        $"Demo localized text binding {index} is incomplete.");
                }
            }
        }

        private void OnDestroy()
        {
            _destroying = true;
            _initialized = false;
            _roomPresenter?.SetVisible(false);
            UnbindLevel();
            UnbindEvents();
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        private static void SetScreenImmediate(
            CanvasGroup group,
            bool visible)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (group.gameObject.activeSelf != visible)
            {
                group.gameObject.SetActive(visible);
            }
        }

        private static void SetButtonInteractable(
            Button button,
            bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void ApplyColor(
            Image[] images,
            Color color)
        {
            if (images == null)
            {
                return;
            }

            for (var index = 0; index < images.Length; index++)
            {
                if (images[index] != null)
                {
                    images[index].color = color;
                }
            }
        }

        private static void ApplyColor(
            Text[] texts,
            Color color)
        {
            if (texts == null)
            {
                return;
            }

            for (var index = 0; index < texts.Length; index++)
            {
                if (texts[index] != null)
                {
                    texts[index].color = color;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_levelButtons != null &&
                _levelButtons.Length < MinimumDemoLevelCount)
            {
                Debug.LogWarning(
                    "Calm Space needs at least one level button.",
                    this);
            }

            if (_themeButtons != null &&
                _themeButtons.Length != 0 &&
                _themeButtons.Length != ExpectedThemeCount)
            {
                Debug.LogWarning(
                    "Calm Space demo expects exactly three theme buttons.",
                    this);
            }

            if (_decorationButtons == null)
            {
                Debug.LogWarning(
                    "Calm Space decoration bindings must be an array.",
                    this);
            }
        }
#endif

        [Serializable]
        public sealed class LocalizedTextBinding
        {
            [SerializeField]
            private Text _text;

            [SerializeField]
            private DemoTextKey _key;

            public bool IsConfigured => _text != null;

            public void Apply(
                IDemoLocalizationService localization)
            {
                if (_text != null && localization != null)
                {
                    _text.text = localization.Get(_key);
                }
            }
        }

        [Serializable]
        public sealed class LevelButtonBinding
        {
            [SerializeField]
            private Button _button;

            [SerializeField]
            private Text _numberText;

            [SerializeField]
            private Text _titleText;

            [SerializeField]
            private GameObject _lockRoot;

            [SerializeField]
            private GameObject _completedRoot;

            [SerializeField]
            private Image _cardImage;

            public Button Button => _button;

            public bool IsConfigured =>
                _button != null &&
                _numberText != null &&
                _titleText != null &&
                _lockRoot != null &&
                _completedRoot != null &&
                _cardImage != null;

            public void SetVisible(bool visible)
            {
                if (_button != null &&
                    _button.gameObject.activeSelf != visible)
                {
                    _button.gameObject.SetActive(visible);
                }
            }

            public void SetContent(
                int levelNumber,
                string displayName)
            {
                if (_numberText != null)
                {
                    _numberText.text =
                        levelNumber.ToString("00");
                }

                if (_titleText != null)
                {
                    RectTransform titleRect =
                        _titleText.rectTransform;
                    titleRect.anchoredPosition =
                        new Vector2(28f, 23f);
                    titleRect.sizeDelta =
                        new Vector2(368f, 116f);
                    _titleText.fontSize = 28;
                    _titleText.resizeTextForBestFit = true;
                    _titleText.resizeTextMinSize = 23;
                    _titleText.resizeTextMaxSize = 28;
                    _titleText.horizontalOverflow =
                        HorizontalWrapMode.Wrap;
                    _titleText.verticalOverflow =
                        VerticalWrapMode.Truncate;
                    _titleText.lineSpacing = 0.9f;
                    _titleText.text =
                        displayName ?? string.Empty;
                }
            }

            public void SetState(
                bool unlocked,
                bool completed,
                bool navigationEnabled,
                ThemePalette palette)
            {
                if (_button != null)
                {
                    _button.interactable =
                        unlocked && navigationEnabled;
                }

                _lockRoot?.SetActive(!unlocked);
                _completedRoot?.SetActive(completed);

                if (palette == null)
                {
                    return;
                }

                if (_cardImage != null)
                {
                    var cardColor =
                        completed
                            ? palette.SecondaryAccent
                            : palette.Panel;
                    cardColor.a = unlocked ? 0.96f : 0.58f;
                    _cardImage.color = cardColor;
                }

                if (_numberText != null)
                {
                    _numberText.color =
                        unlocked
                            ? palette.Accent
                            : palette.SecondaryText;
                }

                if (_titleText != null)
                {
                    _titleText.color =
                        unlocked
                            ? palette.PrimaryText
                            : palette.SecondaryText;
                }
            }
        }

        [Serializable]
        public sealed class DecorationButtonBinding
        {
            [SerializeField]
            private Button _button;

            [SerializeField]
            private Image _preview;

            [SerializeField]
            private Text _name;

            [SerializeField]
            private Text _state;

            [SerializeField]
            private GameObject _selectedRoot;

            public Button Button => _button;

            public bool IsConfigured =>
                _button != null &&
                _preview != null &&
                _name != null &&
                _state != null &&
                _selectedRoot != null;

            public void SetVisible(bool visible)
            {
                if (_button != null &&
                    _button.gameObject.activeSelf != visible)
                {
                    _button.gameObject.SetActive(visible);
                }
            }

            public void SetContent(
                string localizedName,
                string localizedState,
                bool selected,
                bool interactable,
                ThemePalette palette)
            {
                if (_button != null)
                {
                    _button.interactable = interactable;
                }

                if (_name != null)
                {
                    _name.text = localizedName ?? string.Empty;
                }

                if (_state != null)
                {
                    _state.text = localizedState ?? string.Empty;
                }

                _selectedRoot?.SetActive(selected);

                if (palette == null)
                {
                    return;
                }

                if (_name != null)
                {
                    _name.color = palette.PrimaryText;
                }

                if (_state != null)
                {
                    _state.color = selected
                        ? palette.SecondaryAccent
                        : palette.SecondaryText;
                }
            }
        }

        [Serializable]
        public sealed class ThemeButtonBinding
        {
            [SerializeField]
            private Button _button;

            [SerializeField]
            private Image _swatch;

            [SerializeField]
            private Text _label;

            [SerializeField]
            private GameObject _selectedRoot;

            public Button Button => _button;

            public bool IsConfigured =>
                _button != null &&
                _swatch != null &&
                _label != null &&
                _selectedRoot != null;

            public void SetVisible(bool visible)
            {
                if (_button != null &&
                    _button.gameObject.activeSelf != visible)
                {
                    _button.gameObject.SetActive(visible);
                }
            }

            public void SetPalette(
                ThemePalette palette,
                string localizedDisplayName,
                bool selected,
                bool navigationEnabled)
            {
                if (palette == null)
                {
                    return;
                }

                if (_button != null)
                {
                    _button.interactable = navigationEnabled;
                }

                if (_swatch != null)
                {
                    _swatch.color = palette.Accent;
                }

                if (_label != null)
                {
                    _label.text =
                        localizedDisplayName ?? string.Empty;
                    _label.color = palette.PrimaryText;
                }

                _selectedRoot?.SetActive(selected);
            }
        }
    }
}
