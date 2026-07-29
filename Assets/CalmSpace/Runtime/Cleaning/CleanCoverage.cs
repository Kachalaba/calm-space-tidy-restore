using System;
using Unity.Collections;
using Unity.Jobs;

namespace CalmSpace.Cleaning
{
    /// <summary>
    /// Allocation-free dual clock gate for expensive coverage evaluations.
    /// Both the frame and real-time intervals must elapse, keeping sampling
    /// bounded on high-refresh devices without becoming too eager at 30 FPS.
    /// </summary>
    public sealed class CoverageSampleGate
    {
        private readonly int _minimumFrameInterval;
        private readonly double _minimumTimeIntervalSeconds;
        private long _nextFrame = long.MinValue;
        private double _nextTimeSeconds = double.NegativeInfinity;

        public CoverageSampleGate(
            int minimumFrameInterval,
            double minimumTimeIntervalSeconds)
        {
            if (minimumFrameInterval < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumFrameInterval));
            }

            if (double.IsNaN(minimumTimeIntervalSeconds) ||
                double.IsInfinity(minimumTimeIntervalSeconds) ||
                minimumTimeIntervalSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumTimeIntervalSeconds));
            }

            _minimumFrameInterval = minimumFrameInterval;
            _minimumTimeIntervalSeconds =
                minimumTimeIntervalSeconds;
        }

        public bool TryReserve(
            long frame,
            double timeSeconds)
        {
            if (frame < 0L ||
                double.IsNaN(timeSeconds) ||
                double.IsInfinity(timeSeconds) ||
                frame < _nextFrame ||
                timeSeconds < _nextTimeSeconds)
            {
                return false;
            }

            _nextFrame = frame + _minimumFrameInterval;
            _nextTimeSeconds =
                timeSeconds + _minimumTimeIntervalSeconds;
            return true;
        }

        public void Reset()
        {
            _nextFrame = long.MinValue;
            _nextTimeSeconds = double.NegativeInfinity;
        }
    }

    public struct SumMaskJob : IJob
    {
        [ReadOnly]
        public NativeArray<byte> Pixels;

        public NativeArray<long> Sum;

        public int PixelCount;

        public int ChannelStride;

        public void Execute()
        {
            if (!Sum.IsCreated || Sum.Length == 0)
            {
                return;
            }

            long total = 0L;

            if (Pixels.IsCreated && PixelCount > 0 && ChannelStride > 0)
            {
                int availablePixelCount = Pixels.Length / ChannelStride;
                int count = Math.Min(PixelCount, availablePixelCount);

                for (int pixelIndex = 0; pixelIndex < count; pixelIndex++)
                {
                    total += Pixels[pixelIndex * ChannelStride];
                }
            }

            Sum[0] = total;
        }
    }

    public sealed class CleanCompletionLatch
    {
        private readonly float _threshold;
        private bool _isCompleted;

        public CleanCompletionLatch(float threshold)
        {
            if (float.IsNaN(threshold) || threshold <= 0f || threshold > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(threshold),
                    threshold,
                    "The completion threshold must be greater than zero and no greater than one.");
            }

            _threshold = threshold;
        }

        public bool IsCompleted => _isCompleted;

        public bool TryComplete(float cleanedFraction)
        {
            if (_isCompleted ||
                float.IsNaN(cleanedFraction) ||
                cleanedFraction < _threshold)
            {
                return false;
            }

            _isCompleted = true;
            return true;
        }

        public void Reset()
        {
            _isCompleted = false;
        }
    }
}
