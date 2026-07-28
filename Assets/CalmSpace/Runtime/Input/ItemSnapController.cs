using System;
using System.Threading;
using CalmSpace.Audio;
using CalmSpace.Core;
using CalmSpace.Haptics;
using CalmSpace.Levels;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace CalmSpace.Input
{
    /// <summary>
    /// Owns one item's plane-constrained drag and snap/return presentation.
    /// </summary>
    public sealed class ItemSnapController : MonoBehaviour
    {
        private const float MinimumPlaneNormalSquared = 0.000001f;
        private const float MinimumMovementSquared = 0.00000001f;

        [SerializeField]
        private Transform _snapTarget;

        [SerializeField]
        private SnapTargetGroup _snapTargetGroup;

        [SerializeField]
        private SnapCategory _snapCategory;

        [SerializeField]
        private Transform _dragPlaneReference;

        [SerializeField]
        private Vector3 _dragPlaneNormal = Vector3.up;

        [SerializeField]
        [Min(0f)]
        private float _positionSnapThreshold = 0.1f;

        [SerializeField]
        [Range(0f, 180f)]
        private float _rotationSnapThresholdDegrees = 10f;

        [SerializeField]
        [Min(0f)]
        private float _snapDurationSeconds = 0.18f;

        [SerializeField]
        [Min(0f)]
        private float _returnDurationSeconds = 0.12f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _dragHapticIntensity = 0.5f;

        private IHapticService _hapticService;
        private IAsmrAudioService _audioService;
        private IPresentationActivityCoordinator _activityCoordinator;
        private IDisposable _dragLease;
        private Plane _dragPlane;
        private Vector3 _pointerOffset;
        private SnapPose _dragStartPose;
        private SnapSlotReservation _activeSnapReservation;
        private int _itemInstanceId;
        private int _operationGeneration;
        private bool _isDragging;
        private bool _isAnimating;
        private bool _isPlaced;

        public event Action<ItemSnapController> Placed;

        public int ItemInstanceId
        {
            get
            {
                if (_itemInstanceId == 0)
                {
                    _itemInstanceId = GetInstanceID();
                }

                return _itemInstanceId;
            }
        }

        public bool IsDragging => _isDragging;

        public bool IsPlaced => _isPlaced;

        public SnapPose TargetPose
        {
            get
            {
                if (_activeSnapReservation.IsValid)
                {
                    return _activeSnapReservation.TargetPose;
                }

                return _snapTarget == null
                    ? new SnapPose(transform.position, transform.rotation)
                    : new SnapPose(
                        _snapTarget.position,
                        _snapTarget.rotation);
            }
        }

        private void Awake()
        {
            _itemInstanceId = GetInstanceID();
        }

        [Inject]
        public void Construct(
            IHapticService hapticService,
            IAsmrAudioService audioService,
            IPresentationActivityCoordinator activityCoordinator)
        {
            _hapticService = hapticService ??
                throw new ArgumentNullException(nameof(hapticService));
            _audioService = audioService ??
                throw new ArgumentNullException(nameof(audioService));
            _activityCoordinator = activityCoordinator ??
                throw new ArgumentNullException(
                    nameof(activityCoordinator));
        }

        public bool TryBeginDrag(Ray pointerRay)
        {
            if (!isActiveAndEnabled ||
                _isPlaced ||
                _isDragging ||
                _isAnimating ||
                (_snapTarget == null && _snapTargetGroup == null) ||
                _activityCoordinator == null)
            {
                return false;
            }

            if (!TryBuildDragPlane(out _dragPlane) ||
                !_dragPlane.Raycast(pointerRay, out var enter))
            {
                return false;
            }

            if (!_activityCoordinator.TryEnterDrag(out var lease) ||
                lease == null)
            {
                lease?.Dispose();
                return false;
            }

            _dragLease = lease;
            _dragStartPose = new SnapPose(
                transform.position,
                transform.rotation);
            _pointerOffset =
                transform.position - pointerRay.GetPoint(enter);
            _isDragging = true;
            _operationGeneration++;
            return true;
        }

        public bool Drag(Ray pointerRay)
        {
            if (!_isDragging ||
                !_dragPlane.Raycast(pointerRay, out var enter))
            {
                return false;
            }

            var nextPosition =
                pointerRay.GetPoint(enter) + _pointerOffset;
            var movement = nextPosition - transform.position;
            if (movement.sqrMagnitude <= MinimumMovementSquared)
            {
                return true;
            }

            transform.position = nextPosition;
            _hapticService?.PlayDragTick(_dragHapticIntensity);
            return true;
        }

        public async UniTask<bool> EndDragAsync(
            Ray pointerRay,
            CancellationToken cancellationToken = default)
        {
            if (!_isDragging)
            {
                return false;
            }

            Drag(pointerRay);
            _isDragging = false;
            _isAnimating = true;
            var endingLease = _dragLease;

            var generation = ++_operationGeneration;
            var currentPose = new SnapPose(
                transform.position,
                transform.rotation);
            var pendingReservation = default(SnapSlotReservation);
            var targetPose = _dragStartPose;
            var shouldSnap = TryResolveSnapDestination(
                currentPose,
                generation,
                out targetPose,
                out pendingReservation);
            var destination = shouldSnap
                ? targetPose
                : _dragStartPose;
            var duration = shouldSnap
                ? _snapDurationSeconds
                : _returnDurationSeconds;
            var placementSucceeded = false;

            try
            {
                var animationCompleted = await InterpolatePoseAsync(
                    currentPose,
                    destination,
                    duration,
                    generation,
                    cancellationToken);

                if (!animationCompleted ||
                    generation != _operationGeneration)
                {
                    return false;
                }

                if (!shouldSnap)
                {
                    return false;
                }

                _isPlaced = true;
                placementSucceeded = true;
                ReleaseDragLease(endingLease);
                PlayPlacementFeedback();
                InvokePlaced();
                return true;
            }
            catch (OperationCanceledException)
            {
                if (generation == _operationGeneration && !_isPlaced)
                {
                    transform.SetPositionAndRotation(
                        _dragStartPose.Position,
                        _dragStartPose.Rotation);
                }

                return false;
            }
            finally
            {
                if (!placementSucceeded)
                {
                    ReleaseSnapReservation(pendingReservation);
                }

                if (generation == _operationGeneration)
                {
                    _isAnimating = false;
                }

                ReleaseDragLease(endingLease);
            }
        }

        public void CancelDrag()
        {
            if (!_isDragging && !_isAnimating && _dragLease == null)
            {
                if (!_isPlaced)
                {
                    ReleaseActiveSnapReservation();
                }

                return;
            }

            _operationGeneration++;
            _isDragging = false;
            _isAnimating = false;

            if (!_isPlaced)
            {
                transform.SetPositionAndRotation(
                    _dragStartPose.Position,
                    _dragStartPose.Rotation);
                ReleaseActiveSnapReservation();
            }

            _hapticService?.Cancel();
            ReleaseDragLease();
        }

        private void OnDisable()
        {
            CancelDrag();
        }

        private void OnDestroy()
        {
            CancelDrag();
            ReleaseActiveSnapReservation();
        }

        private bool TryResolveSnapDestination(
            SnapPose currentPose,
            int reservationToken,
            out SnapPose targetPose,
            out SnapSlotReservation reservation)
        {
            reservation = default;
            targetPose = _dragStartPose;

            if (_snapTargetGroup != null)
            {
                if (!_snapTargetGroup.TryReserveNearestCompatible(
                        _snapCategory,
                        ItemInstanceId,
                        reservationToken,
                        currentPose.Position,
                        _positionSnapThreshold,
                        out reservation))
                {
                    return false;
                }

                _activeSnapReservation = reservation;
                targetPose = reservation.TargetPose;
                return true;
            }

            if (_snapTarget == null)
            {
                return false;
            }

            targetPose = new SnapPose(
                _snapTarget.position,
                _snapTarget.rotation);
            return SnapMath.IsPoseWithinThreshold(
                currentPose.Position,
                currentPose.Rotation,
                targetPose,
                _positionSnapThreshold,
                _rotationSnapThresholdDegrees);
        }

        private void ReleaseSnapReservation(
            SnapSlotReservation reservation)
        {
            if (!reservation.IsValid)
            {
                return;
            }

            if (_activeSnapReservation == reservation)
            {
                _activeSnapReservation = default;
            }

            reservation.Release();
        }

        private void ReleaseActiveSnapReservation()
        {
            var reservation = _activeSnapReservation;
            _activeSnapReservation = default;
            reservation.Release();
        }

        private async UniTask<bool> InterpolatePoseAsync(
            SnapPose start,
            SnapPose destination,
            float durationSeconds,
            int generation,
            CancellationToken cancellationToken)
        {
            if (durationSeconds <= 0f)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (generation != _operationGeneration)
                {
                    return false;
                }

                transform.SetPositionAndRotation(
                    destination.Position,
                    destination.Rotation);
                return true;
            }

            var destroyCancellation =
                this.GetCancellationTokenOnDestroy();
            var elapsed = 0f;

            while (elapsed < durationSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                destroyCancellation.ThrowIfCancellationRequested();
                if (generation != _operationGeneration)
                {
                    return false;
                }

                elapsed += Time.unscaledDeltaTime;
                var linear = Mathf.Clamp01(
                    elapsed / durationSeconds);
                var eased = linear * linear * (3f - 2f * linear);

                transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(
                        start.Position,
                        destination.Position,
                        eased),
                    Quaternion.SlerpUnclamped(
                        start.Rotation,
                        destination.Rotation,
                        eased));

                if (linear >= 1f)
                {
                    break;
                }

                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    destroyCancellation);
            }

            return generation == _operationGeneration;
        }

        private bool TryBuildDragPlane(out Plane plane)
        {
            var normal = _dragPlaneReference == null
                ? _dragPlaneNormal
                : _dragPlaneReference.TransformDirection(
                    _dragPlaneNormal);
            if (normal.sqrMagnitude < MinimumPlaneNormalSquared)
            {
                plane = default;
                return false;
            }

            var point = _dragPlaneReference == null
                ? transform.position
                : _dragPlaneReference.position;
            plane = new Plane(normal.normalized, point);
            return true;
        }

        private void PlayPlacementFeedback()
        {
            try
            {
                _hapticService?.PlaySnap();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            try
            {
                _audioService?.PlaySnap(transform.position);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void InvokePlaced()
        {
            var handler = Placed;
            if (handler == null)
            {
                return;
            }

            foreach (Action<ItemSnapController> subscriber in
                     handler.GetInvocationList())
            {
                try
                {
                    subscriber(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void ReleaseDragLease()
        {
            var lease = _dragLease;
            _dragLease = null;
            lease?.Dispose();
        }

        private void ReleaseDragLease(IDisposable lease)
        {
            if (lease == null)
            {
                return;
            }

            if (ReferenceEquals(_dragLease, lease))
            {
                _dragLease = null;
            }

            // Activity leases are idempotent. Disposing the captured lease
            // again is safe, while a newer drag lease remains untouched.
            lease.Dispose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _positionSnapThreshold =
                Mathf.Max(0f, _positionSnapThreshold);
            _rotationSnapThresholdDegrees =
                Mathf.Clamp(_rotationSnapThresholdDegrees, 0f, 180f);
            _snapDurationSeconds =
                Mathf.Max(0f, _snapDurationSeconds);
            _returnDurationSeconds =
                Mathf.Max(0f, _returnDurationSeconds);
        }
#endif
    }
}
