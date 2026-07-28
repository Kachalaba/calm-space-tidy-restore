using CalmSpace.Core;
using CalmSpace.Monetization;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class PresentationActivityCoordinatorTests
    {
        [Test]
        public void GameplayAndDragLeasesBlockAdsUntilDisposed()
        {
            var coordinator = new PresentationActivityCoordinator();

            Assert.That(coordinator.TryEnterGameplay(out var gameplay), Is.True);
            Assert.That(
                coordinator.TryEnterAd(out var rejectedAd, out var gameplayReason),
                Is.False);
            Assert.That(rejectedAd, Is.Null);
            Assert.That(gameplayReason, Is.EqualTo(AdBlockReason.GameplayActive));

            Assert.That(coordinator.TryEnterDrag(out var drag), Is.True);
            gameplay.Dispose();

            Assert.That(
                coordinator.TryEnterAd(out rejectedAd, out var dragReason),
                Is.False);
            Assert.That(dragReason, Is.EqualTo(AdBlockReason.DragActive));

            drag.Dispose();

            Assert.That(
                coordinator.TryEnterAd(out var ad, out var acceptedReason),
                Is.True);
            Assert.That(acceptedReason, Is.EqualTo(AdBlockReason.None));
            ad.Dispose();
        }

        [Test]
        public void AdLeaseAtomicallyBlocksNewGameplayAndDragLeases()
        {
            var coordinator = new PresentationActivityCoordinator();

            Assert.That(coordinator.TryEnterAd(out var ad, out _), Is.True);
            Assert.That(coordinator.TryEnterGameplay(out var gameplay), Is.False);
            Assert.That(coordinator.TryEnterDrag(out var drag), Is.False);
            Assert.That(gameplay, Is.Null);
            Assert.That(drag, Is.Null);

            ad.Dispose();
            ad.Dispose();

            Assert.That(coordinator.TryEnterGameplay(out gameplay), Is.True);
            gameplay.Dispose();
        }

        [Test]
        public void MultipleDragLeasesAreCountedAndCannotUnderflow()
        {
            var coordinator = new PresentationActivityCoordinator();

            Assert.That(coordinator.TryEnterDrag(out var first), Is.True);
            Assert.That(coordinator.TryEnterDrag(out var second), Is.True);
            Assert.That(coordinator.ActiveDragCount, Is.EqualTo(2));

            first.Dispose();
            first.Dispose();
            Assert.That(coordinator.ActiveDragCount, Is.EqualTo(1));

            second.Dispose();
            Assert.That(coordinator.ActiveDragCount, Is.Zero);
        }
    }
}
