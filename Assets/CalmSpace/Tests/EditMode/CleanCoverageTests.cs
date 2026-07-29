using CalmSpace.Cleaning;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace CalmSpace.Tests.EditMode
{
    public sealed class CleanCoverageTests
    {
        [Test]
        public void CoverageGateRequiresBothFrameAndTimeIntervals()
        {
            var gate = new CoverageSampleGate(10, 0.25d);

            Assert.That(gate.TryReserve(100L, 5d), Is.True);
            Assert.That(gate.TryReserve(109L, 5.25d), Is.False);
            Assert.That(gate.TryReserve(110L, 5.249d), Is.False);
            Assert.That(gate.TryReserve(110L, 5.25d), Is.True);
        }

        [Test]
        public void RejectedCoverageReservationDoesNotMoveWindow()
        {
            var gate = new CoverageSampleGate(10, 0.5d);

            Assert.That(gate.TryReserve(10L, 1d), Is.True);
            Assert.That(gate.TryReserve(15L, 2d), Is.False);
            Assert.That(gate.TryReserve(20L, 1.5d), Is.True);
        }

        [Test]
        public void CoverageGateResetRearmsFromEarlierFrameAndTime()
        {
            var gate = new CoverageSampleGate(10, 0.25d);

            Assert.That(gate.TryReserve(100L, 10d), Is.True);

            gate.Reset();

            Assert.That(gate.TryReserve(0L, 0d), Is.True);
        }

        [Test]
        public void InvalidCoverageSampleDoesNotConsumeWindow()
        {
            var gate = new CoverageSampleGate(10, 0.25d);

            Assert.That(gate.TryReserve(-1L, 0d), Is.False);
            Assert.That(gate.TryReserve(0L, double.NaN), Is.False);
            Assert.That(
                gate.TryReserve(0L, double.PositiveInfinity),
                Is.False);
            Assert.That(
                gate.TryReserve(0L, double.NegativeInfinity),
                Is.False);
            Assert.That(gate.TryReserve(0L, 0d), Is.True);
        }

        [Test]
        public void CoverageGateRejectsInvalidConfiguration()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CoverageSampleGate(0, 0.25d));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CoverageSampleGate(-1, 0.25d));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CoverageSampleGate(10, 0d));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CoverageSampleGate(10, -0.25d));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CoverageSampleGate(10, double.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CoverageSampleGate(
                    10,
                    double.PositiveInfinity));
        }

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
