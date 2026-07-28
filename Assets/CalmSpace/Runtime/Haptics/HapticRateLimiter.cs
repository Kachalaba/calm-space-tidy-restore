using System;

namespace CalmSpace.Haptics
{
    /// <summary>
    /// Keeps short drag vibrations separated and reserves a quiet window after
    /// a stronger snap vibration.
    /// </summary>
    public sealed class HapticRateLimiter
    {
        private readonly double _dragTickIntervalSeconds;
        private readonly double _postSnapSilenceSeconds;
        private double _nextDragTickSeconds = double.NegativeInfinity;

        public HapticRateLimiter(
            double dragTickIntervalSeconds,
            double postSnapSilenceSeconds)
        {
            if (!IsFinitePositive(dragTickIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dragTickIntervalSeconds));
            }

            if (!IsFinitePositive(postSnapSilenceSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(postSnapSilenceSeconds));
            }

            _dragTickIntervalSeconds = dragTickIntervalSeconds;
            _postSnapSilenceSeconds = postSnapSilenceSeconds;
        }

        public bool TryConsumeDragTick(double nowSeconds)
        {
            if (!IsFinite(nowSeconds) || nowSeconds < _nextDragTickSeconds)
            {
                return false;
            }

            _nextDragTickSeconds = nowSeconds + _dragTickIntervalSeconds;
            return true;
        }

        public void RecordSnap(double nowSeconds)
        {
            if (!IsFinite(nowSeconds))
            {
                return;
            }

            var nextAfterSnap = nowSeconds + _postSnapSilenceSeconds;
            if (nextAfterSnap > _nextDragTickSeconds)
            {
                _nextDragTickSeconds = nextAfterSnap;
            }
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0d && IsFinite(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
