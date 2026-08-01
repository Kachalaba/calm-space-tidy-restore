using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CalmSpace.UI
{
    [DisallowMultipleComponent]
    public sealed class WorkshopBottomSheet : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _sheet;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _actionButton;
        [SerializeField] private GameObject _settingsContent;
        [SerializeField] private GameObject _recoveryContent;
        [SerializeField] private Text _recoveryTitleLabel;
        [SerializeField] private Text _actionLabel;

        private UnityAction _action;
        private bool _closeBound;

        public bool IsOpen =>
            _sheet != null &&
            _sheet.gameObject.activeSelf &&
            _sheet.interactable;

        public Button ActionButton => _actionButton;

        public void Configure(
            CanvasGroup sheet,
            Button closeButton,
            Button actionButton)
        {
            Configure(
                sheet,
                closeButton,
                actionButton,
                null,
                null,
                null,
                actionButton == null
                    ? null
                    : actionButton.GetComponentInChildren<Text>(true));
        }

        public void Configure(
            CanvasGroup sheet,
            Button closeButton,
            Button actionButton,
            GameObject settingsContent,
            GameObject recoveryContent,
            Text recoveryTitleLabel,
            Text actionLabel)
        {
            UnbindClose();
            ClearAction();
            _sheet = sheet;
            _closeButton = closeButton;
            _actionButton = actionButton;
            _settingsContent = settingsContent;
            _recoveryContent = recoveryContent;
            _recoveryTitleLabel = recoveryTitleLabel;
            _actionLabel = actionLabel;
            BindClose();
            Close();
        }

        private void Awake()
        {
            BindClose();
        }

        public void Open(Action action = null)
        {
            if (action == null)
            {
                OpenSettings();
                return;
            }

            OpenRecovery(
                _recoveryTitleLabel?.text,
                _actionLabel?.text,
                action);
        }

        public void OpenSettings()
        {
            ClearAction();
            SetContentMode(settings: true);
            if (_actionButton != null)
            {
                _actionButton.gameObject.SetActive(false);
                _actionButton.interactable = false;
            }

            SetOpen(true);
        }

        public void OpenRecovery(
            string title,
            string actionLabel,
            Action action)
        {
            ClearAction();
            RenderRecoveryCopy(title, actionLabel);
            SetContentMode(settings: false);
            if (_actionButton != null)
            {
                _action = () =>
                {
                    Close();
                    action?.Invoke();
                };
                _actionButton.onClick.AddListener(_action);
                _actionButton.gameObject.SetActive(true);
                _actionButton.interactable = action != null;
            }

            SetOpen(true);
        }

        public void RenderRecoveryCopy(string title, string actionLabel)
        {
            if (_recoveryTitleLabel != null)
            {
                _recoveryTitleLabel.text = title ?? string.Empty;
            }

            if (_actionLabel != null)
            {
                _actionLabel.text = actionLabel ?? string.Empty;
            }
        }

        public void Close()
        {
            ClearAction();
            SetOpen(false);
        }

        public UniTask CloseAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Close();
            return UniTask.CompletedTask;
        }

        private void BindClose()
        {
            if (_closeBound || _closeButton == null)
            {
                return;
            }

            _closeButton.onClick.AddListener(Close);
            _closeBound = true;
        }

        private void UnbindClose()
        {
            if (!_closeBound)
            {
                return;
            }

            _closeButton?.onClick.RemoveListener(Close);
            _closeBound = false;
        }

        private void ClearAction()
        {
            if (_action != null && _actionButton != null)
            {
                _actionButton.onClick.RemoveListener(_action);
            }

            _action = null;
        }

        private void SetContentMode(bool settings)
        {
            if (_settingsContent != null)
            {
                _settingsContent.SetActive(settings);
                SetChildButtonsActive(_settingsContent, settings);
            }

            if (_recoveryContent != null)
            {
                _recoveryContent.SetActive(!settings);
            }
        }

        private static void SetChildButtonsActive(
            GameObject root,
            bool active)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (var index = 0; index < buttons.Length; index++)
            {
                buttons[index].gameObject.SetActive(active);
            }
        }

        private void SetOpen(bool open)
        {
            if (_sheet == null)
            {
                return;
            }

            _sheet.alpha = open ? 1f : 0f;
            _sheet.interactable = open;
            _sheet.blocksRaycasts = open;
            if (_sheet.gameObject.activeSelf != open)
            {
                _sheet.gameObject.SetActive(open);
            }
        }

        private void OnDestroy()
        {
            ClearAction();
            UnbindClose();
        }
    }
}
