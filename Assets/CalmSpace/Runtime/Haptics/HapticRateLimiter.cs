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
        private readonly double _minimumSnapIntervalSeconds;
        private double _nextDragTickSeconds = double.NegativeInfinity;
        private double _nextSnapSeconds = double.NegativeInfinity;

        public HapticRateLimiter(
            double dragTickIntervalSeconds,
            double postSnapSilenceSeconds,
            double minimumSnapIntervalSeconds = 0.08d)
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

            if (!IsFinitePositive(minimumSnapIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumSnapIntervalSeconds));
            }

            _dragTickIntervalSeconds = dragTickIntervalSeconds;
            _postSnapSilenceSeconds = postSnapSilenceSeconds;
            _minimumSnapIntervalSeconds = minimumSnapIntervalSeconds;
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

        /// <summary>
        /// Atomically reserves a snap pulse and its following quiet window.
        /// This prevents repeated placement callbacks from overwhelming the
        /// native haptic engine.
        /// </summary>
        public bool TryConsumeSnap(double nowSeconds)
        {
            if (!IsFinite(nowSeconds) || nowSeconds < _nextSnapSeconds)
            {
                return false;
            }

            _nextSnapSeconds =
                nowSeconds + _minimumSnapIntervalSeconds;
            RecordSnap(nowSeconds);
            return true;
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
