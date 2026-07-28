using CalmSpace.Cleaning;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace CalmSpace.Tests.EditMode
{
    public sealed class CleanCoverageTests
    {
        [Test]
        public void SumMaskJobSumsR8Pixels()
        {
            var pixels = new NativeArray<byte>(
                new byte[] { 0, 127, 128, 255 },
                Allocator.TempJob);
            var sum = new NativeArray<long>(1, Allocator.TempJob);

            new SumMaskJob
            {
                Pixels = pixels,
                Sum = sum,
                PixelCount = 4,
                ChannelStride = 1
            }.Run();

            Assert.That(sum[0], Is.EqualTo(510L));

            sum.Dispose();
            pixels.Dispose();
        }

        [Test]
        public void SumMaskJobReadsOnlyRedChannelFromRgba()
        {
            var pixels = new NativeArray<byte>(
                new byte[]
                {
                    10, 255, 255, 255,
                    20, 255, 255, 255
                },
                Allocator.TempJob);
            var sum = new NativeArray<long>(1, Allocator.TempJob);

            new SumMaskJob
            {
                Pixels = pixels,
                Sum = sum,
                PixelCount = 2,
                ChannelStride = 4
            }.Run();

            Assert.That(sum[0], Is.EqualTo(30L));

            sum.Dispose();
            pixels.Dispose();
        }

        [Test]
        public void CompletionThresholdFiresOnceAndResetRearmsIt()
        {
            var latch = new CleanCompletionLatch(0.95f);

            Assert.That(latch.TryComplete(0.949f), Is.False);
            Assert.That(latch.TryComplete(0.95f), Is.True);
            Assert.That(latch.TryComplete(1f), Is.False);

            latch.Reset();

            Assert.That(latch.TryComplete(1f), Is.True);
        }
    }
}
