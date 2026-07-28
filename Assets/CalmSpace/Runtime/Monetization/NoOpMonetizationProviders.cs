using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace CalmSpace.Monetization
{
    /// <summary>
    /// Conservative adapter used until a real ad SDK is selected and wired.
    /// It never reports inventory and never emits presentation callbacks.
    /// </summary>
    public sealed class NoOpAdProvider : IAdProvider
    {
        public event Action AvailabilityChanged
        {
            add { }
            remove { }
        }

        public UniTask<ProviderInitializationStatus> InitializeAsync(
            CancellationToken cancellationToken)
        {
            return UniTask.FromResult(
                ProviderInitializationStatus.Unavailable);
        }

        public bool IsReady(AdFormat format, string placementId)
        {
            return false;
        }

        public UniTask<ProviderAdResult> ShowAsync(
            ProviderAdRequest request,
            IProviderAdSessionObserver observer,
            CancellationToken cancellationToken)
        {
            return UniTask.FromResult(
                new ProviderAdResult(
                    cancellationToken.IsCancellationRequested
                        ? ProviderAdOutcome.Cancelled
                        : ProviderAdOutcome.Unavailable,
                    null));
        }
    }

    /// <summary>
    /// Conservative store adapter used until a receipt-authoritative IAP
    /// provider is selected. It never grants or revokes ownership.
    /// </summary>
    public sealed class NoOpNoAdsEntitlementProvider :
        INoAdsEntitlementProvider
    {
        public UniTask<ProviderEntitlementRestoreResult>
            RestoreLifetimeNoAdsAsync(
                CancellationToken cancellationToken)
        {
            return UniTask.FromResult(
                new ProviderEntitlementRestoreResult(
                    ProviderEntitlementStatus.Unavailable,
                    null));
        }

        public UniTask<ProviderNoAdsPurchaseResult>
            PurchaseLifetimeNoAdsAsync(
                CancellationToken cancellationToken)
        {
            return UniTask.FromResult(
                new ProviderNoAdsPurchaseResult(
                    cancellationToken.IsCancellationRequested
                        ? ProviderPurchaseStatus.Cancelled
                        : ProviderPurchaseStatus.Unavailable,
                    null));
        }
    }
}
