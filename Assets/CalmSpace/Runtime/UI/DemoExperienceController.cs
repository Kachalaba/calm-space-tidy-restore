using System;
using System.Threading;
using CalmSpace.Analytics;
using CalmSpace.Audio;
using CalmSpace.Cleaning;
using CalmSpace.Core;
using CalmSpace.Demo;
using CalmSpace.Levels;
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
        private bool _eventsBound;
        private bool _initialized;
        private bool _initializing;
        private bool _isNavigating;
        private bool _destroying;

        public bool IsInitialized => _initialized;

        public bool IsNavigating => _isNavigating;

        public int CurrentLevelIndex => _currentLevelIndex;

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
            IMonotonicClock clock)
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

            var recommended =
                DemoProgressRules.GetRecommendedLevel(
                    _progressStore.Current,
                    _levelCatalog.Count);
            return PlayLevelAsync(recommended, cancellationToken);
        }

        public async UniTask<bool> PlayLevelAsync(
            int levelIndex,
            CancellationToken cancellationToken = default)
        {
            if (!CanNavigate() ||
                !_progressStore.IsLevelUnlocked(levelIndex) ||
                !_levelCatalog.TryGetEntry(
                    levelIndex,
                    out var entry))
            {
                return false;
            }

            BeginNavigation(true);
            try
            {
                await _levelTransitionCurtain.FadeToOpaqueAsync(
                    cancellationToken);
                return await LoadLevelCoreAsync(
                    entry.Definition.LevelId,
                    levelIndex,
                    false,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
            finally
            {
                await RevealAfterLevelTransitionAsync();
                EndNavigation();
            }
        }

        public async UniTask<bool> LoadNextLevelAsync(
            CancellationToken cancellationToken = default)
        {
            if (!CanNavigate() || _currentLevelIndex < 0)
            {
                return false;
            }

            BeginNavigation(true);
            try
            {
                await _levelTransitionCurtain.FadeToOpaqueAsync(
                    cancellationToken);
                var nextIndex = _currentLevelIndex + 1;
                if (nextIndex >= _levelCatalog.Count)
                {
                    await ReturnHomeCoreAsync(
                        cancellationToken,
                        true);
                    return true;
                }

                if (!_progressStore.IsLevelUnlocked(nextIndex) ||
                    !_levelCatalog.TryGetEntry(
                        nextIndex,
                        out var entry))
                {
                    return false;
                }

                return await LoadLevelCoreAsync(
                    entry.Definition.LevelId,
                    nextIndex,
                    true,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
            finally
            {
                await RevealAfterLevelTransitionAsync();
                EndNavigation();
            }
        }

        public async UniTask ReturnHomeAsync(
            CancellationToken cancellationToken = default)
        {
            if (!CanNavigate())
            {
                return;
            }

            BeginNavigation(true);
            try
            {
                await _levelTransitionCurtain.FadeToOpaqueAsync(
                    cancellationToken);
                await ReturnHomeCoreAsync(
                    cancellationToken,
                    true);
            }
            catch (OperationCanceledException)
            {
                // A scene shutdown cancels the visual transition.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                await RevealAfterLevelTransitionAsync();
                EndNavigation();
            }
        }

        private async UniTask<bool> LoadLevelCoreAsync(
            string levelId,
            int requestedIndex,
            bool loadSequentially,
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
            BindLevel(_levelFlow.CurrentLevel);
            _levelUndoCount = 0;
            _levelStartedAtSeconds = _clock.NowSeconds;
            _analytics.Track(
                ProductAnalyticsEvent.LevelStarted(
                    _boundLevel.Definition,
                    _currentLevelIndex,
                    _progressStore.Current.CozyTokens,
                    _levelCatalog.RestorationMetadataValid));
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
                    _levelCatalog.RestorationMetadataValid));
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

        private async UniTask ShowHomeFromMenuAsync(
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
                    _homeScreen,
                    cancellationToken);
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

            _lastCompletionReward = Mathf.Max(
                0,
                _progressStore.CompleteLevelAndReward(
                    _currentLevelIndex,
                    _decorationCatalog.CompletionReward));
            _analytics.Track(
                ProductAnalyticsEvent.LevelCompleted(
                    level.Definition,
                    _currentLevelIndex,
                    GetCurrentLevelDurationSeconds(),
                    _levelUndoCount,
                    _lastCompletionReward,
                    _progressStore.Current.CozyTokens,
                    _lastCompletionReward > 0,
                    _levelCatalog.RestorationMetadataValid));
            RefreshProgressUi();
            RefreshLevelButtons();
            RefreshDecorationUi();
            RefreshCompletionRewardUi();

            if (_completionTitleText != null)
            {
                DemoTextKey titleKey =
                    DemoTextKey.CompletionTitle;
                if (_levelCatalog.TryGetRestorationStage(
                        _currentLevelIndex,
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
                        level.Definition,
                        _levelCatalog.RestorationMetadataValid);
            }

            bool hasNext =
                _currentLevelIndex + 1 < _levelCatalog.Count;
            bool continuesRestoration =
                hasNext &&
                _levelCatalog.IsRestorationContinuation(
                    _currentLevelIndex,
                    _currentLevelIndex + 1);
            if (_completionHomeButton != null)
            {
                _completionHomeButton.gameObject.SetActive(hasNext);
            }

            if (_nextButtonText != null)
            {
                _nextButtonText.text =
                    continuesRestoration
                        ? _localization.Get(
                            DemoTextKey.ContinueRestoration)
                        : hasNext
                            ? _localization.Get(
                                DemoTextKey.NextSpace)
                        : _localization.Get(
                            DemoTextKey.BackHome);
            }

            PresentCompletionAsync(
                _lifetimeCancellation.Token).Forget();
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
                        _levelCatalog.RestorationMetadataValid);
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
                        _levelCatalog.RestorationMetadataValid));
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
            LoadNextLevelAsync(
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
                            _levelCatalog.RestorationMetadataValid));
                }

                RefreshNavigationAvailability();
            }
        }

        private void HandleUndoAvailabilityChanged(bool canUndo)
        {
            RefreshNavigationAvailability();
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
                _clock == null)
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
                _languageButtonText == null ||
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
                _themeButtons.Length != ExpectedThemeCount)
            {
                throw new InvalidOperationException(
                    "Exactly three theme button bindings are required.");
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
                _decorationButtons.Length !=
                    _decorationCatalog.Count)
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
                _roomPresenter.DecorationCount !=
                    _decorationCatalog.Count)
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
                _themeButtons.Length != ExpectedThemeCount)
            {
                Debug.LogWarning(
                    "Calm Space demo expects exactly three theme buttons.",
                    this);
            }

            if (_decorationButtons == null ||
                _decorationButtons.Length == 0)
            {
                Debug.LogWarning(
                    "Calm Space needs home room decoration buttons.",
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
