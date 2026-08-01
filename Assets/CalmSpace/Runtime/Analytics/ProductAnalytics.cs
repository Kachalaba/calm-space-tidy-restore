using System;
using System.Globalization;
using System.Text;
using CalmSpace.Demo;
using CalmSpace.Levels;
using CalmSpace.Monetization;
using CalmSpace.Workshop;
using UnityEngine;

namespace CalmSpace.Analytics
{
    public enum ProductEventKind
    {
        SessionStarted = 0,
        LevelStarted = 1,
        LevelCompleted = 2,
        LevelAbandoned = 3,
        UndoUsed = 4,
        DecorationPurchased = 5,
        DecorationSelected = 6,
        ThemeSelected = 7,
        MusicChanged = 8,
        LocaleChanged = 9,
        WorkshopViewed = 10,
        RestorationTaskSelected = 11,
        RestorationRevealStarted = 12,
        RestorationRevealCompleted = 13,
        MemoryUnlocked = 14,
        MemoryViewed = 15,
        AlbumOpened = 16,
        WorkshopChoiceShown = 17,
        DecorSlotOpened = 18,
        DailyCareAvailable = 19,
        DailyCareCompleted = 20,
        ChapterCompleted = 21,
        NextRoomTeaserViewed = 22,
        RewardedOfferOpened = 23,
        RewardedOfferOutcome = 24,
        RelaxPassScreenOpened = 25
    }

    /// <summary>
    /// Typed selection causes that can be emitted by the decor surfaces.
    /// This keeps new workshop events from relying on arbitrary source text.
    /// </summary>
    public enum DecorationSelectionSource
    {
        Owned = 0,
        Purchase = 1,
        Rewarded = 2,
        RelaxPass = 3,
        Migration = 4
    }

    /// <summary>
    /// Provider-neutral, immutable soft-launch event. Optional fields use
    /// empty strings and -1 rather than dictionaries, keeping call sites typed
    /// and avoiding boxing in gameplay code.
    /// </summary>
    public readonly struct ProductAnalyticsEvent
    {
        private ProductAnalyticsEvent(
            ProductEventKind kind,
            string levelId,
            LevelType levelType,
            int levelIndex,
            string chapterId,
            int stageIndex,
            int stageCount,
            double durationSeconds,
            int undoCount,
            int progressCurrent,
            int progressTotal,
            int tokenDelta,
            int tokenBalance,
            string itemId,
            string source,
            string locale,
            string appVersion,
            bool flag)
            : this(
                kind,
                levelId,
                levelType,
                levelIndex,
                chapterId,
                stageIndex,
                stageCount,
                durationSeconds,
                undoCount,
                progressCurrent,
                progressTotal,
                tokenDelta,
                tokenBalance,
                itemId,
                source,
                locale,
                appVersion,
                flag,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false)
        {
        }

        private ProductAnalyticsEvent(
            ProductEventKind kind,
            string levelId,
            LevelType levelType,
            int levelIndex,
            string chapterId,
            int stageIndex,
            int stageCount,
            double durationSeconds,
            int undoCount,
            int progressCurrent,
            int progressTotal,
            int tokenDelta,
            int tokenBalance,
            string itemId,
            string source,
            string locale,
            string appVersion,
            bool flag,
            string beatId,
            string slotId,
            string variantId,
            string careId,
            LevelLaunchSource? launchSource,
            AdShowOutcome? outcome,
            DecorationSelectionSource? decorationSource,
            bool firstView)
        {
            Kind = kind;
            LevelId = levelId ?? string.Empty;
            LevelType = levelType;
            LevelIndex = levelIndex;
            ChapterId = chapterId ?? string.Empty;
            StageIndex = stageIndex;
            StageCount = stageCount;
            DurationSeconds = Math.Max(0d, durationSeconds);
            UndoCount = Math.Max(0, undoCount);
            ProgressCurrent = progressCurrent;
            ProgressTotal = progressTotal;
            TokenDelta = tokenDelta;
            TokenBalance = tokenBalance;
            ItemId = itemId ?? string.Empty;
            Source = source ?? string.Empty;
            Locale = locale ?? string.Empty;
            AppVersion = appVersion ?? string.Empty;
            Flag = flag;
            BeatId = beatId ?? string.Empty;
            SlotId = slotId ?? string.Empty;
            VariantId = variantId ?? string.Empty;
            CareId = careId ?? string.Empty;
            LaunchSource = launchSource;
            Outcome = outcome;
            DecorationSource = decorationSource;
            FirstView = firstView;
        }

        public ProductEventKind Kind { get; }

        public string EventName => GetEventName(Kind);

        public string LevelId { get; }

        public LevelType LevelType { get; }

        public int LevelIndex { get; }

        public string ChapterId { get; }

        public int StageIndex { get; }

        public int StageCount { get; }

        public double DurationSeconds { get; }

        public int UndoCount { get; }

        public int ProgressCurrent { get; }

        public int ProgressTotal { get; }

        public int TokenDelta { get; }

        public int TokenBalance { get; }

        public string ItemId { get; }

        public string Source { get; }

        public string Locale { get; }

        public string AppVersion { get; }

        public string BeatId { get; }

        public string SlotId { get; }

        public string VariantId { get; }

        public string CareId { get; }

        public LevelLaunchSource? LaunchSource { get; }

        public AdShowOutcome? Outcome { get; }

        public DecorationSelectionSource? DecorationSource { get; }

        public bool FirstView { get; }

        /// <summary>
        /// Event-specific boolean. On level completion it means that this was
        /// the first rewarded completion; on music events it is the new state.
        /// Provider adapters should map it to a semantic event parameter.
        /// </summary>
        public bool Flag { get; }

        public static ProductAnalyticsEvent SessionStarted(
            string locale,
            string appVersion,
            int completedLevelCount,
            int levelCount)
        {
            return new ProductAnalyticsEvent(
                ProductEventKind.SessionStarted,
                string.Empty,
                default,
                -1,
                string.Empty,
                -1,
                0,
                0d,
                0,
                Math.Max(0, completedLevelCount),
                Math.Max(0, levelCount),
                0,
                0,
                string.Empty,
                string.Empty,
                locale,
                appVersion,
                false);
        }

        public static ProductAnalyticsEvent LevelStarted(
            LevelDefinition definition,
            int levelIndex,
            int tokenBalance,
            bool includeRestorationContext = true)
        {
            return CreateLevelEvent(
                ProductEventKind.LevelStarted,
                definition,
                levelIndex,
                0d,
                0,
                0,
                0,
                0,
                tokenBalance,
                false,
                includeRestorationContext);
        }

        public static ProductAnalyticsEvent LevelStarted(
            LevelDefinition definition,
            int levelIndex,
            int tokenBalance,
            LevelLaunchSource launchSource,
            bool includeRestorationContext = true)
        {
            return CreateLevelEvent(
                ProductEventKind.LevelStarted,
                definition,
                levelIndex,
                0d,
                0,
                0,
                0,
                0,
                tokenBalance,
                false,
                includeRestorationContext,
                launchSource);
        }

        public static ProductAnalyticsEvent LevelCompleted(
            LevelDefinition definition,
            int levelIndex,
            double durationSeconds,
            int undoCount,
            int reward,
            int tokenBalance,
            bool firstRewardedCompletion,
            bool includeRestorationContext = true)
        {
            return CreateLevelEvent(
                ProductEventKind.LevelCompleted,
                definition,
                levelIndex,
                durationSeconds,
                undoCount,
                0,
                0,
                Math.Max(0, reward),
                tokenBalance,
                firstRewardedCompletion,
                includeRestorationContext);
        }

        public static ProductAnalyticsEvent LevelAbandoned(
            LevelDefinition definition,
            int levelIndex,
            double durationSeconds,
            int undoCount,
            int progressCurrent,
            int progressTotal,
            bool includeRestorationContext = true)
        {
            return CreateLevelEvent(
                ProductEventKind.LevelAbandoned,
                definition,
                levelIndex,
                durationSeconds,
                undoCount,
                progressCurrent,
                progressTotal,
                0,
                0,
                false,
                includeRestorationContext);
        }

        public static ProductAnalyticsEvent UndoUsed(
            LevelDefinition definition,
            int levelIndex,
            int undoCount,
            int progressCurrent,
            int progressTotal,
            bool includeRestorationContext = true)
        {
            return CreateLevelEvent(
                ProductEventKind.UndoUsed,
                definition,
                levelIndex,
                0d,
                undoCount,
                progressCurrent,
                progressTotal,
                0,
                0,
                false,
                includeRestorationContext);
        }

        public static ProductAnalyticsEvent DecorationPurchased(
            string decorationId,
            int cost,
            int tokenBalance)
        {
            return CreateMetaEvent(
                ProductEventKind.DecorationPurchased,
                decorationId,
                "room",
                -Math.Max(0, cost),
                tokenBalance,
                false);
        }

        public static ProductAnalyticsEvent DecorationSelected(
            string decorationId,
            string source,
            int tokenBalance)
        {
            return CreateMetaEvent(
                ProductEventKind.DecorationSelected,
                decorationId,
                source,
                0,
                tokenBalance,
                false);
        }

        public static ProductAnalyticsEvent DecorationSelected(
            string slotId,
            string variantId,
            DecorationSelectionSource source,
            int tokenBalance)
        {
            return CreateWorkshopEvent(
                ProductEventKind.DecorationSelected,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                tokenBalance,
                string.Empty,
                slotId,
                variantId,
                string.Empty,
                null,
                null,
                source,
                false);
        }

        public static ProductAnalyticsEvent DecorationSelected(
            string slotId,
            string variantId,
            DecorationGrantSource source,
            int tokenBalance)
        {
            return DecorationSelected(
                slotId,
                variantId,
                ToDecorationSelectionSource(source),
                tokenBalance);
        }

        public static ProductAnalyticsEvent WorkshopViewed(string chapterId)
        {
            return CreateWorkshopEvent(
                ProductEventKind.WorkshopViewed,
                string.Empty,
                default,
                -1,
                chapterId,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent RestorationTaskSelected(
            LevelLaunchRequest request)
        {
            return CreateWorkshopEvent(
                ProductEventKind.RestorationTaskSelected,
                request.LevelId,
                default,
                request.LevelIndex,
                request.ChapterId,
                0,
                0,
                request.BeatId,
                string.Empty,
                string.Empty,
                string.Empty,
                request.Source,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent RestorationRevealStarted(
            string beatId)
        {
            return CreateWorkshopEvent(
                ProductEventKind.RestorationRevealStarted,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                beatId,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent RestorationRevealCompleted(
            string beatId)
        {
            return CreateWorkshopEvent(
                ProductEventKind.RestorationRevealCompleted,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                beatId,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent MemoryUnlocked(
            string beatId,
            string memoryId)
        {
            return CreateWorkshopEvent(
                ProductEventKind.MemoryUnlocked,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                beatId,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false,
                memoryId);
        }

        public static ProductAnalyticsEvent MemoryViewed(
            string memoryId,
            bool firstView)
        {
            return CreateWorkshopEvent(
                ProductEventKind.MemoryViewed,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                firstView,
                memoryId);
        }

        public static ProductAnalyticsEvent AlbumOpened()
        {
            return CreateWorkshopEvent(
                ProductEventKind.AlbumOpened,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent WorkshopChoiceShown(string slotId)
        {
            return CreateSlotEvent(
                ProductEventKind.WorkshopChoiceShown,
                slotId);
        }

        public static ProductAnalyticsEvent DecorSlotOpened(string slotId)
        {
            return CreateSlotEvent(
                ProductEventKind.DecorSlotOpened,
                slotId);
        }

        public static ProductAnalyticsEvent DailyCareAvailable(string careId)
        {
            return CreateWorkshopEvent(
                ProductEventKind.DailyCareAvailable,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                careId,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent DailyCareCompleted(
            string careId,
            int reward,
            int tokenBalance)
        {
            return CreateWorkshopEvent(
                ProductEventKind.DailyCareCompleted,
                string.Empty,
                default,
                -1,
                string.Empty,
                Math.Max(0, reward),
                tokenBalance,
                string.Empty,
                string.Empty,
                string.Empty,
                careId,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent ChapterCompleted(
            string chapterId,
            string beatId)
        {
            return CreateWorkshopEvent(
                ProductEventKind.ChapterCompleted,
                string.Empty,
                default,
                -1,
                chapterId,
                0,
                0,
                beatId,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent NextRoomTeaserViewed(
            string chapterId)
        {
            return CreateWorkshopEvent(
                ProductEventKind.NextRoomTeaserViewed,
                string.Empty,
                default,
                -1,
                chapterId,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent RewardedOfferOpened(
            string slotId,
            string variantId)
        {
            return CreateWorkshopEvent(
                ProductEventKind.RewardedOfferOpened,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                string.Empty,
                slotId,
                variantId,
                string.Empty,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent RewardedOfferOutcome(
            string slotId,
            string variantId,
            AdShowOutcome outcome)
        {
            return CreateWorkshopEvent(
                ProductEventKind.RewardedOfferOutcome,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                string.Empty,
                slotId,
                variantId,
                string.Empty,
                null,
                outcome,
                null,
                false);
        }

        public static ProductAnalyticsEvent RelaxPassScreenOpened()
        {
            return CreateWorkshopEvent(
                ProductEventKind.RelaxPassScreenOpened,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false);
        }

        public static ProductAnalyticsEvent ThemeSelected(
            string themeId)
        {
            return CreateMetaEvent(
                ProductEventKind.ThemeSelected,
                themeId,
                "home",
                0,
                0,
                false);
        }

        public static ProductAnalyticsEvent MusicChanged(
            bool enabled)
        {
            return CreateMetaEvent(
                ProductEventKind.MusicChanged,
                string.Empty,
                "home",
                0,
                0,
                enabled);
        }

        public static ProductAnalyticsEvent LocaleChanged(
            string locale)
        {
            return new ProductAnalyticsEvent(
                ProductEventKind.LocaleChanged,
                string.Empty,
                default,
                -1,
                string.Empty,
                -1,
                0,
                0d,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                "settings",
                locale,
                string.Empty,
                false);
        }

        public string ToDebugString()
        {
            var builder = new StringBuilder(256);
            builder.Append("CALMSPACE_ANALYTICS event=");
            builder.Append(EventName);
            Append(builder, "level_id", LevelId);
            if (LevelIndex >= 0)
            {
                Append(builder, "level_index", LevelIndex);
                Append(builder, "level_type", LevelType.ToString());
            }

            Append(builder, "chapter_id", ChapterId);
            if (StageIndex >= 0)
            {
                Append(builder, "stage_number", StageIndex + 1);
                Append(builder, "stage_count", StageCount);
            }

            if (DurationSeconds > 0d)
            {
                builder.Append(" duration_seconds=");
                builder.Append(
                    DurationSeconds.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture));
            }

            if (UndoCount > 0)
            {
                Append(builder, "undo_count", UndoCount);
            }

            if (ProgressTotal > 0)
            {
                Append(builder, "progress_current", ProgressCurrent);
                Append(builder, "progress_total", ProgressTotal);
            }

            if (TokenDelta != 0)
            {
                Append(builder, "token_delta", TokenDelta);
            }

            if (TokenBalance != 0)
            {
                Append(builder, "token_balance", TokenBalance);
            }

            Append(builder, "item_id", ItemId);
            Append(builder, "source", Source);
            Append(builder, "locale", Locale);
            Append(builder, "app_version", AppVersion);
            Append(builder, "beat_id", BeatId);
            Append(builder, "slot_id", SlotId);
            Append(builder, "variant_id", VariantId);
            Append(builder, "care_id", CareId);
            if (LaunchSource.HasValue)
            {
                Append(
                    builder,
                    "launch_source",
                    GetLaunchSourceName(LaunchSource.Value));
            }

            if (Outcome.HasValue)
            {
                Append(builder, "outcome", GetOutcomeName(Outcome.Value));
            }

            if (Kind == ProductEventKind.MemoryViewed)
            {
                Append(builder, "first_view", FirstView ? 1 : 0);
            }

            if (Kind == ProductEventKind.LevelCompleted ||
                Kind == ProductEventKind.MusicChanged)
            {
                Append(builder, "flag", Flag ? 1 : 0);
            }

            return builder.ToString();
        }

        public static string GetEventName(ProductEventKind kind)
        {
            switch (kind)
            {
                case ProductEventKind.SessionStarted:
                    return "session_started";
                case ProductEventKind.LevelStarted:
                    return "level_started";
                case ProductEventKind.LevelCompleted:
                    return "level_completed";
                case ProductEventKind.LevelAbandoned:
                    return "level_abandoned";
                case ProductEventKind.UndoUsed:
                    return "undo_used";
                case ProductEventKind.DecorationPurchased:
                    return "decoration_purchased";
                case ProductEventKind.DecorationSelected:
                    return "decoration_selected";
                case ProductEventKind.ThemeSelected:
                    return "theme_selected";
                case ProductEventKind.MusicChanged:
                    return "music_changed";
                case ProductEventKind.LocaleChanged:
                    return "locale_changed";
                case ProductEventKind.WorkshopViewed:
                    return "workshop_viewed";
                case ProductEventKind.RestorationTaskSelected:
                    return "restoration_task_selected";
                case ProductEventKind.RestorationRevealStarted:
                    return "restoration_reveal_started";
                case ProductEventKind.RestorationRevealCompleted:
                    return "restoration_reveal_completed";
                case ProductEventKind.MemoryUnlocked:
                    return "memory_unlocked";
                case ProductEventKind.MemoryViewed:
                    return "memory_viewed";
                case ProductEventKind.AlbumOpened:
                    return "album_opened";
                case ProductEventKind.WorkshopChoiceShown:
                    return "workshop_choice_shown";
                case ProductEventKind.DecorSlotOpened:
                    return "decor_slot_opened";
                case ProductEventKind.DailyCareAvailable:
                    return "daily_care_available";
                case ProductEventKind.DailyCareCompleted:
                    return "daily_care_completed";
                case ProductEventKind.ChapterCompleted:
                    return "chapter_completed";
                case ProductEventKind.NextRoomTeaserViewed:
                    return "next_room_teaser_viewed";
                case ProductEventKind.RewardedOfferOpened:
                    return "rewarded_offer_opened";
                case ProductEventKind.RewardedOfferOutcome:
                    return "rewarded_offer_outcome";
                case ProductEventKind.RelaxPassScreenOpened:
                    return "relax_pass_screen_opened";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "Unsupported product analytics event.");
            }
        }

        private static ProductAnalyticsEvent CreateLevelEvent(
            ProductEventKind kind,
            LevelDefinition definition,
            int levelIndex,
            double durationSeconds,
            int undoCount,
            int progressCurrent,
            int progressTotal,
            int tokenDelta,
            int tokenBalance,
            bool flag,
            bool includeRestorationContext,
            LevelLaunchSource? launchSource = null)
        {
            string chapterId = string.Empty;
            int stageIndex = -1;
            int stageCount = 0;
            if (includeRestorationContext &&
                definition != null &&
                definition.TryGetRestorationStage(out var stage))
            {
                chapterId = stage.ChapterId;
                stageIndex = stage.StageIndex;
                stageCount = stage.StageCount;
            }

            ProductAnalyticsEvent analyticsEvent = new ProductAnalyticsEvent(
                kind,
                definition?.LevelId,
                definition != null
                    ? definition.Type
                    : default,
                levelIndex,
                chapterId,
                stageIndex,
                stageCount,
                durationSeconds,
                undoCount,
                progressCurrent,
                progressTotal,
                tokenDelta,
                tokenBalance,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                flag);
            return launchSource.HasValue
                ? CreateWorkshopEvent(
                    kind,
                    analyticsEvent.LevelId,
                    analyticsEvent.LevelType,
                    analyticsEvent.LevelIndex,
                    analyticsEvent.ChapterId,
                    analyticsEvent.TokenDelta,
                    analyticsEvent.TokenBalance,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    launchSource,
                    null,
                    null,
                    analyticsEvent.Flag,
                    analyticsEvent.ItemId,
                    analyticsEvent.DurationSeconds,
                    analyticsEvent.UndoCount,
                    analyticsEvent.ProgressCurrent,
                    analyticsEvent.ProgressTotal,
                    analyticsEvent.StageIndex,
                    analyticsEvent.StageCount)
                : analyticsEvent;
        }

        private static ProductAnalyticsEvent CreateMetaEvent(
            ProductEventKind kind,
            string itemId,
            string source,
            int tokenDelta,
            int tokenBalance,
            bool flag)
        {
            return new ProductAnalyticsEvent(
                kind,
                string.Empty,
                default,
                -1,
                string.Empty,
                -1,
                0,
                0d,
                0,
                0,
                0,
                tokenDelta,
                tokenBalance,
                itemId,
                source,
                string.Empty,
                string.Empty,
                flag);
        }

        private static ProductAnalyticsEvent CreateSlotEvent(
            ProductEventKind kind,
            string slotId)
        {
            return CreateWorkshopEvent(
                kind,
                string.Empty,
                default,
                -1,
                string.Empty,
                0,
                0,
                string.Empty,
                slotId,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                false);
        }

        private static ProductAnalyticsEvent CreateWorkshopEvent(
            ProductEventKind kind,
            string levelId,
            LevelType levelType,
            int levelIndex,
            string chapterId,
            int tokenDelta,
            int tokenBalance,
            string beatId,
            string slotId,
            string variantId,
            string careId,
            LevelLaunchSource? launchSource,
            AdShowOutcome? outcome,
            DecorationSelectionSource? decorationSource,
            bool firstView,
            string itemId = "",
            double durationSeconds = 0d,
            int undoCount = 0,
            int progressCurrent = 0,
            int progressTotal = 0,
            int stageIndex = -1,
            int stageCount = 0)
        {
            return new ProductAnalyticsEvent(
                kind,
                levelId,
                levelType,
                levelIndex,
                chapterId,
                stageIndex,
                stageCount,
                durationSeconds,
                undoCount,
                progressCurrent,
                progressTotal,
                tokenDelta,
                tokenBalance,
                itemId,
                decorationSource.HasValue
                    ? GetDecorationSourceName(decorationSource.Value)
                    : string.Empty,
                string.Empty,
                string.Empty,
                false,
                beatId,
                slotId,
                variantId,
                careId,
                launchSource,
                outcome,
                decorationSource,
                firstView);
        }

        private static DecorationSelectionSource ToDecorationSelectionSource(
            DecorationGrantSource source)
        {
            switch (source)
            {
                case DecorationGrantSource.Rewarded:
                    return DecorationSelectionSource.Rewarded;
                case DecorationGrantSource.RelaxPass:
                    return DecorationSelectionSource.RelaxPass;
                case DecorationGrantSource.Migration:
                    return DecorationSelectionSource.Migration;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(source),
                        source,
                        "Unsupported decoration grant source.");
            }
        }

        private static string GetDecorationSourceName(
            DecorationSelectionSource source)
        {
            switch (source)
            {
                case DecorationSelectionSource.Owned:
                    return "owned";
                case DecorationSelectionSource.Purchase:
                    return "purchase";
                case DecorationSelectionSource.Rewarded:
                    return "rewarded";
                case DecorationSelectionSource.RelaxPass:
                    return "relax_pass";
                case DecorationSelectionSource.Migration:
                    return "migration";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(source),
                        source,
                        "Unsupported decoration selection source.");
            }
        }

        private static string GetLaunchSourceName(
            LevelLaunchSource source)
        {
            switch (source)
            {
                case LevelLaunchSource.Workshop:
                    return "workshop";
                case LevelLaunchSource.Catalog:
                    return "catalog";
                case LevelLaunchSource.DailyCare:
                    return "daily_care";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(source),
                        source,
                        "Unsupported level launch source.");
            }
        }

        private static string GetOutcomeName(AdShowOutcome outcome)
        {
            switch (outcome)
            {
                case AdShowOutcome.Blocked:
                    return "blocked";
                case AdShowOutcome.Completed:
                    return "completed";
                case AdShowOutcome.Closed:
                    return "closed";
                case AdShowOutcome.Failed:
                    return "failed";
                case AdShowOutcome.Unavailable:
                    return "unavailable";
                case AdShowOutcome.Cancelled:
                    return "cancelled";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(outcome),
                        outcome,
                        "Unsupported rewarded offer outcome.");
            }
        }

        private static void Append(
            StringBuilder builder,
            string key,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            builder.Append(' ');
            builder.Append(key);
            builder.Append('=');
            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                builder.Append(
                    char.IsWhiteSpace(character)
                        ? '_'
                        : character);
            }
        }

        private static void Append(
            StringBuilder builder,
            string key,
            int value)
        {
            builder.Append(' ');
            builder.Append(key);
            builder.Append('=');
            builder.Append(value);
        }
    }

    public interface IProductAnalytics
    {
        void Track(in ProductAnalyticsEvent analyticsEvent);
    }

    public interface IProductAnalyticsSink
    {
        void Send(in ProductAnalyticsEvent analyticsEvent);
    }

    /// <summary>
    /// Analytics is observational and must never interrupt the calming
    /// experience. Provider failures are isolated and reported once.
    /// </summary>
    public sealed class SafeProductAnalyticsService :
        IProductAnalytics
    {
        private readonly IProductAnalyticsSink _sink;
        private bool _reportedFailure;

        public SafeProductAnalyticsService(
            IProductAnalyticsSink sink)
        {
            _sink = sink ??
                throw new ArgumentNullException(nameof(sink));
        }

        public void Track(
            in ProductAnalyticsEvent analyticsEvent)
        {
            if (_reportedFailure)
            {
                return;
            }

            try
            {
                _sink.Send(in analyticsEvent);
            }
            catch (Exception exception)
            {
                _reportedFailure = true;
                Debug.LogWarning(
                    "Calm Space analytics provider was isolated after an " +
                    "error: " +
                    exception.Message);
            }
        }
    }

    /// <summary>
    /// Makes the event stream inspectable in Editor and development APK logs.
    /// Release builds collect nothing until a consent-aware production adapter
    /// is selected and registered in the composition root.
    /// </summary>
    public sealed class DevelopmentProductAnalyticsSink :
        IProductAnalyticsSink
    {
        public void Send(
            in ProductAnalyticsEvent analyticsEvent)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(analyticsEvent.ToDebugString());
#endif
        }
    }
}
