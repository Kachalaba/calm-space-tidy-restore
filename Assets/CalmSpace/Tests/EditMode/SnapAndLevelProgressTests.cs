using CalmSpace.Core;
using CalmSpace.Levels;
using NUnit.Framework;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class SnapAndLevelProgressTests
    {
        [TestCase(0.09f, 0.10f, true)]
        [TestCase(0.10f, 0.10f, true)]
        [TestCase(0.11f, 0.10f, false)]
        public void PositionThresholdIsInclusive(
            float distance,
            float threshold,
            bool expected)
        {
            Assert.That(
                SnapMath.IsPositionWithinThreshold(
                    Vector3.zero,
                    Vector3.right * distance,
                    threshold),
                Is.EqualTo(expected));
        }

        [Test]
        public void PoseThresholdChecksBothDistanceAndRotation()
        {
            var target = new SnapPose(Vector3.zero, Quaternion.identity);

            Assert.That(
                SnapMath.IsPoseWithinThreshold(
                    Vector3.right * 0.05f,
                    Quaternion.Euler(0f, 9f, 0f),
                    target,
                    0.05f,
                    10f),
                Is.True);

            Assert.That(
                SnapMath.IsPoseWithinThreshold(
                    Vector3.right * 0.05f,
                    Quaternion.Euler(0f, 11f, 0f),
                    target,
                    0.05f,
                    10f),
                Is.False);
        }

        [Test]
        public void ProgressRejectsUnknownAndDuplicatePlacements()
        {
            var progress = new LevelProgressTracker(2);

            Assert.That(progress.RegisterItem(11), Is.True);
            Assert.That(progress.RegisterItem(22), Is.True);
            Assert.That(progress.RegisterItem(22), Is.False);
            Assert.That(progress.TryMarkPlaced(999), Is.False);
            Assert.That(progress.TryMarkPlaced(11), Is.True);
            Assert.That(progress.TryMarkPlaced(11), Is.False);
            Assert.That(progress.IsComplete, Is.False);
            Assert.That(progress.TryMarkPlaced(22), Is.True);
            Assert.That(progress.IsComplete, Is.True);
            Assert.That(progress.PlacedCount, Is.EqualTo(2));
        }

        [Test]
        public void ProgressRejectsRegistrationBeyondExpectedCount()
        {
            var progress = new LevelProgressTracker(1);

            Assert.That(progress.RegisterItem(11), Is.True);
            Assert.That(progress.RegisterItem(22), Is.False);
        }
    }
}
