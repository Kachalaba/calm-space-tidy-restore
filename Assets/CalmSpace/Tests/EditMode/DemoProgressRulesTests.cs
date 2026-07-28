using CalmSpace.Demo;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class DemoProgressRulesTests
    {
        [Test]
        public void DefaultProfileUnlocksOnlyFirstLevel()
        {
            DemoProgressSnapshot profile =
                DemoProgressRules.CreateDefault(6, "sage");

            Assert.That(
                profile.HighestUnlockedLevelIndex,
                Is.Zero);
            Assert.That(profile.CompletedLevelMask, Is.Zero);
            Assert.That(profile.SelectedThemeId, Is.EqualTo("sage"));
            Assert.That(profile.MusicEnabled, Is.True);
            Assert.That(
                DemoProgressRules.IsLevelUnlocked(profile, 0, 6),
                Is.True);
            Assert.That(
                DemoProgressRules.IsLevelUnlocked(profile, 1, 6),
                Is.False);
        }

        [Test]
        public void CompletingLevelUnlocksNextAndIsIdempotent()
        {
            DemoProgressSnapshot initial =
                DemoProgressRules.CreateDefault(6, "sage");
            DemoProgressSnapshot completed =
                DemoProgressRules.MarkCompleted(
                    initial,
                    0,
                    6,
                    "sage");
            DemoProgressSnapshot duplicate =
                DemoProgressRules.MarkCompleted(
                    completed,
                    0,
                    6,
                    "sage");

            Assert.That(completed, Is.EqualTo(duplicate));
            Assert.That(
                completed.HighestUnlockedLevelIndex,
                Is.EqualTo(1));
            Assert.That(
                DemoProgressRules.IsLevelCompleted(
                    completed,
                    0,
                    6),
                Is.True);
            Assert.That(
                DemoProgressRules.IsLevelUnlocked(
                    completed,
                    1,
                    6),
                Is.True);
        }

        [Test]
        public void LastLevelCompletionDoesNotUnlockPastCatalog()
        {
            var snapshot = new DemoProgressSnapshot(
                5,
                0,
                "ocean",
                true);

            DemoProgressSnapshot completed =
                DemoProgressRules.MarkCompleted(
                    snapshot,
                    5,
                    6,
                    "sage");

            Assert.That(
                completed.HighestUnlockedLevelIndex,
                Is.EqualTo(5));
            Assert.That(
                DemoProgressRules.IsLevelCompleted(
                    completed,
                    5,
                    6),
                Is.True);
        }

        [Test]
        public void NormalizeClampsCorruptIndicesAndTrimsMask()
        {
            var corrupt = new DemoProgressSnapshot(
                900,
                -1,
                string.Empty,
                false);

            DemoProgressSnapshot normalized =
                DemoProgressRules.Normalize(
                    corrupt,
                    6,
                    "sage");

            Assert.That(
                normalized.HighestUnlockedLevelIndex,
                Is.EqualTo(5));
            Assert.That(
                normalized.CompletedLevelMask,
                Is.EqualTo(0b11_1111));
            Assert.That(
                normalized.SelectedThemeId,
                Is.EqualTo("sage"));
            Assert.That(normalized.MusicEnabled, Is.False);
        }

        [Test]
        public void RecommendedLevelIsFirstUnlockedIncomplete()
        {
            var snapshot = new DemoProgressSnapshot(
                3,
                0b0101,
                "sunset",
                true);

            Assert.That(
                DemoProgressRules.GetRecommendedLevel(snapshot, 6),
                Is.EqualTo(1));
            Assert.That(
                DemoProgressRules.CountCompleted(snapshot, 6),
                Is.EqualTo(2));
        }

        [Test]
        public void EmptyCatalogProducesNoRecommendedLevel()
        {
            DemoProgressSnapshot profile =
                DemoProgressRules.CreateDefault(0, "sage");

            Assert.That(
                profile.HighestUnlockedLevelIndex,
                Is.EqualTo(-1));
            Assert.That(
                DemoProgressRules.GetRecommendedLevel(profile, 0),
                Is.EqualTo(-1));
        }
    }
}
