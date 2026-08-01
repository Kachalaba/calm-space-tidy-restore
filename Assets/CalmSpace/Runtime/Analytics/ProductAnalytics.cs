using System;
using System.Globalization;
using System.Text;
using CalmSpace.Levels;
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
        LocaleChanged = 9
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
            bool includeRestorationContext)
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

            return new ProductAnalyticsEvent(
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
