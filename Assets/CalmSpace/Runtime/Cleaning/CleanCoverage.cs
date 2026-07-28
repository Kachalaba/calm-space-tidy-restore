using System;
using Unity.Collections;
using Unity.Jobs;

namespace CalmSpace.Cleaning
{
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
