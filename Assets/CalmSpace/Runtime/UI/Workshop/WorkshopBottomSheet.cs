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
            UnbindClose();
            ClearAction();
            _sheet = sheet;
            _closeButton = closeButton;
            _actionButton = actionButton;
            BindClose();
            Close();
        }

        private void Awake()
        {
            BindClose();
        }

        public void Open(Action action = null)
        {
            ClearAction();
            if (action != null && _actionButton != null)
            {
                _action = () => action();
                _actionButton.onClick.AddListener(_action);
                _actionButton.gameObject.SetActive(true);
                _actionButton.interactable = true;
            }
            else if (_actionButton != null)
            {
                _actionButton.gameObject.SetActive(false);
                _actionButton.interactable = false;
            }

            SetOpen(true);
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
