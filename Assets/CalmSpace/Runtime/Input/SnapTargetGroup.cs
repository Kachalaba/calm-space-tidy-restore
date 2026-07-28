using System;
using CalmSpace.Core;
using UnityEngine;

namespace CalmSpace.Input
{
    /// <summary>
    /// Stable semantic category shared by authored item shapes and sorting
    /// slots. Numeric values intentionally mirror the editor ShapeKind values.
    /// </summary>
    public enum SnapCategory
    {
        Block = 0,
        Pebble = 1,
        Capsule = 2,
        Cylinder = 3
    }

    /// <summary>
    /// Token-bound reservation returned by a SnapTargetGroup. The token keeps
    /// a stale cancelled drag from releasing a newer reservation belonging to
    /// the same item.
    /// </summary>
    public readonly struct SnapSlotReservation :
        IEquatable<SnapSlotReservation>
    {
        private readonly SnapSlot _slot;
        private readonly int _itemInstanceId;
        private readonly int _reservationToken;

        internal SnapSlotReservation(
            SnapSlot slot,
            int itemInstanceId,
            int reservationToken)
        {
            _slot = slot;
            _itemInstanceId = itemInstanceId;
            _reservationToken = reservationToken;
        }

        public bool IsValid =>
            _slot != null &&
            _itemInstanceId != 0 &&
            _reservationToken != 0;

        public SnapSlot Slot => _slot;

        public int ItemInstanceId => _itemInstanceId;

        public int ReservationToken => _reservationToken;

        public SnapPose TargetPose =>
            _slot == null ? default : _slot.TargetPose;

        public bool Release()
        {
            return
                _slot != null &&
                _slot.Release(
                    _itemInstanceId,
                    _reservationToken);
        }

        public bool Equals(SnapSlotReservation other)
        {
            return
                ReferenceEquals(_slot, other._slot) &&
                _itemInstanceId == other._itemInstanceId &&
                _reservationToken == other._reservationToken;
        }

        public override bool Equals(object obj)
        {
            return
                obj is SnapSlotReservation other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode =
                    _slot != null ? _slot.GetHashCode() : 0;
                hashCode =
                    (hashCode * 397) ^ _itemInstanceId;
                hashCode =
                    (hashCode * 397) ^ _reservationToken;
                return hashCode;
            }
        }

        public static bool operator ==(
            SnapSlotReservation left,
            SnapSlotReservation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            SnapSlotReservation left,
            SnapSlotReservation right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Resolves the nearest compatible unoccupied slot from an explicitly
    /// authored array and reserves it atomically before snap animation begins.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SnapTargetGroup : MonoBehaviour
    {
        [SerializeField]
        private SnapSlot[] _slots = Array.Empty<SnapSlot>();

        public int SlotCount => _slots?.Length ?? 0;

        public bool TryReserveNearestCompatible(
            SnapCategory category,
            int itemInstanceId,
            int reservationToken,
            Vector3 itemPosition,
            float positionThreshold,
            out SnapSlotReservation reservation)
        {
            reservation = default;
            if (_slots == null ||
                _slots.Length == 0 ||
                itemInstanceId == 0 ||
                reservationToken == 0 ||
                positionThreshold < 0f ||
                float.IsNaN(positionThreshold) ||
                float.IsInfinity(positionThreshold))
            {
                return false;
            }

            var maximumDistanceSquared =
                positionThreshold * positionThreshold;
            SnapSlot nearest = null;
            var nearestDistanceSquared = float.PositiveInfinity;

            for (var index = 0; index < _slots.Length; index++)
            {
                SnapSlot slot = _slots[index];
                if (slot == null ||
                    !slot.CanReserve(
                        category,
                        itemInstanceId,
                        reservationToken))
                {
                    continue;
                }

                Vector3 offset =
                    slot.TargetPose.Position - itemPosition;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared ||
                    distanceSquared >= nearestDistanceSquared)
                {
                    continue;
                }

                nearest = slot;
                nearestDistanceSquared = distanceSquared;
            }

            return
                nearest != null &&
                nearest.TryReserve(
                    category,
                    itemInstanceId,
                    reservationToken,
                    out reservation);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_slots == null)
            {
                _slots = Array.Empty<SnapSlot>();
            }

            for (var first = 0; first < _slots.Length; first++)
            {
                SnapSlot firstSlot = _slots[first];
                if (firstSlot == null)
                {
                    continue;
                }

                for (var second = first + 1;
                     second < _slots.Length;
                     second++)
                {
                    if (ReferenceEquals(
                            firstSlot,
                            _slots[second]))
                    {
                        Debug.LogError(
                            "A SnapTargetGroup cannot contain the same " +
                            "SnapSlot more than once.",
                            this);
                    }
                }
            }
        }
#endif
    }
}
