using CalmSpace.Haptics;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class HapticRateLimiterTests
    {
        [Test]
        public void FirstTickPlaysAndCallsInsideIntervalAreSuppressed()
        {
            var limiter = new HapticRateLimiter(0.05d, 0.10d);

            Assert.That(limiter.TryConsumeDragTick(1.00d), Is.True);
            Assert.That(limiter.TryConsumeDragTick(1.049d), Is.False);
            Assert.That(limiter.TryConsumeDragTick(1.05d), Is.True);
        }

        [Test]
        public void SnapCreatesPostSnapSilence()
        {
            var limiter = new HapticRateLimiter(0.05d, 0.10d);

            limiter.RecordSnap(2.00d);

            Assert.That(limiter.TryConsumeDragTick(2.099d), Is.False);
            Assert.That(limiter.TryConsumeDragTick(2.10d), Is.True);
        }

        [Test]
        public void SnapCallsInsideMinimumIntervalAreSuppressed()
        {
            var limiter = new HapticRateLimiter(
                0.05d,
                0.10d,
                0.08d);

            Assert.That(limiter.TryConsumeSnap(0d), Is.True);
            Assert.That(limiter.TryConsumeSnap(0.079d), Is.False);
            Assert.That(limiter.TryConsumeSnap(0.08d), Is.True);
        }

        [Test]
        public void RejectedSnapDoesNotExtendPostSnapSilence()
        {
            var limiter = new HapticRateLimiter(
                0.05d,
                0.10d,
                0.08d);

            Assert.That(limiter.TryConsumeSnap(2d), Is.True);
            Assert.That(limiter.TryConsumeSnap(2.079d), Is.False);

            Assert.That(limiter.TryConsumeDragTick(2.099d), Is.False);
            Assert.That(limiter.TryConsumeDragTick(2.10d), Is.True);
        }

        [Test]
        public void InvalidSnapTimesAreRejectedWithoutConsumingWindow()
        {
            var limiter = new HapticRateLimiter(
                0.05d,
                0.10d,
                0.08d);

            Assert.That(limiter.TryConsumeSnap(double.NaN), Is.False);
            Assert.That(
                limiter.TryConsumeSnap(double.PositiveInfinity),
                Is.False);
            Assert.That(
                limiter.TryConsumeSnap(double.NegativeInfinity),
                Is.False);
            Assert.That(limiter.TryConsumeSnap(0d), Is.True);
        }

        [Test]
        public void ConstructorRejectsNonPositiveIntervals()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new HapticRateLimiter(0d, 0.1d));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new HapticRateLimiter(0.05d, 0d));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new HapticRateLimiter(0.05d, 0.1d, 0d));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new HapticRateLimiter(
                    0.05d,
                    0.1d,
                    double.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new HapticRateLimiter(
                    0.05d,
                    0.1d,
                    double.PositiveInfinity));
        }
    }
}
