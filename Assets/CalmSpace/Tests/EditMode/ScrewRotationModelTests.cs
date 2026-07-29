using System;
using CalmSpace.Fasteners;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class ScrewRotationModelTests
    {
        [Test]
        public void RejectsATurnCountBelowOne()
        {
            Assert.That(
                () => new ScrewRotationModel(0, 0.5f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void RejectsANonPositiveOrNonFiniteTurnDuration()
        {
            Assert.That(
                () => new ScrewRotationModel(3, 0f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new ScrewRotationModel(3, float.NaN),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new ScrewRotationModel(
                    3,
                    float.PositiveInfinity),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ReportsOneBoundaryPerWholeTurn()
        {
            var model = new ScrewRotationModel(3, 0.5f);

            ScrewAdvanceResult first = model.Advance(0.5f);
            Assert.That(first.CrossedTurnBoundary, Is.True);
            Assert.That(first.CompletedTurns, Is.EqualTo(1));
            Assert.That(first.IsComplete, Is.False);
            Assert.That(
                first.Progress,
                Is.EqualTo(1f / 3f).Within(1e-4f));

            ScrewAdvanceResult partial = model.Advance(0.25f);
            Assert.That(partial.CrossedTurnBoundary, Is.False);
            Assert.That(partial.CompletedTurns, Is.EqualTo(1));

            ScrewAdvanceResult second = model.Advance(0.25f);
            Assert.That(second.CrossedTurnBoundary, Is.True);
            Assert.That(second.CompletedTurns, Is.EqualTo(2));
        }

        [Test]
        public void ReportsEveryNewlyCompletedTurnIncludingTheFinalTurn()
        {
            var model = new ScrewRotationModel(3, 1f);

            ScrewAdvanceResult multiTurn = model.Advance(2.25f);
            Assert.That(
                ReadNewlyCompletedTurns(multiTurn),
                Is.EqualTo(2),
                "One advance can cross more than one whole turn.");
            Assert.That(multiTurn.IsComplete, Is.False);

            ScrewAdvanceResult finalTurn = model.Advance(0.75f);
            Assert.That(
                ReadNewlyCompletedTurns(finalTurn),
                Is.EqualTo(1),
                "Completing the screw still owes the final turn tick.");
            Assert.That(finalTurn.IsComplete, Is.True);
        }

        [Test]
        public void PausingFreezesProgressWithoutRewindingIt()
        {
            var model = new ScrewRotationModel(4, 0.5f);
            model.Advance(0.75f);
            float held = model.Progress;

            // Standing in for a lifted finger: no time is fed in at all.
            Assert.That(
                model.Advance(0f).Progress,
                Is.EqualTo(held));
            Assert.That(
                model.Advance(-1f).Progress,
                Is.EqualTo(held));
            Assert.That(
                model.Advance(float.NaN).Progress,
                Is.EqualTo(held));
            Assert.That(model.CompletedTurns, Is.EqualTo(1));

            // Picking the same screw back up resumes from where it stopped.
            model.Advance(0.25f);
            Assert.That(model.CompletedTurns, Is.EqualTo(2));
            Assert.That(
                model.Progress,
                Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void ClampsAtCompletionAndStopsReportingBoundaries()
        {
            var model = new ScrewRotationModel(2, 0.5f);

            ScrewAdvanceResult overshoot = model.Advance(10f);
            Assert.That(overshoot.IsComplete, Is.True);
            Assert.That(overshoot.CrossedTurnBoundary, Is.True);
            Assert.That(overshoot.CompletedTurns, Is.EqualTo(2));
            Assert.That(
                overshoot.Progress,
                Is.EqualTo(1f).Within(1e-4f));

            ScrewAdvanceResult afterwards = model.Advance(5f);
            Assert.That(afterwards.IsComplete, Is.True);
            Assert.That(
                afterwards.CrossedTurnBoundary,
                Is.False,
                "A finished screw must not keep ticking.");
            Assert.That(
                afterwards.Progress,
                Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void TreatsAnInfiniteDeltaAsFinishingTheScrew()
        {
            var model = new ScrewRotationModel(3, 0.5f);

            ScrewAdvanceResult result =
                model.Advance(float.PositiveInfinity);

            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.CompletedTurns, Is.EqualTo(3));
            Assert.That(
                result.Progress,
                Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void AccumulatesRotationAcrossWholeAndPartialTurns()
        {
            var model = new ScrewRotationModel(3, 0.5f);

            model.Advance(0.75f);

            Assert.That(
                model.RotationDegrees,
                Is.EqualTo(540f).Within(1e-2f));
        }

        [Test]
        public void ResetReturnsTheScrewToItsSeatedState()
        {
            var model = new ScrewRotationModel(2, 0.5f);
            model.Advance(10f);

            model.Reset();

            Assert.That(model.IsComplete, Is.False);
            Assert.That(model.CompletedTurns, Is.EqualTo(0));
            Assert.That(model.Progress, Is.EqualTo(0f));
            Assert.That(
                model.Advance(0.5f).CrossedTurnBoundary,
                Is.True);
        }

        private static int ReadNewlyCompletedTurns(
            ScrewAdvanceResult result)
        {
            var property = typeof(ScrewAdvanceResult).GetProperty(
                "NewlyCompletedTurns");
            Assert.That(
                property,
                Is.Not.Null,
                "ScrewAdvanceResult must expose a public integer " +
                "NewlyCompletedTurns count for ScrewController.");
            Assert.That(
                property.PropertyType,
                Is.EqualTo(typeof(int)),
                "NewlyCompletedTurns is a count.");
            return (int)property.GetValue(result);
        }
    }
}
