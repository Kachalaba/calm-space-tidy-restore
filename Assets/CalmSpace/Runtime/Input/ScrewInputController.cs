using System;
using CalmSpace.Core;
using CalmSpace.Fasteners;
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
    /// New Input System adapter for hold-to-unscrew. A hold owns the same drag
    /// activity lease as a moving item, so a fullscreen ad cannot land while a
    /// finger is still turning a screw.
    ///
    /// Once a screw is grabbed the finger is free to drift off it — the hold
    /// keeps counting until the pointer is lifted. Each hold becomes one
    /// compact undo step, so partial turns and removals are both reversible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScrewInputController : MonoBehaviour
    {
        private const int NoTouchId = -1;

        [SerializeField]
        private Camera _raycastCamera;

        [SerializeField]
        private LayerMask _screwLayers = ~0;

        [SerializeField]
        [Min(0.01f)]
        private float _maximumRaycastDistance = 100f;

        [SerializeField]
        private QueryTriggerInteraction _queryTriggerInteraction =
            QueryTriggerInteraction.Ignore;

        private IPresentationActivityCoordinator _activityCoordinator;
        private IUndoHistory _undoHistory;
        private IDisposable _dragLease;
        private ScrewController _heldScrew;
        private int _activeTouchId = NoTouchId;
        private bool _mouseOwnsHold;
        private bool _ownsEnhancedTouchSupport;
        private ScrewUndoState _holdStartState;
        private bool _hasHoldStartState;

        public ScrewController HeldScrew => _heldScrew;

        public bool HasActiveHold => _dragLease != null;

        [Inject]
        public void Construct(
            IPresentationActivityCoordinator activityCoordinator,
            IUndoHistory undoHistory)
        {
            _activityCoordinator = activityCoordinator ??
                throw new ArgumentNullException(
                    nameof(activityCoordinator));
            _undoHistory = undoHistory ??
                throw new ArgumentNullException(nameof(undoHistory));
        }

        private void OnEnable()
        {
            ResolveCameraIfNeeded();
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

            if (_mouseOwnsHold)
            {
                ProcessOwnedMouse();
                return;
            }

            if (TryBeginTouchHold())
            {
                return;
            }

            TryBeginMouseHold();
        }

        private void OnDisable()
        {
            EndHold();

            if (_ownsEnhancedTouchSupport &&
                EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Disable();
            }

            _ownsEnhancedTouchSupport = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                EndHold();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                EndHold();
            }
        }

        private bool TryBeginTouchHold()
        {
            var touches = EnhancedTouch.activeTouches;
            for (var index = 0; index < touches.Count; index++)
            {
                var touch = touches[index];
                if (touch.phase != InputTouchPhase.Began)
                {
                    continue;
                }

                if (!TryBeginHold(touch.screenPosition))
                {
                    continue;
                }

                _activeTouchId = touch.touchId;
                _mouseOwnsHold = false;
                return true;
            }

            return false;
        }

        private bool TryBeginMouseHold()
        {
            if (EnhancedTouch.activeTouches.Count > 0)
            {
                return false;
            }

            var mouse = Mouse.current;
            if (mouse == null ||
                !mouse.leftButton.wasPressedThisFrame ||
                !TryBeginHold(mouse.position.ReadValue()))
            {
                return false;
            }

            _mouseOwnsHold = true;
            return true;
        }

        private bool TryBeginHold(Vector2 screenPosition)
        {
            ResolveCameraIfNeeded();

            if (_raycastCamera == null ||
                _activityCoordinator == null ||
                UiPointerBlocker.IsBlocked(screenPosition))
            {
                return false;
            }

            Ray pointerRay =
                _raycastCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(
                    pointerRay,
                    out RaycastHit hit,
                    _maximumRaycastDistance,
                    _screwLayers,
                    _queryTriggerInteraction))
            {
                return false;
            }

            ScrewController screw =
                hit.collider.GetComponentInParent<ScrewController>();
            if (screw == null || screw.IsRemoved)
            {
                return false;
            }

            IDisposable lease = null;
            if (!PointerInteractionOwnership.TryAcquire(this) ||
                !_activityCoordinator.TryEnterDrag(out lease) ||
                lease == null)
            {
                lease?.Dispose();
                PointerInteractionOwnership.Release(this);
                return false;
            }

            ScrewUndoState startState =
                screw.CaptureUndoState();
            if (!screw.BeginHold())
            {
                lease.Dispose();
                PointerInteractionOwnership.Release(this);
                return false;
            }

            _dragLease = lease;
            _heldScrew = screw;
            _holdStartState = startState;
            _hasHoldStartState = true;
            return true;
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

                if (touch.phase == InputTouchPhase.Canceled ||
                    touch.phase == InputTouchPhase.Ended)
                {
                    EndHold();
                }
                else
                {
                    ContinueHold();
                }

                return;
            }

            EndHold();
        }

        private void ProcessOwnedMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null ||
                mouse.leftButton.wasReleasedThisFrame ||
                !mouse.leftButton.isPressed)
            {
                EndHold();
                return;
            }

            ContinueHold();
        }

        private void ContinueHold()
        {
            if (_heldScrew == null)
            {
                EndHold();
                return;
            }

            _heldScrew.ContinueHold(Time.deltaTime);

            // The screw released itself the moment it came out; drop the
            // pointer so the next tap can start on a different one.
            if (_heldScrew.IsRemoved)
            {
                EndHold();
            }
        }

        private void EndHold()
        {
            if (_heldScrew != null)
            {
                ScrewController screw = _heldScrew;
                screw.EndHold();
                if (_hasHoldStartState &&
                    screw.CaptureUndoState() != _holdStartState)
                {
                    _undoHistory?.Push(
                        new ScrewHoldUndoCommand(
                            screw,
                            _holdStartState));
                }

                _heldScrew = null;
            }

            _hasHoldStartState = false;
            ResetPointerState();
        }

        private void ResetPointerState()
        {
            _activeTouchId = NoTouchId;
            _mouseOwnsHold = false;
            PointerInteractionOwnership.Release(this);

            var lease = _dragLease;
            _dragLease = null;
            lease?.Dispose();
        }

        private void ResolveCameraIfNeeded()
        {
            if (_raycastCamera == null)
            {
                _raycastCamera = Camera.main;
            }
        }

        private sealed class ScrewHoldUndoCommand :
            IUndoCommand
        {
            private readonly ScrewController _screw;
            private readonly ScrewUndoState _state;

            public ScrewHoldUndoCommand(
                ScrewController screw,
                ScrewUndoState state)
            {
                _screw = screw;
                _state = state;
            }

            public bool CanUndo =>
                _screw != null &&
                _screw.CaptureUndoState() != _state;

            public bool TryUndo()
            {
                return
                    _screw != null &&
                    _screw.RestoreUndoState(_state);
            }
        }
    }
}
