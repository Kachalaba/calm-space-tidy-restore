using System;
using System.Threading;
using CalmSpace.Core;
using Cysharp.Threading.Tasks;

namespace CalmSpace.Monetization
{
    public enum AdFormat
    {
        Interstitial = 0,
        Rewarded = 1
    }

    public enum ProviderInitializationStatus
    {
        Unavailable = 0,
        Ready = 1
    }

    public enum ProviderAdOutcome
    {
        Unavailable = 0,
        Completed = 1,
        Closed = 2,
        Failed = 3,
        Cancelled = 4
    }

    public enum AdShowOutcome
    {
        Blocked = 0,
        Completed = 1,
        Closed = 2,
        Failed = 3,
        Unavailable = 4,
        Cancelled = 5
    }

    public enum ProviderEntitlementStatus
    {
        Unavailable = 0,
        VerifiedEntitled = 1,
        VerifiedNotEntitled = 2,
        Owned = VerifiedEntitled,
        NotOwned = VerifiedNotEntitled,
        Unknown = Unavailable
    }

    public enum ProviderPurchaseStatus
    {
        Unavailable = 0,
        Purchased = 1,
        AlreadyOwned = 2,
        Cancelled = 3,
        Failed = 4
    }

    public enum EntitlementVerification
    {
        Unverified = 0,
        VerifiedEntitled = 1,
        VerifiedNotEntitled = 2,
        Unavailable = 3
    }

    public enum DataUnprotectStatus
    {
        Success = 0,
        InvalidPayload = 1,
        UnsupportedVersion = 2,
        AuthenticationFailed = 3
    }

    public enum PersistentStateLoadStatus
    {
        Missing = 0,
        Found = 1,
        RecoveredFromTemporary = 2,
        RecoveredFromBackup = 3,
        AuthenticationFailed = 4,
        InvalidData = 5,
        UnsupportedVersion = 6,
        IoError = 7,
        Cancelled = 8
    }

    public enum PersistentStateSaveStatus
    {
        Failed = 0,
        Succeeded = 1,
        Cancelled = 2
    }

    public sealed class InterstitialAdRequest
    {
        public InterstitialAdRequest(string placementId)
        {
            if (string.IsNullOrWhiteSpace(placementId))
            {
                throw new ArgumentException(
                    "A placement identifier is required.",
                    nameof(placementId));
            }

            PlacementId = placementId;
        }

        public string PlacementId { get; }
    }

    public sealed class RewardedAdRequest
    {
        public RewardedAdRequest(
            string placementId,
            string rewardId,
            int amount)
        {
            if (string.IsNullOrWhiteSpace(placementId))
            {
                throw new ArgumentException(
                    "A placement identifier is required.",
                    nameof(placementId));
            }

            if (string.IsNullOrWhiteSpace(rewardId))
            {
                throw new ArgumentException(
                    "A reward identifier is required.",
                    nameof(rewardId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "A reward amount must be positive.");
            }

            PlacementId = placementId;
            RewardId = rewardId;
            Amount = amount;
        }

        public string PlacementId { get; }

        public string RewardId { get; }

        public int Amount { get; }
    }

    public sealed class ProviderAdRequest
    {
        public ProviderAdRequest(
            AdFormat format,
            string placementId,
            string rewardId = null,
            int rewardAmount = 0)
        {
            if (string.IsNullOrWhiteSpace(placementId))
            {
                throw new ArgumentException(
                    "A placement identifier is required.",
                    nameof(placementId));
            }

            if (format == AdFormat.Rewarded)
            {
                if (string.IsNullOrWhiteSpace(rewardId))
                {
                    throw new ArgumentException(
                        "A rewarded request requires a reward identifier.",
                        nameof(rewardId));
                }

                if (rewardAmount <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(rewardAmount),
                        rewardAmount,
                        "A reward amount must be positive.");
                }
            }

            Format = format;
            PlacementId = placementId;
            RewardId = rewardId;
            RewardAmount = rewardAmount;
        }

        public AdFormat Format { get; }

        public string PlacementId { get; }

        public string RewardId { get; }

        public int RewardAmount { get; }
    }

    public readonly struct ProviderAdResult
    {
        public ProviderAdResult(
            ProviderAdOutcome outcome,
            string providerMessage)
        {
            Outcome = outcome;
            ProviderMessage = providerMessage;
        }

        public ProviderAdOutcome Outcome { get; }

        public string ProviderMessage { get; }
    }

    public readonly struct InterstitialAdResult
    {
        public InterstitialAdResult(
            AdShowOutcome outcome,
            AdBlockReason blockReason,
            string providerMessage)
        {
            Outcome = outcome;
            BlockReason = blockReason;
            ProviderMessage = providerMessage;
        }

        public AdShowOutcome Outcome { get; }

        public AdBlockReason BlockReason { get; }

        public string ProviderMessage { get; }
    }

    public readonly struct RewardGrant
    {
        public RewardGrant(string rewardId, int amount)
        {
            RewardId = rewardId;
            Amount = amount;
        }

        public string RewardId { get; }

        public int Amount { get; }
    }

    public readonly struct RewardedAdResult
    {
        public RewardedAdResult(
            AdShowOutcome outcome,
            AdBlockReason blockReason,
            bool rewardEarned,
            string providerMessage)
        {
            Outcome = outcome;
            BlockReason = blockReason;
            RewardEarned = rewardEarned;
            ProviderMessage = providerMessage;
        }

        public AdShowOutcome Outcome { get; }

        public AdBlockReason BlockReason { get; }

        public bool RewardEarned { get; }

        public string ProviderMessage { get; }
    }

    public readonly struct ProviderEntitlementRestoreResult
    {
        public ProviderEntitlementRestoreResult(
            ProviderEntitlementStatus status,
            string providerMessage)
        {
            Status = status;
            ProviderMessage = providerMessage;
        }

        public ProviderEntitlementStatus Status { get; }

        public string ProviderMessage { get; }
    }

    public readonly struct ProviderNoAdsPurchaseResult
    {
        public ProviderNoAdsPurchaseResult(
            ProviderPurchaseStatus status,
            string providerMessage)
        {
            Status = status;
            ProviderMessage = providerMessage;
        }

        public ProviderPurchaseStatus Status { get; }

        public string ProviderMessage { get; }
    }

    public interface IProviderAdSessionObserver
    {
        void OnOpened();

        void OnRewardEarned();
    }

    public interface IAdProvider
    {
        event Action AvailabilityChanged;

        UniTask<ProviderInitializationStatus> InitializeAsync(
            CancellationToken cancellationToken);

        bool IsReady(AdFormat format, string placementId);

        /// <summary>
        /// Owns the complete fullscreen session. Implementations must invoke
        /// OnOpened only after the SDK confirms presentation, deliver any
        /// reward signal before completion, and complete this UniTask only
        /// after the fullscreen view has closed and no further observer calls
        /// can occur. This invariant keeps the manager's atomic ad lease valid
        /// for the entire visible presentation.
        /// </summary>
        UniTask<ProviderAdResult> ShowAsync(
            ProviderAdRequest request,
            IProviderAdSessionObserver observer,
            CancellationToken cancellationToken);
    }

    public interface INoAdsEntitlementProvider
    {
        UniTask<ProviderEntitlementRestoreResult>
            RestoreLifetimeNoAdsAsync(
                CancellationToken cancellationToken);

        UniTask<ProviderNoAdsPurchaseResult>
            PurchaseLifetimeNoAdsAsync(
                CancellationToken cancellationToken);
    }

    public interface IRewardedAdCallbacks
    {
        void OnRewardEarned(RewardGrant reward);

        void OnCompleted(RewardedAdResult result);
    }

    public interface IAuthenticatedDataProtector
    {
        byte[] Protect(byte[] plaintext, byte[] associatedData);

        DataUnprotectStatus TryUnprotect(
            byte[] protectedData,
            byte[] associatedData,
            out byte[] plaintext);
    }

    public sealed class PersistentMonetizationState
    {
        public PersistentMonetizationState(
            int schemaVersion,
            bool lifetimeNoAds,
            long verifiedAtUnixSeconds)
        {
            if (schemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    schemaVersion,
                    "A positive schema version is required.");
            }

            SchemaVersion = schemaVersion;
            LifetimeNoAds = lifetimeNoAds;
            VerifiedAtUnixSeconds = verifiedAtUnixSeconds;
        }

        public int SchemaVersion { get; }

        public bool LifetimeNoAds { get; }

        public long VerifiedAtUnixSeconds { get; }
    }

    public readonly struct PersistentStateLoadResult
    {
        private PersistentStateLoadResult(
            PersistentStateLoadStatus status,
            PersistentMonetizationState state,
            string errorMessage)
        {
            Status = status;
            State = state;
            ErrorMessage = errorMessage;
        }

        public PersistentStateLoadStatus Status { get; }

        public PersistentMonetizationState State { get; }

        public string ErrorMessage { get; }

        public bool HasState =>
            State != null &&
            (Status == PersistentStateLoadStatus.Found ||
             Status == PersistentStateLoadStatus.RecoveredFromTemporary ||
             Status == PersistentStateLoadStatus.RecoveredFromBackup);

        public static PersistentStateLoadResult Found(
            PersistentMonetizationState state)
        {
            return new PersistentStateLoadResult(
                PersistentStateLoadStatus.Found,
                state ?? throw new ArgumentNullException(nameof(state)),
                null);
        }

        public static PersistentStateLoadResult Missing()
        {
            return new PersistentStateLoadResult(
                PersistentStateLoadStatus.Missing,
                null,
                null);
        }

        public static PersistentStateLoadResult RecoveredFromTemporary(
            PersistentMonetizationState state)
        {
            return new PersistentStateLoadResult(
                PersistentStateLoadStatus.RecoveredFromTemporary,
                state ?? throw new ArgumentNullException(nameof(state)),
                null);
        }

        public static PersistentStateLoadResult RecoveredFromBackup(
            PersistentMonetizationState state)
        {
            return new PersistentStateLoadResult(
                PersistentStateLoadStatus.RecoveredFromBackup,
                state ?? throw new ArgumentNullException(nameof(state)),
                null);
        }

        public static PersistentStateLoadResult Failed(
            PersistentStateLoadStatus status,
            string errorMessage)
        {
            if (status == PersistentStateLoadStatus.Found ||
                status == PersistentStateLoadStatus.Missing ||
                status ==
                PersistentStateLoadStatus.RecoveredFromTemporary ||
                status == PersistentStateLoadStatus.RecoveredFromBackup)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new PersistentStateLoadResult(
                status,
                null,
                errorMessage);
        }
    }

    public readonly struct PersistentStateSaveResult
    {
        private PersistentStateSaveResult(
            PersistentStateSaveStatus status,
            string errorMessage)
        {
            Status = status;
            ErrorMessage = errorMessage;
        }

        public PersistentStateSaveStatus Status { get; }

        public string ErrorMessage { get; }

        public bool IsSuccess =>
            Status == PersistentStateSaveStatus.Succeeded;

        public static PersistentStateSaveResult Succeeded()
        {
            return new PersistentStateSaveResult(
                PersistentStateSaveStatus.Succeeded,
                null);
        }

        public static PersistentStateSaveResult Failed(
            string errorMessage)
        {
            return new PersistentStateSaveResult(
                PersistentStateSaveStatus.Failed,
                errorMessage);
        }

        public static PersistentStateSaveResult Cancelled()
        {
            return new PersistentStateSaveResult(
                PersistentStateSaveStatus.Cancelled,
                null);
        }
    }

    public interface IMonetizationStateStore
    {
        UniTask<PersistentStateLoadResult> LoadAsync(
            CancellationToken cancellationToken);

        UniTask<PersistentStateSaveResult> SaveAsync(
            PersistentMonetizationState state,
            CancellationToken cancellationToken);
    }

    public sealed class MonetizationOptions
    {
        public static readonly TimeSpan MinimumInterstitialCooldown =
            TimeSpan.FromSeconds(180d);

        public MonetizationOptions(
            TimeSpan interstitialCooldown,
            bool allowRewardedForNoAdsOwners)
        {
            if (interstitialCooldown < MinimumInterstitialCooldown)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interstitialCooldown),
                    interstitialCooldown,
                    "Interstitial cooldown must be at least 180 seconds.");
            }

            InterstitialCooldown = interstitialCooldown;
            AllowRewardedForNoAdsOwners =
                allowRewardedForNoAdsOwners;
        }

        public TimeSpan InterstitialCooldown { get; }

        public bool AllowRewardedForNoAdsOwners { get; }
    }

    public readonly struct MonetizationSnapshot
    {
        public MonetizationSnapshot(
            bool isInitialized,
            ProviderInitializationStatus adProviderStatus,
            bool cachedLifetimeNoAds,
            EntitlementVerification entitlementVerification,
            bool suppressInterruptiveAds)
        {
            IsInitialized = isInitialized;
            AdProviderStatus = adProviderStatus;
            CachedLifetimeNoAds = cachedLifetimeNoAds;
            EntitlementVerification = entitlementVerification;
            SuppressInterruptiveAds = suppressInterruptiveAds;
        }

        public bool IsInitialized { get; }

        public ProviderInitializationStatus AdProviderStatus { get; }

        public bool CachedLifetimeNoAds { get; }

        public EntitlementVerification EntitlementVerification { get; }

        public bool SuppressInterruptiveAds { get; }
    }

    public interface IMonetizationManager : IDisposable
    {
        event Action AvailabilityChanged;

        MonetizationSnapshot Snapshot { get; }

        bool IsNoAds { get; }

        UniTask InitializeAsync(
            CancellationToken cancellationToken = default);

        UniTask<InterstitialAdResult> TryShowInterstitialAsync(
            InterstitialAdRequest request,
            CancellationToken cancellationToken = default);

        UniTask<InterstitialAdResult>
            TryShowInterstitialBetweenLevelsAsync(
                string placementId,
                CancellationToken cancellationToken = default);

        UniTask<RewardedAdResult> ShowRewardedAsync(
            RewardedAdRequest request,
            IRewardedAdCallbacks callbacks,
            CancellationToken cancellationToken = default);

        UniTask<RewardedAdResult> TryShowRewardedAsync(
            RewardedAdRequest request,
            IRewardedAdCallbacks callbacks,
            CancellationToken cancellationToken = default);

        UniTask<ProviderNoAdsPurchaseResult>
            PurchaseLifetimeNoAdsAsync(
                CancellationToken cancellationToken = default);

        UniTask<ProviderNoAdsPurchaseResult> PurchaseNoAdsAsync(
            CancellationToken cancellationToken = default);

        UniTask<ProviderEntitlementRestoreResult>
            RestoreLifetimeNoAdsAsync(
                CancellationToken cancellationToken = default);

        UniTask<ProviderEntitlementRestoreResult> RestoreNoAdsAsync(
            CancellationToken cancellationToken = default);
    }
}
