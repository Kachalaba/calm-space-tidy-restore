using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CalmSpace.UI
{
    public readonly struct WorkshopHomeViewState
    {
        public WorkshopHomeViewState(
            string primaryLabel,
            string progressLabel,
            bool primaryVisible,
            bool hotspotVisible)
        {
            PrimaryLabel = primaryLabel ?? string.Empty;
            ProgressLabel = progressLabel ?? string.Empty;
            PrimaryVisible = primaryVisible;
            HotspotVisible = hotspotVisible;
        }

        public string PrimaryLabel { get; }
        public string ProgressLabel { get; }
        public bool PrimaryVisible { get; }
        public bool HotspotVisible { get; }
    }

    [DisallowMultipleComponent]
    public sealed class WorkshopHomeView : MonoBehaviour
    {
        [SerializeField] private Button _primaryButton;
        [SerializeField] private Button _catalogButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _hotspotButton;
        [SerializeField] private GameObject _albumRoot;
        [SerializeField] private GameObject _decorRoot;
        [SerializeField] private GameObject _dailyCareRoot;
        [SerializeField] private GameObject _relaxPassRoot;
        [SerializeField] private Text _primaryLabel;
        [SerializeField] private Text _progressLabel;
        [SerializeField] private Text _catalogLabel;
        [SerializeField] private WorkshopBottomSheet _bottomSheet;
        [SerializeField] private Button _settingsMusicButton;
        [SerializeField] private Button _settingsLocaleButton;
        [SerializeField] private Button _settingsHapticButton;
        [SerializeField] private Text _settingsMusicLabel;
        [SerializeField] private Text _settingsLocaleLabel;
        [SerializeField] private Text _settingsHapticLabel;

        private UnityAction _primaryAction;
        private UnityAction _catalogAction;
        private UnityAction _settingsAction;
        private UnityAction _hotspotAction;
        private UnityAction _settingsMusicAction;
        private UnityAction _settingsLocaleAction;
        private UnityAction _settingsHapticAction;
        private bool _bound;

        public Button PrimaryButton => _primaryButton;
        public Button CatalogButton => _catalogButton;
        public Button SettingsButton => _settingsButton;
        public WorkshopBottomSheet BottomSheet => _bottomSheet;
        public Button SettingsMusicButton => _settingsMusicButton;
        public Button SettingsLocaleButton => _settingsLocaleButton;
        public Button SettingsHapticButton => _settingsHapticButton;
        public int ActiveHotspotCount => IsActive(_hotspotButton) ? 1 : 0;
        public int OpenBottomSheetCount =>
            _bottomSheet != null && _bottomSheet.IsOpen ? 1 : 0;
        public int UnavailableCapabilityCount =>
            ActiveCount(_albumRoot) +
            ActiveCount(_decorRoot) +
            ActiveCount(_dailyCareRoot) +
            ActiveCount(_relaxPassRoot);

        public bool VisibleActionControlsHaveBindings =>
            HasBinding(_primaryButton, _primaryAction) &&
            HasBinding(_catalogButton, _catalogAction) &&
            HasBinding(_settingsButton, _settingsAction) &&
            HasBinding(_hotspotButton, _hotspotAction) &&
            (_bottomSheet == null ||
             !_bottomSheet.IsOpen ||
             _bottomSheet.ActionButton == null ||
             !_bottomSheet.ActionButton.gameObject.activeSelf ||
             _bottomSheet.ActionButton.interactable) &&
            HasBinding(_settingsMusicButton, _settingsMusicAction) &&
            HasBinding(_settingsLocaleButton, _settingsLocaleAction) &&
            HasBinding(_settingsHapticButton, _settingsHapticAction);

        public void Configure(
            Button primaryButton,
            Button catalogButton,
            Button settingsButton,
            Button hotspotButton,
            GameObject albumRoot,
            GameObject decorRoot,
            GameObject dailyCareRoot,
            GameObject relaxPassRoot,
            Text primaryLabel,
            Text progressLabel,
            WorkshopBottomSheet bottomSheet)
        {
            Unbind();
            _primaryButton = primaryButton;
            _catalogButton = catalogButton;
            _settingsButton = settingsButton;
            _hotspotButton = hotspotButton;
            _albumRoot = albumRoot;
            _decorRoot = decorRoot;
            _dailyCareRoot = dailyCareRoot;
            _relaxPassRoot = relaxPassRoot;
            _primaryLabel = primaryLabel;
            _progressLabel = progressLabel;
            _bottomSheet = bottomSheet;
            _settingsMusicButton = null;
            _settingsLocaleButton = null;
            _settingsHapticButton = null;
            _settingsMusicLabel = null;
            _settingsLocaleLabel = null;
            _settingsHapticLabel = null;
            SetActive(_settingsButton, false);
            HideUnavailableCapabilities();
        }

        public void ConfigureSettings(
            Button musicButton,
            Button localeButton,
            Button hapticButton,
            Text musicLabel,
            Text localeLabel,
            Text hapticLabel)
        {
            _settingsMusicButton = musicButton;
            _settingsLocaleButton = localeButton;
            _settingsHapticButton = hapticButton;
            _settingsMusicLabel = musicLabel;
            _settingsLocaleLabel = localeLabel;
            _settingsHapticLabel = hapticLabel;
            bool configured = musicButton != null &&
                localeButton != null && hapticButton != null;
            SetActive(_settingsButton, configured);
        }

        public void ConfigureChrome(Text catalogLabel)
        {
            _catalogLabel = catalogLabel;
        }

        private void Awake()
        {
            HideUnavailableCapabilities();
        }

        public void Bind(
            Action primary,
            Action catalog,
            Action settings,
            Action hotspot)
        {
            Unbind();
            _primaryAction = primary == null
                ? null
                : new UnityAction(primary);
            _catalogAction = catalog == null
                ? null
                : new UnityAction(catalog);
            _settingsAction = settings == null
                ? null
                : new UnityAction(settings);
            _hotspotAction = hotspot == null
                ? null
                : new UnityAction(hotspot);
            Add(_primaryButton, _primaryAction);
            Add(_catalogButton, _catalogAction);
            Add(_settingsButton, _settingsAction);
            Add(_hotspotButton, _hotspotAction);
            _bound = true;
        }

        public void BindSettings(
            Action music,
            Action locale,
            Action haptic)
        {
            Remove(_settingsMusicButton, _settingsMusicAction);
            Remove(_settingsLocaleButton, _settingsLocaleAction);
            Remove(_settingsHapticButton, _settingsHapticAction);
            _settingsMusicAction = music == null
                ? null
                : new UnityAction(music);
            _settingsLocaleAction = locale == null
                ? null
                : new UnityAction(locale);
            _settingsHapticAction = haptic == null
                ? null
                : new UnityAction(haptic);
            Add(_settingsMusicButton, _settingsMusicAction);
            Add(_settingsLocaleButton, _settingsLocaleAction);
            Add(_settingsHapticButton, _settingsHapticAction);
        }

        public void Unbind()
        {
            Remove(_primaryButton, _primaryAction);
            Remove(_catalogButton, _catalogAction);
            Remove(_settingsButton, _settingsAction);
            Remove(_hotspotButton, _hotspotAction);
            Remove(_settingsMusicButton, _settingsMusicAction);
            Remove(_settingsLocaleButton, _settingsLocaleAction);
            Remove(_settingsHapticButton, _settingsHapticAction);
            _primaryAction = null;
            _catalogAction = null;
            _settingsAction = null;
            _hotspotAction = null;
            _settingsMusicAction = null;
            _settingsLocaleAction = null;
            _settingsHapticAction = null;
            _bound = false;
        }

        public void Render(WorkshopHomeViewState state)
        {
            SetActive(_primaryButton, state.PrimaryVisible);
            SetActive(_hotspotButton, state.HotspotVisible);
            if (_primaryLabel != null)
            {
                _primaryLabel.text = state.PrimaryLabel;
            }

            if (_progressLabel != null)
            {
                _progressLabel.text = state.ProgressLabel;
            }

            HideUnavailableCapabilities();
        }

        public void ShowRetry(Action retry)
        {
            _bottomSheet?.Open(retry);
        }

        public void RenderSettings(
            string musicLabel,
            string localeLabel,
            string hapticLabel)
        {
            if (_settingsMusicLabel != null)
            {
                _settingsMusicLabel.text = musicLabel ?? string.Empty;
            }

            if (_settingsLocaleLabel != null)
            {
                _settingsLocaleLabel.text = localeLabel ?? string.Empty;
            }

            if (_settingsHapticLabel != null)
            {
                _settingsHapticLabel.text = hapticLabel ?? string.Empty;
            }
        }

        public void RenderChrome(string catalogLabel)
        {
            if (_catalogLabel != null)
            {
                _catalogLabel.text = catalogLabel ?? string.Empty;
            }
        }

        public void CloseBottomSheet()
        {
            _bottomSheet?.Close();
        }

        private void HideUnavailableCapabilities()
        {
            SetActive(_albumRoot, false);
            SetActive(_decorRoot, false);
            SetActive(_dailyCareRoot, false);
            SetActive(_relaxPassRoot, false);
        }

        private static bool HasBinding(Button button, UnityAction action)
        {
            return !IsActive(button) || action != null;
        }

        private static bool IsActive(Button button)
        {
            return button != null && button.gameObject.activeSelf;
        }

        private static int ActiveCount(GameObject root)
        {
            return root != null && root.activeSelf ? 1 : 0;
        }

        private static void Add(Button button, UnityAction action)
        {
            if (button != null && action != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void Remove(Button button, UnityAction action)
        {
            if (button != null && action != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null)
            {
                SetActive(button.gameObject, active);
            }
        }

        private static void SetActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        private void OnDestroy()
        {
            if (_bound)
            {
                Unbind();
            }
        }
    }
}
