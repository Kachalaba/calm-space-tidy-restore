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
            Assert.That(profile.CalmPoints, Is.Zero);
            Assert.That(profile.RewardedLevelMask, Is.Zero);
            Assert.That(profile.OwnedDecorationMask, Is.EqualTo(1));
            Assert.That(profile.SelectedDecorationIndex, Is.Zero);
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

        [Test]
        public void CompletionRewardIsGrantedOnlyOncePerLevel()
        {
            DemoProgressSnapshot initial =
                DemoProgressRules.CreateDefault(8, "sage");

            DemoProgressSnapshot rewarded =
                DemoProgressRules.CompleteLevelWithReward(
                    initial,
                    2,
                    8,
                    "sage",
                    10);
            DemoProgressSnapshot replay =
                DemoProgressRules.CompleteLevelWithReward(
                    rewarded,
                    2,
                    8,
                    "sage",
                    10);

            Assert.That(rewarded.CalmPoints, Is.EqualTo(10));
            Assert.That(
                rewarded.RewardedLevelMask,
                Is.EqualTo(1 << 2));
            Assert.That(replay, Is.EqualTo(rewarded));
        }

        [Test]
        public void PurchasingDecorationDeductsOnceAndSelectsIt()
        {
            var funded = new DemoProgressSnapshot(
                3,
                0b0111,
                "sage",
                true,
                50,
                0b0111,
                1,
                0);

            Assert.That(
                DemoProgressRules.TryPurchaseAndSelectDecoration(
                    funded,
                    2,
                    35,
                    8,
                    "sage",
                    out DemoProgressSnapshot purchased),
                Is.True);
            Assert.That(purchased.CalmPoints, Is.EqualTo(15));
            Assert.That(
                purchased.OwnedDecorationMask,
                Is.EqualTo(0b0101));
            Assert.That(
                purchased.SelectedDecorationIndex,
                Is.EqualTo(2));

            Assert.That(
                DemoProgressRules.TryPurchaseAndSelectDecoration(
                    purchased,
                    2,
                    35,
                    8,
                    "sage",
                    out DemoProgressSnapshot selectedAgain),
                Is.True);
            Assert.That(selectedAgain, Is.EqualTo(purchased));
        }

        [Test]
        public void CannotBuyUnaffordableDecorationOrSelectUnownedOne()
        {
            var initial = new DemoProgressSnapshot(
                0,
                0,
                "sage",
                true,
                5,
                0,
                1,
                0);

            Assert.That(
                DemoProgressRules.TryPurchaseAndSelectDecoration(
                    initial,
                    3,
                    40,
                    8,
                    "sage",
                    out DemoProgressSnapshot afterPurchase),
                Is.False);
            Assert.That(afterPurchase, Is.EqualTo(initial));
            Assert.That(
                DemoProgressRules.TrySelectDecoration(
                    initial,
                    3,
                    8,
                    "sage",
                    out DemoProgressSnapshot afterSelect),
                Is.False);
            Assert.That(afterSelect, Is.EqualTo(initial));
        }
    }
}
