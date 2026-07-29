using System.Threading;
using System;
using CalmSpace.Cleaning;
using CalmSpace.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch =
    UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;
using VContainer;

namespace CalmSpace.Input
{
    /// <summary>
    /// New Input System adapter for cleaning strokes. A cleaning stroke owns
    /// the same drag activity lease as movable items, so fullscreen ads cannot
    /// race a finger that is still touching the surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CleaningInputController : MonoBehaviour
    {
        private const int NoTouchId = -1;

        [SerializeField]
        private RenderTextureCleaner _cleaner;

        private IPresentationActivityCoordinator _activityCoordinator;
        private IDisposable _dragLease;
        private int _activeTouchId = NoTouchId;
        private bool _mouseOwnsStroke;
        private bool _ownsEnhancedTouchSupport;
        private bool _evaluationInFlight;
        private bool _evaluationPending;
        private CancellationTokenSource _enableCancellation;
        private int _enableGeneration;

        public bool HasActiveStroke => _dragLease != null;

        public RenderTextureCleaner Cleaner => _cleaner;

        [Inject]
        public void Construct(
            IPresentationActivityCoordinator activityCoordinator)
        {
            _activityCoordinator = activityCoordinator ??
                throw new ArgumentNullException(
                    nameof(activityCoordinator));
        }

        /// <summary>
        /// Points the controller at the surface the level is currently on.
        /// Any stroke in flight is dropped rather than carried across, so a
        /// finger already down cannot paint the newly revealed layer.
        /// </summary>
        public void SetActiveCleaner(RenderTextureCleaner cleaner)
        {
            if (_cleaner == cleaner)
            {
                return;
            }

            CancelStroke();
            _cleaner = cleaner;
        }

        private void OnEnable()
        {
            _enableGeneration++;
            _enableCancellation =
                new CancellationTokenSource();
            EnhancedTouchSupport.Enable();
            _ownsEnhancedTouchSupport = true;

            ResetPointerState();
        }

        private void Update()
        {
            if (_activeTouchId != NoTouchId)
            {
                ProcessOwnedTouch();
                return;
            }

            if (_mouseOwnsStroke)
            {
                ProcessOwnedMouse();
                return;
            }

            if (TryBeginTouchStroke())
            {
                return;
            }

            TryBeginMouseStroke();
        }

        private void OnDisable()
        {
            _enableGeneration++;
            _enableCancellation?.Cancel();
            CancelStroke();

            if (_ownsEnhancedTouchSupport &&
                EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Disable();
            }

            _ownsEnhancedTouchSupport = false;
            _enableCancellation?.Dispose();
            _enableCancellation = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelStroke();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                CancelStroke();
            }
        }

        private bool TryBeginTouchStroke()
        {
            var touches = EnhancedTouch.activeTouches;
            for (var index = 0; index < touches.Count; index++)
            {
                var touch = touches[index];
                if (touch.phase != InputTouchPhase.Began)
                {
                    continue;
                }

                if (!TryBeginStroke(touch.screenPosition))
                {
                    continue;
                }

                _activeTouchId = touch.touchId;
                _mouseOwnsStroke = false;
                return true;
            }

            return false;
        }

        private bool TryBeginMouseStroke()
        {
            if (EnhancedTouch.activeTouches.Count > 0)
            {
                return false;
            }

            var mouse = Mouse.current;
            if (mouse == null ||
                !mouse.leftButton.wasPressedThisFrame ||
                !TryBeginStroke(mouse.position.ReadValue()))
            {
                return false;
            }

            _mouseOwnsStroke = true;
            return true;
        }

        private bool TryBeginStroke(Vector2 screenPosition)
        {
            IDisposable lease = null;

            if (_cleaner == null ||
                _activityCoordinator == null ||
                UiPointerBlocker.IsBlocked(screenPosition) ||
                !PointerInteractionOwnership.TryAcquire(this) ||
                !_activityCoordinator.TryEnterDrag(out lease) ||
                lease == null)
            {
                lease?.Dispose();
                PointerInteractionOwnership.Release(this);
                return false;
            }

            _dragLease = lease;
            _cleaner.BeginStroke();

            if (_cleaner.PaintFromScreenPoint(screenPosition))
            {
                RequestProgressEvaluation();
                return true;
            }

            CancelStroke();
            return false;
        }

        private void ProcessOwnedTouch()
        {
            var touches = EnhancedTouch.activeTouches;
            for (var index = 0; index < touches.Count; index++)
            {
                var touch = touches[index];
                if (touch.touchId != _activeTouchId)
                {
                    continue;
                }

                if (touch.phase == InputTouchPhase.Canceled)
                {
                    CancelStroke();
                }
                else if (touch.phase == InputTouchPhase.Ended)
                {
                    Paint(touch.screenPosition);
                    EndStroke();
                }
                else if (touch.phase == InputTouchPhase.Moved)
                {
                    Paint(touch.screenPosition);
                }

                return;
            }

            CancelStroke();
        }

        private void ProcessOwnedMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                CancelStroke();
                return;
            }

            var screenPosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                Paint(screenPosition);
                EndStroke();
            }
            else if (mouse.leftButton.isPressed)
            {
                Paint(screenPosition);
            }
            else
            {
                CancelStroke();
            }
        }

        private void Paint(Vector2 screenPosition)
        {
            if (_cleaner != null &&
                _cleaner.PaintFromScreenPoint(screenPosition))
            {
                RequestProgressEvaluation();
            }
        }

        private void EndStroke()
        {
            _cleaner?.EndStroke();
            RequestProgressEvaluation();
            ReleaseOwnership();
        }

        private void CancelStroke()
        {
            _cleaner?.EndStroke();
            ReleaseOwnership();
        }

        private void ReleaseOwnership()
        {
            _activeTouchId = NoTouchId;
            _mouseOwnsStroke = false;
            PointerInteractionOwnership.Release(this);

            var lease = _dragLease;
            _dragLease = null;
            lease?.Dispose();
        }

        private void ResetPointerState()
        {
            PointerInteractionOwnership.Release(this);
            _activeTouchId = NoTouchId;
            _mouseOwnsStroke = false;
            _dragLease = null;
            _evaluationInFlight = false;
            _evaluationPending = false;
        }

        private void RequestProgressEvaluation()
        {
            if (_cleaner == null ||
                !isActiveAndEnabled)
            {
                return;
            }

            _evaluationPending = true;
            if (_evaluationInFlight)
            {
                return;
            }

            EvaluateProgressAsync(
                    _enableGeneration,
                    _enableCancellation.Token)
                .Forget();
        }

        private async UniTaskVoid EvaluateProgressAsync(
            int generation,
            CancellationToken cancellationToken)
        {
            _evaluationInFlight = true;

            try
            {
                do
                {
                    _evaluationPending = false;
                    await _cleaner.EvaluateProgressAsync(
                        cancellationToken);
                }
                while (_evaluationPending &&
                       generation == _enableGeneration &&
                       this != null &&
                       isActiveAndEnabled);
            }
            catch (OperationCanceledException)
            {
                // Expected when the Addressable level is unloaded.
            }
            finally
            {
                if (generation == _enableGeneration)
                {
                    _evaluationInFlight = false;

                    if (_evaluationPending &&
                        this != null &&
                        isActiveAndEnabled)
                    {
                        EvaluateProgressAsync(
                                generation,
                                cancellationToken)
                            .Forget();
                    }
                }
            }
        }
    }
}
