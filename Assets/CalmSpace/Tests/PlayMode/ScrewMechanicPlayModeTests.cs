using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using CalmSpace.Audio;
using CalmSpace.Core;
using CalmSpace.Fasteners;
using CalmSpace.Haptics;
using CalmSpace.Input;
using CalmSpace.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CalmSpace.Tests.PlayMode
{
    public sealed class ScrewMechanicPlayModeTests
    {
        [UnityTest]
        public IEnumerator PanelStaysLockedUntilTheLastScrewIsOut()
        {
            ScrewFixture fixture = ScrewFixture.Create("Locked", 2);
            yield return null;

            Assert.That(fixture.Panel.IsReleased, Is.False);
            Assert.That(fixture.PlateCollider.enabled, Is.False);
            Assert.That(fixture.Item.enabled, Is.False);

            fixture.TurnOutScrew(0);
            Assert.That(
                fixture.Panel.RemovedScrewCount,
                Is.EqualTo(1));
            Assert.That(
                fixture.Panel.IsReleased,
                Is.False,
                "One screw left must still hold the panel down.");
            Assert.That(fixture.PlateCollider.enabled, Is.False);

            fixture.TurnOutScrew(1);
            Assert.That(fixture.Panel.IsReleased, Is.True);
            Assert.That(
                fixture.PlateCollider.enabled,
                Is.True,
                "A freed panel has to take the raycast again.");
            Assert.That(fixture.Item.enabled, Is.True);

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LiftingTheFingerFreezesAScrewWithoutResettingIt()
        {
            ScrewFixture fixture = ScrewFixture.Create("Paused", 1);
            yield return null;

            ScrewController screw = fixture.Screws[0];
            Assert.That(screw.BeginHold(), Is.True);
            screw.ContinueHold(0.30f);
            float held = screw.Progress;
            Assert.That(held, Is.GreaterThan(0f));
            Assert.That(screw.IsRemoved, Is.False);

            screw.EndHold();
            screw.ContinueHold(1f);
            Assert.That(
                screw.Progress,
                Is.EqualTo(held),
                "A released screw must not keep turning.");
            Assert.That(screw.IsRemoved, Is.False);

            Assert.That(screw.BeginHold(), Is.True);
            screw.ContinueHold(0.30f);
            Assert.That(
                screw.Progress,
                Is.GreaterThan(held),
                "Picking the screw back up resumes where it stopped.");

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProgressCountsScrewsAndPanelsTogether()
        {
            ScrewFixture fixture =
                ScrewFixture.Create("Progress", 2);
            yield return null;

            var reported = new List<(int Current, int Total)>();
            fixture.Level.ProgressChanged += (current, total) =>
                reported.Add((current, total));

            Assert.That(fixture.Level.ScrewCount, Is.EqualTo(2));

            fixture.TurnOutScrew(0);
            fixture.TurnOutScrew(1);

            Assert.That(reported.Count, Is.EqualTo(2));
            Assert.That(reported[0], Is.EqualTo((1, 3)));
            Assert.That(
                reported[1],
                Is.EqualTo((2, 3)),
                "Two screws out of two screws plus one panel.");
            Assert.That(
                fixture.Level.State,
                Is.EqualTo(LevelState.Active),
                "The panel still has to be put away.");

            fixture.Level.OnItemPlaced(fixture.Item);
            Assert.That(reported[reported.Count - 1],
                Is.EqualTo((3, 3)));

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpeningProgressMatchesTheReportedPair()
        {
            ScrewFixture fixture = ScrewFixture.Create("Opening", 2);
            yield return null;

            // The HUD reads this pair before any event fires. It used to
            // read the item count instead, so a screw level opened on
            // "0 / 2" and then jumped to "1 / 3".
            Assert.That(
                fixture.Level.ProgressCurrent,
                Is.EqualTo(0));
            Assert.That(
                fixture.Level.ProgressTotal,
                Is.EqualTo(3),
                "Two screws plus one panel.");

            var reported = (Current: -1, Total: -1);
            fixture.Level.ProgressChanged += (current, total) =>
                reported = (current, total);
            fixture.TurnOutScrew(0);

            Assert.That(
                reported,
                Is.EqualTo(
                    (fixture.Level.ProgressCurrent,
                        fixture.Level.ProgressTotal)),
                "The event and the properties must never disagree.");

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelCompletesOnlyAfterScrewsAndPanel()
        {
            ScrewFixture fixture =
                ScrewFixture.Create("Complete", 2);
            yield return null;

            var completed = false;
            fixture.Level.LevelCompleted += _ => completed = true;

            fixture.Level.OnItemPlaced(fixture.Item);
            Assert.That(
                completed,
                Is.False,
                "Placing the panel cannot finish a fastened level.");

            fixture.TurnOutScrew(0);
            Assert.That(completed, Is.False);

            fixture.TurnOutScrew(1);
            yield return null;

            Assert.That(completed, Is.True);
            Assert.That(
                fixture.Level.State,
                Is.EqualTo(LevelState.Completed));

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovalStillReleasesPanelWhenFeedbackThrows()
        {
            ScrewFixture fixture = ScrewFixture.Create(
                "Feedback Failure",
                1,
                new ThrowingAudio());
            yield return null;

            LogAssert.Expect(
                LogType.Exception,
                new Regex("audio feedback failed"));
            Assert.DoesNotThrow(() => fixture.TurnOutScrew(0));

            Assert.That(fixture.Screws[0].IsRemoved, Is.True);
            Assert.That(
                fixture.Panel.RemovedScrewCount,
                Is.EqualTo(1));
            Assert.That(
                fixture.Panel.IsReleased,
                Is.True,
                "Optional feedback cannot prevent panel release.");

            fixture.Destroy();
            yield return null;
        }

        private sealed class ScrewFixture
        {
            private readonly GameObject _root;

            private ScrewFixture(
                GameObject root,
                ScrewPuzzleLevel level,
                FastenerPanel panel,
                ItemSnapController item,
                Collider plateCollider,
                ScrewController[] screws)
            {
                _root = root;
                Level = level;
                Panel = panel;
                Item = item;
                PlateCollider = plateCollider;
                Screws = screws;
            }

            public ScrewPuzzleLevel Level { get; }

            public FastenerPanel Panel { get; }

            public ItemSnapController Item { get; }

            public Collider PlateCollider { get; }

            public ScrewController[] Screws { get; }

            public static ScrewFixture Create(
                string prefix,
                int screwCount,
                IAsmrAudioService screwAudio = null)
            {
                // Built inactive so every Awake sees a fully wired
                // hierarchy — FastenerPanel counts its screws there.
                var root = new GameObject(prefix + " Level");
                root.SetActive(false);

                ScrewPuzzleLevel level =
                    root.AddComponent<ScrewPuzzleLevel>();

                var targetObject =
                    new GameObject(prefix + " Target");
                targetObject.transform.SetParent(
                    root.transform,
                    false);
                targetObject.transform.position =
                    new Vector3(0f, 0f, -1f);

                var panelObject = new GameObject(prefix + " Panel");
                panelObject.transform.SetParent(root.transform, false);
                BoxCollider plateCollider =
                    panelObject.AddComponent<BoxCollider>();
                ItemSnapController item =
                    panelObject.AddComponent<ItemSnapController>();
                SetPrivateField(
                    item,
                    "_snapTarget",
                    targetObject.transform);
                SetPrivateField(
                    item,
                    "_positionSnapThreshold",
                    0.5f);
                SetPrivateField(item, "_snapDurationSeconds", 0f);
                SetPrivateField(item, "_returnDurationSeconds", 0f);
                item.Construct(
                    new SilentHaptics(),
                    new SilentAudio(),
                    new PresentationActivityCoordinator());

                FastenerPanel panel =
                    panelObject.AddComponent<FastenerPanel>();

                var screws = new ScrewController[screwCount];
                for (var index = 0; index < screwCount; index++)
                {
                    var screwObject = new GameObject(
                        prefix + " Screw " + index);
                    screwObject.transform.SetParent(
                        root.transform,
                        false);
                    screwObject.AddComponent<BoxCollider>();
                    ScrewController screw =
                        screwObject.AddComponent<ScrewController>();
                    SetPrivateField(screw, "_panel", panel);
                    SetPrivateField(screw, "_turnsRequired", 2);
                    SetPrivateField(screw, "_secondsPerTurn", 0.25f);
                    SetPrivateField(
                        screw,
                        "_settleDurationSeconds",
                        0f);
                    screw.Construct(
                        new SilentHaptics(),
                        screwAudio ?? new SilentAudio());
                    screws[index] = screw;
                }

                SetPrivateField(panel, "_screws", screws);
                SetPrivateField(
                    panel,
                    "_lockedColliders",
                    new Collider[] { plateCollider });
                SetPrivateField(panel, "_item", item);

                var definition =
                    ScriptableObject.CreateInstance<LevelDefinition>();
                SetPrivateField(definition, "_levelId", prefix);
                SetPrivateField(
                    definition,
                    "_levelType",
                    LevelType.ScrewPuzzle);

                root.SetActive(true);
                level.Construct(
                    new PresentationActivityCoordinator());
                Assert.That(
                    level.InitLevel(definition),
                    Is.True,
                    "The screw level failed to initialise.");

                return new ScrewFixture(
                    root,
                    level,
                    panel,
                    item,
                    plateCollider,
                    screws);
            }

            public void TurnOutScrew(int index)
            {
                ScrewController screw = Screws[index];
                Assert.That(screw.BeginHold(), Is.True);

                // Feed the hold in slices rather than one jump so the
                // per-turn boundaries are actually exercised.
                for (var step = 0; step < 12; step++)
                {
                    screw.ContinueHold(0.05f);
                }

                Assert.That(screw.IsRemoved, Is.True);
            }

            public void Destroy()
            {
                Object.Destroy(_root);
            }
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target
                .GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class SilentHaptics : IHapticService
        {
            public bool IsSupported => true;

            public void PlayDragTick(float intensity)
            {
            }

            public void PlaySnap()
            {
            }

            public void Cancel()
            {
            }
        }

        private sealed class SilentAudio : IAsmrAudioService
        {
            public bool IsAvailable => true;

            public void PlaySnap(Vector3 worldPosition)
            {
            }
        }

        private sealed class ThrowingAudio : IAsmrAudioService
        {
            public bool IsAvailable => true;

            public void PlaySnap(Vector3 worldPosition)
            {
                throw new System.InvalidOperationException(
                    "audio feedback failed");
            }
        }
    }
}
