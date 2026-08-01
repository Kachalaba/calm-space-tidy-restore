using System;
using System.Collections.Generic;
using CalmSpace.Analytics;
using CalmSpace.Levels;
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
