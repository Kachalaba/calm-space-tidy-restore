using System;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Analytics;
using CalmSpace.Demo;
using CalmSpace.Levels;
using CalmSpace.Monetization;
using CalmSpace.Workshop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CalmSpace.Tests.EditMode
{
    public sealed class ProductAnalyticsTests
    {
        private readonly List<LevelDefinition> _created =
            new List<LevelDefinition>();

        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < _created.Count; index++)
            {
                if (_created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        _created[index]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void LevelCompletionCarriesFunnelAndChapterContext()
        {
            LevelDefinition definition = CreateDefinition();
            ProductAnalyticsEvent analyticsEvent =
                ProductAnalyticsEvent.LevelCompleted(
                    definition,
                    4,
                    12.5d,
                    2,
                    15,
                    45,
                    true);

            Assert.That(
                analyticsEvent.EventName,
                Is.EqualTo("level_completed"));
            Assert.That(
                analyticsEvent.LevelId,
                Is.EqualTo("05-fastener-tray"));
            Assert.That(
                analyticsEvent.LevelType,
                Is.EqualTo(LevelType.ScrewPuzzle));
            Assert.That(analyticsEvent.LevelIndex, Is.EqualTo(4));
            Assert.That(
                analyticsEvent.ChapterId,
                Is.EqualTo("cozy-workshop"));
            Assert.That(analyticsEvent.StageIndex, Is.EqualTo(2));
            Assert.That(analyticsEvent.StageCount, Is.EqualTo(6));
            Assert.That(
                analyticsEvent.DurationSeconds,
                Is.EqualTo(12.5d));
            Assert.That(analyticsEvent.UndoCount, Is.EqualTo(2));
            Assert.That(analyticsEvent.TokenDelta, Is.EqualTo(15));
            Assert.That(analyticsEvent.TokenBalance, Is.EqualTo(45));
            Assert.That(analyticsEvent.Flag, Is.True);
            Assert.That(
                analyticsEvent.ToDebugString(),
                Does.Contain("chapter_id=cozy-workshop"));

            ProductAnalyticsEvent standaloneFallback =
                ProductAnalyticsEvent.LevelCompleted(
                    definition,
                    4,
                    12.5d,
                    2,
                    0,
                    30,
                    false,
                    includeRestorationContext: false);
            Assert.That(standaloneFallback.ChapterId, Is.Empty);
            Assert.That(standaloneFallback.StageIndex, Is.EqualTo(-1));
        }

        [Test]
        public void SafeServiceForwardsTypedEvent()
        {
            var sink = new CapturingSink();
            var service = new SafeProductAnalyticsService(sink);
            ProductAnalyticsEvent analyticsEvent =
                ProductAnalyticsEvent.SessionStarted(
                    "Ukrainian",
                    "1.0.0",
                    2,
                    8);

            service.Track(in analyticsEvent);

            Assert.That(sink.Count, Is.EqualTo(1));
            Assert.That(
                sink.Last.EventName,
                Is.EqualTo("session_started"));
            Assert.That(sink.Last.Locale, Is.EqualTo("Ukrainian"));
            Assert.That(sink.Last.ProgressCurrent, Is.EqualTo(2));
            Assert.That(sink.Last.ProgressTotal, Is.EqualTo(8));
        }

        [Test]
        public void ProviderFailureIsIsolatedAfterFirstAttempt()
        {
            var sink = new ThrowingSink();
            var service = new SafeProductAnalyticsService(sink);
            ProductAnalyticsEvent analyticsEvent =
                ProductAnalyticsEvent.MusicChanged(true);
            LogAssert.Expect(
                LogType.Warning,
                "Calm Space analytics provider was isolated after an " +
                "error: test provider failure");

            Assert.DoesNotThrow(
                () => service.Track(in analyticsEvent));
            Assert.DoesNotThrow(
                () => service.Track(in analyticsEvent));

            Assert.That(sink.Count, Is.EqualTo(1));
        }

        [Test]
        public void EventKindsKeepAllNumericValuesAndExactNames()
        {
            var expectedNames = new[]
            {
                "session_started",
                "level_started",
                "level_completed",
                "level_abandoned",
                "undo_used",
                "decoration_purchased",
                "decoration_selected",
                "theme_selected",
                "music_changed",
                "locale_changed",
                "workshop_viewed",
                "restoration_task_selected",
                "restoration_reveal_started",
                "restoration_reveal_completed",
                "memory_unlocked",
                "memory_viewed",
                "album_opened",
                "workshop_choice_shown",
                "decor_slot_opened",
                "daily_care_available",
                "daily_care_completed",
                "chapter_completed",
                "next_room_teaser_viewed",
                "rewarded_offer_opened",
                "rewarded_offer_outcome",
                "relax_pass_screen_opened"
            };

            for (var value = 0; value < expectedNames.Length; value++)
            {
                ProductEventKind kind = (ProductEventKind)value;

                Assert.That((int)kind, Is.EqualTo(value));
                Assert.That(
                    () => ProductAnalyticsEvent.GetEventName(kind),
                    Throws.Nothing);
                Assert.That(
                    ProductAnalyticsEvent.GetEventName(kind),
                    Is.EqualTo(expectedNames[value]));
            }
        }

        [Test]
        public void LevelStartedUsesTypedLaunchSourceAndStableSerialization()
        {
            LevelDefinition definition = CreateDefinition();
            ProductAnalyticsEvent analyticsEvent = InvokeFactory(
                "LevelStarted",
                new[]
                {
                    typeof(LevelDefinition),
                    typeof(int),
                    typeof(int),
                    typeof(LevelLaunchSource),
                    typeof(bool)
                },
                definition,
                4,
                45,
                LevelLaunchSource.Workshop,
                true);

            Assert.That(
                GetProperty<LevelLaunchSource?>(
                    analyticsEvent,
                    "LaunchSource"),
                Is.EqualTo(LevelLaunchSource.Workshop));
            Assert.That(
                analyticsEvent.ToDebugString(),
                Does.Contain("launch_source=workshop"));
        }

        [Test]
        public void DecorationSelectionUsesTypedSlotVariantAndSourceContext()
        {
            Type decorationSourceType = RequireAnalyticsType(
                "DecorationSelectionSource");
            ProductAnalyticsEvent owned = InvokeFactory(
                "DecorationSelected",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    decorationSourceType,
                    typeof(int)
                },
                "shelf",
                "shelf-lamp",
                Enum.ToObject(decorationSourceType, 0),
                45);

            Assert.That(owned.EventName, Is.EqualTo("decoration_selected"));
            Assert.That(GetProperty<string>(owned, "SlotId"), Is.EqualTo("shelf"));
            Assert.That(
                GetProperty<string>(owned, "VariantId"),
                Is.EqualTo("shelf-lamp"));
            Assert.That(
                GetPropertyType(owned, "DecorationSource"),
                Is.EqualTo(typeof(Nullable<>).MakeGenericType(
                    decorationSourceType)));
            Assert.That(
                GetProperty<object>(owned, "DecorationSource").ToString(),
                Is.EqualTo("Owned"));
            Assert.That(owned.Source, Is.EqualTo("owned"));
            Assert.That(
                owned.ToDebugString(),
                Does.Contain("slot_id=shelf")
                    .And.Contain("variant_id=shelf-lamp")
                    .And.Contain("source=owned"));

            ProductAnalyticsEvent purchased = InvokeFactory(
                "DecorationSelected",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    decorationSourceType,
                    typeof(int)
                },
                "shelf",
                "shelf-lamp",
                Enum.ToObject(decorationSourceType, 1),
                40);
            Assert.That(purchased.Source, Is.EqualTo("purchase"));

            ProductAnalyticsEvent rewarded = InvokeFactory(
                "DecorationSelected",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(DecorationGrantSource),
                    typeof(int)
                },
                "shelf",
                "shelf-lamp",
                DecorationGrantSource.Rewarded,
                45);
            Assert.That(rewarded.Source, Is.EqualTo("rewarded"));
        }

        [Test]
        public void WorkshopFactoriesCarryOnlyTypedStableContext()
        {
            var request = new LevelLaunchRequest(
                "03-tea-drawer",
                2,
                LevelLaunchSource.Workshop,
                "cozy-workshop",
                "tea-drawer");

            AssertFactory(
                "WorkshopViewed",
                new[] { typeof(string) },
                new object[] { "cozy-workshop" },
                "workshop_viewed");
            ProductAnalyticsEvent selected = InvokeFactory(
                "RestorationTaskSelected",
                new[] { typeof(LevelLaunchRequest) },
                request);
            Assert.That(
                selected.EventName,
                Is.EqualTo("restoration_task_selected"));
            Assert.That(selected.LevelId, Is.EqualTo("03-tea-drawer"));
            Assert.That(selected.LevelIndex, Is.EqualTo(2));
            Assert.That(selected.ChapterId, Is.EqualTo("cozy-workshop"));
            Assert.That(GetProperty<string>(selected, "BeatId"), Is.EqualTo("tea-drawer"));
            Assert.That(
                GetProperty<LevelLaunchSource?>(selected, "LaunchSource"),
                Is.EqualTo(LevelLaunchSource.Workshop));
            Assert.That(
                selected.ToDebugString(),
                Does.Contain("launch_source=workshop")
                    .And.Contain("beat_id=tea-drawer"));
            AssertFactory(
                "RestorationRevealStarted",
                new[] { typeof(string) },
                new object[] { "tea-drawer" },
                "restoration_reveal_started",
                beatId: "tea-drawer");
            AssertFactory(
                "RestorationRevealCompleted",
                new[] { typeof(string) },
                new object[] { "tea-drawer" },
                "restoration_reveal_completed",
                beatId: "tea-drawer");
            AssertFactory(
                "MemoryUnlocked",
                new[] { typeof(string), typeof(string) },
                new object[] { "tea-drawer", "tea-postcard" },
                "memory_unlocked",
                beatId: "tea-drawer",
                itemId: "tea-postcard");
            AssertFactory(
                "MemoryViewed",
                new[] { typeof(string), typeof(bool) },
                new object[] { "tea-postcard", true },
                "memory_viewed",
                itemId: "tea-postcard",
                firstView: true);
            AssertFactory(
                "AlbumOpened",
                Type.EmptyTypes,
                Array.Empty<object>(),
                "album_opened");
            AssertFactory(
                "WorkshopChoiceShown",
                new[] { typeof(string) },
                new object[] { "shelf" },
                "workshop_choice_shown",
                slotId: "shelf");
            AssertFactory(
                "DecorSlotOpened",
                new[] { typeof(string) },
                new object[] { "shelf" },
                "decor_slot_opened",
                slotId: "shelf");
            AssertFactory(
                "DailyCareAvailable",
                new[] { typeof(string) },
                new object[] { "tea-care" },
                "daily_care_available",
                careId: "tea-care");
            AssertFactory(
                "DailyCareCompleted",
                new[] { typeof(string), typeof(int), typeof(int) },
                new object[] { "tea-care", 5, 45 },
                "daily_care_completed",
                careId: "tea-care");
            AssertFactory(
                "ChapterCompleted",
                new[] { typeof(string), typeof(string) },
                new object[] { "cozy-workshop", "dusty-window" },
                "chapter_completed",
                beatId: "dusty-window");
            AssertFactory(
                "NextRoomTeaserViewed",
                new[] { typeof(string) },
                new object[] { "cozy-workshop" },
                "next_room_teaser_viewed");
            ProductAnalyticsEvent opened = InvokeFactory(
                "RewardedOfferOpened",
                new[] { typeof(string), typeof(string) },
                "shelf",
                "shelf-lamp");
            Assert.That(opened.EventName, Is.EqualTo("rewarded_offer_opened"));
            Assert.That(GetProperty<string>(opened, "SlotId"), Is.EqualTo("shelf"));
            Assert.That(
                GetProperty<string>(opened, "VariantId"),
                Is.EqualTo("shelf-lamp"));
            ProductAnalyticsEvent outcome = InvokeFactory(
                "RewardedOfferOutcome",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(AdShowOutcome)
                },
                "shelf",
                "shelf-lamp",
                AdShowOutcome.Completed);
            Assert.That(outcome.EventName, Is.EqualTo("rewarded_offer_outcome"));
            Assert.That(
                GetProperty<AdShowOutcome?>(outcome, "Outcome"),
                Is.EqualTo(AdShowOutcome.Completed));
            Assert.That(
                outcome.ToDebugString(),
                Does.Contain("outcome=completed")
                    .And.Not.Contain("provider_message"));
            AssertFactory(
                "RelaxPassScreenOpened",
                Type.EmptyTypes,
                Array.Empty<object>(),
                "relax_pass_screen_opened");
        }

        private static void AssertFactory(
            string factoryName,
            Type[] parameterTypes,
            object[] arguments,
            string eventName,
            string beatId = "",
            string slotId = "",
            string careId = "",
            string itemId = "",
            bool? firstView = null)
        {
            ProductAnalyticsEvent analyticsEvent = InvokeFactory(
                factoryName,
                parameterTypes,
                arguments);

            Assert.That(analyticsEvent.EventName, Is.EqualTo(eventName));
            Assert.That(GetProperty<string>(analyticsEvent, "BeatId"), Is.EqualTo(beatId));
            Assert.That(GetProperty<string>(analyticsEvent, "SlotId"), Is.EqualTo(slotId));
            Assert.That(GetProperty<string>(analyticsEvent, "CareId"), Is.EqualTo(careId));
            if (!string.IsNullOrEmpty(itemId))
            {
                Assert.That(analyticsEvent.ItemId, Is.EqualTo(itemId));
            }

            if (firstView.HasValue)
            {
                Assert.That(
                    GetProperty<bool>(analyticsEvent, "FirstView"),
                    Is.EqualTo(firstView.Value));
            }
        }

        private static ProductAnalyticsEvent InvokeFactory(
            string factoryName,
            Type[] parameterTypes,
            params object[] arguments)
        {
            MethodInfo factory = typeof(ProductAnalyticsEvent).GetMethod(
                factoryName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            Assert.That(factory, Is.Not.Null, factoryName + " factory is required.");
            return (ProductAnalyticsEvent)factory.Invoke(null, arguments);
        }

        private static T GetProperty<T>(
            ProductAnalyticsEvent analyticsEvent,
            string propertyName)
        {
            PropertyInfo property = typeof(ProductAnalyticsEvent).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, propertyName + " property is required.");
            return (T)property.GetValue(analyticsEvent);
        }

        private static Type GetPropertyType(
            ProductAnalyticsEvent analyticsEvent,
            string propertyName)
        {
            PropertyInfo property = typeof(ProductAnalyticsEvent).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, propertyName + " property is required.");
            return property.PropertyType;
        }

        private static Type RequireAnalyticsType(string typeName)
        {
            Type type = Type.GetType(
                "CalmSpace.Analytics." + typeName + ", CalmSpace.Runtime");
            Assert.That(type, Is.Not.Null, typeName + " type is required.");
            return type;
        }

        private LevelDefinition CreateDefinition()
        {
            LevelDefinition definition =
                ScriptableObject.CreateInstance<LevelDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_levelId").stringValue =
                "05-fastener-tray";
            serialized.FindProperty("_displayName").stringValue =
                "Fastener Tray";
            serialized.FindProperty("_levelType").enumValueIndex =
                (int)LevelType.ScrewPuzzle;
            serialized.FindProperty(
                "_restorationChapterId").stringValue =
                "cozy-workshop";
            serialized.FindProperty(
                "_restorationStageIndex").intValue = 2;
            serialized.FindProperty(
                "_restorationStageCount").intValue = 6;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _created.Add(definition);
            return definition;
        }

        private sealed class CapturingSink :
            IProductAnalyticsSink
        {
            public int Count { get; private set; }

            public ProductAnalyticsEvent Last { get; private set; }

            public void Send(
                in ProductAnalyticsEvent analyticsEvent)
            {
                Count++;
                Last = analyticsEvent;
            }
        }

        private sealed class ThrowingSink :
            IProductAnalyticsSink
        {
            public int Count { get; private set; }

            public void Send(
                in ProductAnalyticsEvent analyticsEvent)
            {
                Count++;
                throw new InvalidOperationException(
                    "test provider failure");
            }
        }
    }
}
