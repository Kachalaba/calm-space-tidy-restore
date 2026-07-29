using System;
using System.Threading;
using CalmSpace.Core;
using Cysharp.Threading.Tasks;

namespace CalmSpace.Monetization
{
    public enum RewardedBenefitKind
    {
        InstantSolutionHint = 0,
        ExtraRoomDecor = 1
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
        VerifiedNotEntitled = 2
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

    /// <summary>
    /// A rewarded placement can only request one of the two calm, explicit
    /// player benefits supported by the product. Provider input cannot invent
    /// arbitrary currency identifiers or amounts.
    /// </summary>
    public sealed class RewardedAdRequest
    {
        public RewardedAdRequest(
            string placementId,
            RewardedBenefitKind benefit,
            int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(placementId))
            {
                throw new ArgumentException(
                    "A placement identifier is required.",
                    nameof(placementId));
            }

            if (!Enum.IsDefined(typeof(RewardedBenefitKind), benefit))
            {
                throw new ArgumentOutOfRangeException(nameof(benefit));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            PlacementId = placementId;
            Benefit = benefit;
            Amount = amount;
        }

        public string PlacementId { get; }

        public RewardedBenefitKind Benefit { get; }

        public int Amount { get; }
    }

    public sealed class ProviderRewardedAdRequest
    {
        public ProviderRewardedAdRequest(string placementId)
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

    public readonly struct RewardGrant
    {
        public RewardGrant(
            RewardedBenefitKind benefit,
            int amount)
        {
            Benefit = benefit;
            Amount = amount;
        }

        public RewardedBenefitKind Benefit { get; }

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

        /// <summary>
        /// True only when the provider signalled a reward and the domain
        /// callback persisted it successfully.
        /// </summary>
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

    public readonly struct ProviderRelaxPassPurchaseResult
    {
        public ProviderRelaxPassPurchaseResult(
            ProviderPurchaseStatus status,
            string providerMessage)
        {
            Status = status;
            ProviderMessage = providerMessage;
        }

        public ProviderPurchaseStatus Status { get; }

        public string ProviderMessage { get; }
    }

    public interface IProviderRewardedAdSessionObserver
    {
        void OnOpened();

        void OnRewardEarned();
    }

    /// <summary>
    /// Deliberately rewarded-only. There is no forced or interstitial format
    /// in the provider contract, so an SDK adapter cannot interrupt play.
    /// </summary>
    public interface IRewardedAdProvider
    {
        event Action AvailabilityChanged;

        UniTask<ProviderInitializationStatus> InitializeAsync(
            CancellationToken cancellationToken);

        bool IsReady(string placementId);

        UniTask<ProviderAdResult> ShowAsync(
            ProviderRewardedAdRequest request,
            IProviderRewardedAdSessionObserver observer,
            CancellationToken cancellationToken);
    }

    public interface IRelaxPassEntitlementProvider
    {
        UniTask<ProviderEntitlementRestoreResult>
            RestoreRelaxPassAsync(
                CancellationToken cancellationToken);

        UniTask<ProviderRelaxPassPurchaseResult>
            PurchaseRelaxPassAsync(
                CancellationToken cancellationToken);
    }

    public interface IRewardedAdCallbacks
    {
        bool TryApplyReward(RewardGrant reward);

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

    /// <summary>
    /// Local receipt cache. The binary layout is intentionally unchanged from
    /// the original lifetime-no-ads v1 payload so existing installs migrate
    /// without changing the keystore alias or associated data.
    /// </summary>
    public sealed class PersistentMonetizationState
    {
        public PersistentMonetizationState(
            int schemaVersion,
            bool relaxPassOwned,
            long verifiedAtUnixSeconds)
        {
            if (schemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion));
            }

            SchemaVersion = schemaVersion;
            RelaxPassOwned = relaxPassOwned;
            VerifiedAtUnixSeconds = verifiedAtUnixSeconds;
        }

        public int SchemaVersion { get; }

        public bool RelaxPassOwned { get; }

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
            return CreateWithState(
                PersistentStateLoadStatus.Found,
                state);
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
            return CreateWithState(
                PersistentStateLoadStatus.RecoveredFromTemporary,
                state);
        }

        public static PersistentStateLoadResult RecoveredFromBackup(
            PersistentMonetizationState state)
        {
            return CreateWithState(
                PersistentStateLoadStatus.RecoveredFromBackup,
                state);
        }

        public static PersistentStateLoadResult Failed(
            PersistentStateLoadStatus status,
            string errorMessage)
        {
            if (status == PersistentStateLoadStatus.Found ||
                status == PersistentStateLoadStatus.Missing ||
                status ==
                    PersistentStateLoadStatus.RecoveredFromTemporary ||
                status ==
                    PersistentStateLoadStatus.RecoveredFromBackup)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new PersistentStateLoadResult(
                status,
                null,
                errorMessage);
        }

        private static PersistentStateLoadResult CreateWithState(
            PersistentStateLoadStatus status,
            PersistentMonetizationState state)
        {
            return new PersistentStateLoadResult(
                status,
                state ?? throw new ArgumentNullException(nameof(state)),
                null);
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
        public MonetizationOptions(
            bool allowRewardedForRelaxPassOwners)
        {
            AllowRewardedForRelaxPassOwners =
                allowRewardedForRelaxPassOwners;
        }

        public bool AllowRewardedForRelaxPassOwners { get; }
    }

    public readonly struct MonetizationSnapshot
    {
        public MonetizationSnapshot(
            bool isInitialized,
            ProviderInitializationStatus adProviderStatus,
            bool relaxPassOwned,
            EntitlementVerification entitlementVerification)
        {
            IsInitialized = isInitialized;
            AdProviderStatus = adProviderStatus;
            RelaxPassOwned = relaxPassOwned;
            EntitlementVerification = entitlementVerification;
        }

        public bool IsInitialized { get; }

        public ProviderInitializationStatus AdProviderStatus { get; }

        public bool RelaxPassOwned { get; }

        public EntitlementVerification EntitlementVerification { get; }
    }

    public interface IMonetizationManager : IDisposable
    {
        event Action AvailabilityChanged;

        MonetizationSnapshot Snapshot { get; }

        bool HasRelaxPass { get; }

        UniTask InitializeAsync(
            CancellationToken cancellationToken = default);

        UniTask<RewardedAdResult> ShowRewardedAsync(
            RewardedAdRequest request,
            IRewardedAdCallbacks callbacks,
            CancellationToken cancellationToken = default);

        UniTask<ProviderRelaxPassPurchaseResult>
            PurchaseRelaxPassAsync(
                CancellationToken cancellationToken = default);

        UniTask<ProviderEntitlementRestoreResult>
            RestoreRelaxPassAsync(
                CancellationToken cancellationToken = default);
    }
}
