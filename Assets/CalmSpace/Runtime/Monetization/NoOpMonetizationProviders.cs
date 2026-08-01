using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace CalmSpace.Monetization
{
    /// <summary>
    /// Conservative rewarded adapter used until an SDK is selected. It never
    /// reports inventory and can never open a fullscreen presentation.
    /// </summary>
    public sealed class NoOpRewardedAdProvider :
        IRewardedAdProvider
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

        public bool IsReady(string placementId)
        {
            return false;
        }

        public UniTask<ProviderAdResult> ShowAsync(
            ProviderRewardedAdRequest request,
            IProviderRewardedAdSessionObserver observer,
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
    /// Conservative store adapter. A receipt-authoritative IAP integration can
    /// replace it without changing gameplay or rewarded-ad policy.
    /// </summary>
    public sealed class NoOpRelaxPassEntitlementProvider :
        IRelaxPassEntitlementProvider
    {
        public UniTask<ProviderEntitlementRestoreResult>
            RestoreRelaxPassAsync(
                CancellationToken cancellationToken)
        {
            return UniTask.FromResult(
                new ProviderEntitlementRestoreResult(
                    ProviderEntitlementStatus.Unavailable,
                    null));
        }

        public UniTask<ProviderRelaxPassPurchaseResult>
            PurchaseRelaxPassAsync(
                CancellationToken cancellationToken)
        {
            return UniTask.FromResult(
                new ProviderRelaxPassPurchaseResult(
                    cancellationToken.IsCancellationRequested
                        ? ProviderPurchaseStatus.Cancelled
                        : ProviderPurchaseStatus.Unavailable,
                    null));
        }
    }
}
