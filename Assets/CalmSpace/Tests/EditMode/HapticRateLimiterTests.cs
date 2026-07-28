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
        public void ConstructorRejectsNonPositiveIntervals()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new HapticRateLimiter(0d, 0.1d));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new HapticRateLimiter(0.05d, 0d));
        }
    }
}
