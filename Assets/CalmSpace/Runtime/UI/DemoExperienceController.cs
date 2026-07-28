using System;
using System.Threading;
using CalmSpace.Audio;
using CalmSpace.Cleaning;
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
        private const int ExpectedDemoLevelCount = 6;
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
        private Button _completionHomeButton;

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

        [Header("Level selection (six entries)")]
        [SerializeField]
        private LevelButtonBinding[] _levelButtons =
            new LevelButtonBinding[ExpectedDemoLevelCount];

        [Header("Themes (three entries)")]
        [SerializeField]
        private ThemeButtonBinding[] _themeButtons =
            new ThemeButtonBinding[ExpectedThemeCount];

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
        private IDemoProgressStore _progressStore;
        private IDemoThemeService _themeService;
        private IDemoLocalizationService _localization;
        private IBackgroundMusicService _musicService;

        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        private UnityAction[] _levelButtonActions;
        private UnityAction[] _themeButtonActions;
        private CanvasGroup _activeScreen;
        private LevelBase _boundLevel;
        private RenderTextureCleaner _boundCleaner;
        private int _currentLevelIndex = -1;
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
            IDemoProgressStore progressStore,
            IDemoThemeService themeService,
            IDemoLocalizationService localization,
            IBackgroundMusicService musicService)
        {
            _levelFlow = levelFlow ??
                throw new ArgumentNullException(nameof(levelFlow));
            _levelCatalog = levelCatalog ??
                throw new ArgumentNullException(nameof(levelCatalog));
            _progressStore = progressStore ??
                throw new ArgumentNullException(nameof(progressStore));
            _themeService = themeService ??
                throw new ArgumentNullException(nameof(themeService));
            _localization = localization ??
                throw new ArgumentNullException(nameof(localization));
            _musicService = musicService ??
                throw new ArgumentNullException(nameof(musicService));
        }

        private void Awake()
        {
            SetScreenImmediate(_homeScreen, true);
            SetScreenImmediate(_levelSelectScreen, false);
            SetScreenImmediate(_hudScreen, false);
            SetScreenImmediate(_completionScreen, false);
            SetScreenImmediate(_loadingOverlay, false);
            _activeScreen = _homeScreen;
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
                    _themeService.DefaultThemeId);
                _themeService.Initialize();
                _localization.Initialize();
                BindEvents();
                _musicService.Initialize(
                    _progressStore.Current.MusicEnabled);

                ApplyTheme(_themeService.Current);
                RefreshLocalizedUi();
                RefreshProgressUi();
                RefreshLevelButtons();
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
                RefreshNavigationAvailability();
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
                var nextIndex = _currentLevelIndex + 1;
                if (nextIndex >= _levelCatalog.Count)
                {
                    await ReturnHomeCoreAsync(cancellationToken);
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
                await ReturnHomeCoreAsync(cancellationToken);
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
                EndNavigation();
            }
        }

        private async UniTask<bool> LoadLevelCoreAsync(
            string levelId,
            int requestedIndex,
            bool tryInterstitial,
            CancellationToken cancellationToken)
        {
            UnbindLevel();

            bool loaded;
            if (tryInterstitial &&
                _levelFlow.CurrentLevel != null &&
                requestedIndex ==
                    _levelFlow.CurrentLevelIndex + 1)
            {
                loaded =
                    await _levelFlow.LoadNextLevelAsync(
                        true,
                        cancellationToken);
            }
            else
            {
                loaded =
                    await _levelFlow.LoadLevelAsync(
                        levelId,
                        cancellationToken);
            }

            if (!loaded || _levelFlow.CurrentLevel == null)
            {
                _currentLevelIndex = -1;
                _levelThemeApplicator?.ClearCurrentLevel();
                await ShowScreenAsync(
                    _homeScreen,
                    cancellationToken);
                return false;
            }

            _currentLevelIndex = _levelFlow.CurrentLevelIndex;
            BindLevel(_levelFlow.CurrentLevel);
            ApplyCurrentThemeToLevel();
            RefreshHud(
                _boundLevel.PlacedItemCount,
                _boundLevel.ItemCount);
            if (_boundCleaner != null)
            {
                RefreshCleaningProgress(
                    _boundCleaner.CleanedFraction);
            }
            await ShowScreenAsync(
                _hudScreen,
                cancellationToken);
            return true;
        }

        private async UniTask ReturnHomeCoreAsync(
            CancellationToken cancellationToken)
        {
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
            await ShowScreenAsync(
                _homeScreen,
                cancellationToken);
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
            _boundCleaner =
                _boundLevel.GetComponentInChildren<
                    RenderTextureCleaner>(true);
            if (_boundCleaner != null)
            {
                _boundCleaner.CleanProgressChanged +=
                    HandleCleanProgressChanged;
                RefreshCleaningProgress(
                    _boundCleaner.CleanedFraction);
            }
        }

        private void UnbindLevel()
        {
            if (_boundCleaner != null)
            {
                _boundCleaner.CleanProgressChanged -=
                    HandleCleanProgressChanged;
                _boundCleaner = null;
            }

            if (_boundLevel != null)
            {
                _boundLevel.LevelCompleted -= HandleLevelCompleted;
                _boundLevel.ProgressChanged -=
                    HandleLevelProgressChanged;
            }

            _boundLevel = null;
        }

        private void HandleLevelProgressChanged(
            int placed,
            int total)
        {
            RefreshHud(placed, total);
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

            _progressStore.MarkLevelCompleted(
                _currentLevelIndex);
            RefreshProgressUi();
            RefreshLevelButtons();

            if (_completionTitleText != null)
            {
                _completionTitleText.text =
                    _localization.Get(
                        DemoTextKey.CompletionTitle);
            }

            if (_completionBodyText != null)
            {
                _completionBodyText.text =
                    _localization.FormatCompletionBody(
                        level.Definition);
            }

            var hasNext =
                _currentLevelIndex + 1 < _levelCatalog.Count;
            if (_completionHomeButton != null)
            {
                _completionHomeButton.gameObject.SetActive(hasNext);
            }

            if (_nextButtonText != null)
            {
                _nextButtonText.text =
                    hasNext
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
        }

        private void HandleMusicEnabledChanged(bool enabled)
        {
            if (_progressStore.IsInitialized &&
                _progressStore.Current.MusicEnabled != enabled)
            {
                _progressStore.SetMusicEnabled(enabled);
            }

            RefreshMusicUi(enabled);
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
            ApplyCurrentThemeToLevel();
            RefreshThemeButtons();
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
            if (!_progressStore.IsInitialized ||
                _homeProgressText == null)
            {
                return;
            }

            var completed =
                DemoProgressRules.CountCompleted(
                    _progressStore.Current,
                    _levelCatalog.Count);
            _homeProgressText.text =
                _localization.FormatHomeProgress(
                    completed,
                    _levelCatalog.Count);
        }

        private void RefreshHud(int placed, int total)
        {
            if (_hudLevelNameText != null)
            {
                _hudLevelNameText.text =
                    _localization.GetLevelName(
                        _boundLevel?.Definition);
            }

            if (_hudProgressText == null)
            {
                return;
            }

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

            int percentage = Mathf.RoundToInt(
                Mathf.Clamp01(fraction) * 100f);
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
                    _localization.GetLevelName(
                        entry.Definition));
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
                    _boundLevel.PlacedItemCount,
                    _boundLevel.ItemCount);
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
            SetButtonInteractable(
                _completionHomeButton,
                enabled);
            SetButtonInteractable(_nextButton, enabled);
            SetButtonInteractable(_musicButton, enabled);
            SetButtonInteractable(_languageButton, enabled);
            RefreshLevelButtons();
            RefreshThemeButtons();
        }

        private void BeginNavigation(bool showLoading)
        {
            _isNavigating = true;
            if (showLoading)
            {
                SetScreenImmediate(_loadingOverlay, true);
            }

            RefreshNavigationAvailability();
        }

        private void EndNavigation()
        {
            _isNavigating = false;
            SetScreenImmediate(_loadingOverlay, false);
            RefreshNavigationAvailability();
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
            _completionHomeButton.onClick.AddListener(
                HandleHomePressed);
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

            _progressStore.ProgressChanged +=
                HandleProgressChanged;
            _themeService.ThemeChanged += HandleThemeChanged;
            _musicService.EnabledChanged +=
                HandleMusicEnabledChanged;
            _localization.LocaleChanged +=
                HandleLocaleChanged;
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
            _completionHomeButton?.onClick.RemoveListener(
                HandleHomePressed);
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
            if (CanNavigate())
            {
                _themeService.SelectTheme(themeIndex);
            }
        }

        private void ValidateDependencies()
        {
            if (_levelFlow == null ||
                _levelCatalog == null ||
                _progressStore == null ||
                _themeService == null ||
                _localization == null ||
                _musicService == null)
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
                _playButton == null ||
                _levelsButton == null ||
                _levelSelectBackButton == null ||
                _hudHomeButton == null ||
                _completionHomeButton == null ||
                _nextButton == null ||
                _musicButton == null ||
                _languageButton == null ||
                _languageButtonText == null ||
                _levelThemeApplicator == null)
            {
                throw new InvalidOperationException(
                    "DemoExperienceController has missing screen, button, " +
                    "or theme-applicator references.");
            }

            if (_levelButtons == null ||
                _levelButtons.Length != ExpectedDemoLevelCount)
            {
                throw new InvalidOperationException(
                    "Exactly six demo level button bindings are required.");
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
                _levelButtons.Length != ExpectedDemoLevelCount)
            {
                Debug.LogWarning(
                    "Calm Space demo expects exactly six level buttons.",
                    this);
            }

            if (_themeButtons != null &&
                _themeButtons.Length != ExpectedThemeCount)
            {
                Debug.LogWarning(
                    "Calm Space demo expects exactly three theme buttons.",
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
