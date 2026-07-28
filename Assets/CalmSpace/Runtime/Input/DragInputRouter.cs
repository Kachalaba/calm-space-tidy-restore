using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace CalmSpace.Input
{
    /// <summary>
    /// Single owner for touch/mouse picking, drag forwarding, and two-finger
    /// pinch measurement.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class DragInputRouter : MonoBehaviour
    {
        private const int NoTouchId = -1;

        private static DragInputRouter _activeRouter;

        [SerializeField]
        private Camera _camera;

        [SerializeField]
        private LayerMask _draggableLayers = ~0;

        [SerializeField]
        [Min(0.01f)]
        private float _maximumRaycastDistance = 100f;

        [SerializeField]
        private QueryTriggerInteraction _queryTriggerInteraction =
            QueryTriggerInteraction.Ignore;

        private ItemSnapController _ownedItem;
        private int _activeTouchId = NoTouchId;
        private bool _mouseOwnsDrag;
        private bool _ownsEnhancedTouchSupport;
        private bool _pinchTracking;
        private int _firstPinchTouchId = NoTouchId;
        private int _secondPinchTouchId = NoTouchId;
        private float _previousPinchDistance;

        public float PinchDelta { get; private set; }

        public ItemSnapController OwnedItem => _ownedItem;

        public bool HasDragOwnership => _ownedItem != null;

        private void OnEnable()
        {
            if (_activeRouter != null && _activeRouter != this)
            {
                Debug.LogError(
                    "Only one DragInputRouter may be enabled at a time.",
                    this);
                enabled = false;
                return;
            }

            _activeRouter = this;
            EnhancedTouchSupport.Enable();
            _ownsEnhancedTouchSupport = true;

            ResetPointerState();
        }

        private void Update()
        {
            PinchDelta = 0f;
            UpdatePinch();

            if (_activeTouchId != NoTouchId)
            {
                ProcessOwnedTouch();
                return;
            }

            if (_mouseOwnsDrag)
            {
                ProcessOwnedMouse();
                return;
            }

            if (TryBeginTouchDrag())
            {
                return;
            }

            TryBeginMouseDrag();
        }

        private void OnDisable()
        {
            CancelOwnedDrag();
            ResetPinch();

            if (_activeRouter == this)
            {
                _activeRouter = null;
                if (_ownsEnhancedTouchSupport &&
                    EnhancedTouchSupport.enabled)
                {
                    EnhancedTouchSupport.Disable();
                }
            }

            _ownsEnhancedTouchSupport = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelOwnedDrag();
                ResetPinch();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                CancelOwnedDrag();
                ResetPinch();
            }
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

                var phase = touch.phase;
                if (phase == InputTouchPhase.Canceled)
                {
                    CancelOwnedDrag();
                }
                else if (phase == InputTouchPhase.Ended)
                {
                    EndOwnedDrag(touch.screenPosition);
                }
                else if (phase == InputTouchPhase.Moved)
                {
                    ForwardDrag(touch.screenPosition);
                }

                return;
            }

            CancelOwnedDrag();
        }

        private void ProcessOwnedMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                CancelOwnedDrag();
                return;
            }

            var screenPosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                EndOwnedDrag(screenPosition);
            }
            else if (mouse.leftButton.isPressed)
            {
                ForwardDrag(screenPosition);
            }
            else
            {
                CancelOwnedDrag();
            }
        }

        private bool TryBeginTouchDrag()
        {
            var touches = EnhancedTouch.activeTouches;
            for (var index = 0; index < touches.Count; index++)
            {
                var touch = touches[index];
                if (touch.phase != InputTouchPhase.Began)
                {
                    continue;
                }

                if (TryAcquireItem(touch.screenPosition))
                {
                    _activeTouchId = touch.touchId;
                    _mouseOwnsDrag = false;
                    return true;
                }
            }

            return false;
        }

        private bool TryBeginMouseDrag()
        {
            if (EnhancedTouch.activeTouches.Count > 0)
            {
                return false;
            }

            var mouse = Mouse.current;
            if (mouse == null ||
                !mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            if (!TryAcquireItem(mouse.position.ReadValue()))
            {
                return false;
            }

            _activeTouchId = NoTouchId;
            _mouseOwnsDrag = true;
            return true;
        }

        private bool TryAcquireItem(Vector2 screenPosition)
        {
            if (_camera == null ||
                UiPointerBlocker.IsBlocked(screenPosition))
            {
                return false;
            }

            var pointerRay = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(
                    pointerRay,
                    out var hit,
                    _maximumRaycastDistance,
                    _draggableLayers,
                    _queryTriggerInteraction))
            {
                return false;
            }

            var item =
                hit.collider.GetComponentInParent<ItemSnapController>();
            if (item == null ||
                !PointerInteractionOwnership.TryAcquire(this))
            {
                return false;
            }

            if (!item.TryBeginDrag(pointerRay))
            {
                PointerInteractionOwnership.Release(this);
                return false;
            }

            _ownedItem = item;
            return true;
        }

        private void ForwardDrag(Vector2 screenPosition)
        {
            if (_ownedItem == null || _camera == null)
            {
                CancelOwnedDrag();
                return;
            }

            _ownedItem.Drag(
                _camera.ScreenPointToRay(screenPosition));
        }

        private void EndOwnedDrag(Vector2 screenPosition)
        {
            var item = _ownedItem;
            var cameraReference = _camera;
            ClearOwnership();

            if (item == null || cameraReference == null)
            {
                item?.CancelDrag();
                return;
            }

            item.EndDragAsync(
                    cameraReference.ScreenPointToRay(screenPosition))
                .Forget();
        }

        private void CancelOwnedDrag()
        {
            var item = _ownedItem;
            ClearOwnership();
            item?.CancelDrag();
        }

        private void ClearOwnership()
        {
            _ownedItem = null;
            _activeTouchId = NoTouchId;
            _mouseOwnsDrag = false;
            PointerInteractionOwnership.Release(this);
        }

        private void UpdatePinch()
        {
            var touches = EnhancedTouch.activeTouches;
            var firstIndex = -1;
            var secondIndex = -1;

            for (var index = 0; index < touches.Count; index++)
            {
                var phase = touches[index].phase;
                if (phase == InputTouchPhase.Canceled ||
                    phase == InputTouchPhase.Ended)
                {
                    continue;
                }

                if (firstIndex < 0)
                {
                    firstIndex = index;
                }
                else
                {
                    secondIndex = index;
                    break;
                }
            }

            if (secondIndex < 0)
            {
                ResetPinch();
                return;
            }

            var first = touches[firstIndex];
            var second = touches[secondIndex];
            var firstId = Mathf.Min(first.touchId, second.touchId);
            var secondId = Mathf.Max(first.touchId, second.touchId);
            var distance = Vector2.Distance(
                first.screenPosition,
                second.screenPosition);

            if (_pinchTracking &&
                _firstPinchTouchId == firstId &&
                _secondPinchTouchId == secondId)
            {
                PinchDelta = distance - _previousPinchDistance;
            }

            _pinchTracking = true;
            _firstPinchTouchId = firstId;
            _secondPinchTouchId = secondId;
            _previousPinchDistance = distance;
        }

        private void ResetPointerState()
        {
            PointerInteractionOwnership.Release(this);
            _ownedItem = null;
            _activeTouchId = NoTouchId;
            _mouseOwnsDrag = false;
            ResetPinch();
        }

        private void ResetPinch()
        {
            PinchDelta = 0f;
            _pinchTracking = false;
            _firstPinchTouchId = NoTouchId;
            _secondPinchTouchId = NoTouchId;
            _previousPinchDistance = 0f;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeRouter = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _maximumRaycastDistance =
                Mathf.Max(0.01f, _maximumRaycastDistance);
        }
#endif
    }
}
