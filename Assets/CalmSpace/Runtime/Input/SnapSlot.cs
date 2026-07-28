using CalmSpace.Core;
using UnityEngine;

namespace CalmSpace.Input
{
    /// <summary>
    /// One authored destination in a soft-sorting group. Occupancy is owned by
    /// exactly one item and one operation token at a time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SnapSlot : MonoBehaviour
    {
        [SerializeField]
        private SnapCategory _category;

        [SerializeField]
        private Transform _snapPose;

        private int _reservedItemId;
        private int _reservationToken;

        public SnapCategory Category => _category;

        public bool IsOccupied => _reservedItemId != 0;

        public int ReservedItemId => _reservedItemId;

        public SnapPose TargetPose
        {
            get
            {
                Transform pose =
                    _snapPose == null ? transform : _snapPose;
                return new SnapPose(
                    pose.position,
                    pose.rotation);
            }
        }

        public bool IsCompatible(SnapCategory category)
        {
            return _category == category;
        }

        public bool CanReserve(
            SnapCategory category,
            int itemInstanceId,
            int reservationToken)
        {
            if (!IsCompatible(category) ||
                itemInstanceId == 0 ||
                reservationToken == 0)
            {
                return false;
            }

            return
                !IsOccupied ||
                (_reservedItemId == itemInstanceId &&
                 _reservationToken == reservationToken);
        }

        public bool TryReserve(
            SnapCategory category,
            int itemInstanceId,
            int reservationToken,
            out SnapSlotReservation reservation)
        {
            reservation = default;
            if (!CanReserve(
                    category,
                    itemInstanceId,
                    reservationToken))
            {
                return false;
            }

            _reservedItemId = itemInstanceId;
            _reservationToken = reservationToken;
            reservation = new SnapSlotReservation(
                this,
                itemInstanceId,
                reservationToken);
            return true;
        }

        public bool Release(
            int itemInstanceId,
            int reservationToken)
        {
            if (!IsOccupied ||
                _reservedItemId != itemInstanceId ||
                _reservationToken != reservationToken)
            {
                return false;
            }

            _reservedItemId = 0;
            _reservationToken = 0;
            return true;
        }
    }
}
