using System.Collections;
using System.Reflection;
using CalmSpace.Audio;
using CalmSpace.Cleaning;
using CalmSpace.Core;
using CalmSpace.Haptics;
using CalmSpace.Input;
using CalmSpace.Levels;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CalmSpace.Tests.PlayMode
{
    /// <summary>
    /// Covers stage sequencing with debris-only passes so the flow is tested
    /// without standing up a GPU mask, which batch-mode runs cannot rely on.
    /// </summary>
    public sealed class MultiStageCleaningPlayModeTests
    {
        [UnityTest]
        public IEnumerator OnlyTheFirstPassIsVisibleAtTheStart()
        {
            StageFixture fixture = StageFixture.Create("Start");
            yield return null;

            Assert.That(fixture.Level.StageCount, Is.EqualTo(2));
            Assert.That(fixture.Level.ActiveStageIndex, Is.EqualTo(0));
            Assert.That(
                fixture.Contents[0].activeSelf,
                Is.True);
            Assert.That(
                fixture.Contents[1].activeSelf,
                Is.False,
                "The next pass must stay hidden until it is reached.");
            Assert.That(
                fixture.Debris[1].gameObject.activeSelf,
                Is.False);

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearingAPassRevealsTheNextOne()
        {
            StageFixture fixture = StageFixture.Create("Advance");
            yield return null;

            Assert.That(fixture.PlaceDebris(0), Is.True);
            yield return null;

            Assert.That(
                fixture.Level.CompletedStageCount,
                Is.EqualTo(1));
            Assert.That(
                fixture.Level.ActiveStageIndex,
                Is.EqualTo(1));
            Assert.That(fixture.Contents[1].activeSelf, Is.True);
            Assert.That(
                fixture.Debris[1].gameObject.activeSelf,
                Is.True);
            Assert.That(
                fixture.Level.State,
                Is.EqualTo(LevelState.Active),
                "One pass left means the level is not finished.");

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DebrisProgressIsCurrentWhenTheLevelReportsIt()
        {
            StageFixture fixture = StageFixture.Create("Fresh");
            yield return null;

            // LevelBase hears the item's Placed event before the stage does,
            // so the fraction read during that notification used to be one
            // piece of debris out of date.
            var observed = -1f;
            fixture.Level.ProgressChanged += (_, __) =>
                observed = fixture.Level.ActiveStageFraction;

            Assert.That(fixture.PlaceDebris(0), Is.True);
            yield return null;

            Assert.That(
                observed,
                Is.EqualTo(1f).Within(1e-4f),
                "The only debris of the pass was cleared.");

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProgressReadsInStagesAndCompletesOnTheLast()
        {
            StageFixture fixture = StageFixture.Create("Progress");
            yield return null;

            var completed = false;
            fixture.Level.LevelCompleted += _ => completed = true;

            Assert.That(
                fixture.Level.ActiveTool,
                Is.EqualTo(CleaningToolKind.Hands),
                "Debris comes off by hand before any implement.");
            Assert.That(
                fixture.Level.ActiveStageFraction,
                Is.EqualTo(0f));

            Assert.That(fixture.PlaceDebris(0), Is.True);
            yield return null;
            Assert.That(fixture.PlaceDebris(1), Is.True);
            yield return null;

            Assert.That(
                fixture.Level.CompletedStageCount,
                Is.EqualTo(2));
            Assert.That(completed, Is.True);
            Assert.That(
                fixture.Level.State,
                Is.EqualTo(LevelState.Completed));

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CleanerIsUnavailableUntilEveryDebrisPieceIsPlaced()
        {
            MixedStageFixture fixture =
                MixedStageFixture.Create("Debris Gate", 3);
            yield return null;

            Assert.That(fixture.Stage.IsActivated, Is.True);
            Assert.That(
                fixture.Cleaner.gameObject.activeSelf,
                Is.False,
                "The surface must stay inactive while debris remains.");
            Assert.That(
                fixture.Input.Cleaner,
                Is.Null,
                "Cleaning input must not route strokes to a locked surface.");
            Assert.That(fixture.Level.ActiveCleaner, Is.Null);

            Assert.That(fixture.PlaceDebris(0), Is.True);
            yield return null;
            Assert.That(fixture.Input.Cleaner, Is.Null);
            Assert.That(fixture.Cleaner.gameObject.activeSelf, Is.False);

            Assert.That(fixture.PlaceDebris(1), Is.True);
            yield return null;
            Assert.That(fixture.Input.Cleaner, Is.Null);
            Assert.That(fixture.Cleaner.gameObject.activeSelf, Is.False);

            Assert.That(fixture.PlaceDebris(2), Is.True);
            yield return null;
            Assert.That(fixture.Cleaner.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Input.Cleaner, Is.SameAs(fixture.Cleaner));
            Assert.That(
                fixture.Level.ActiveCleaner,
                Is.SameAs(fixture.Cleaner));

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator UndoingDebrisRestoresProgressAndLocksTheSurface()
        {
            MixedStageFixture fixture =
                MixedStageFixture.Create("Debris Undo", 3);
            yield return null;

            Assert.That(fixture.PlaceDebris(0), Is.True);
            yield return null;
            Assert.That(
                fixture.Level.ActiveStageFraction,
                Is.EqualTo(1f / 6f).Within(1e-4f));

            Assert.That(fixture.Undo(), Is.True);
            yield return null;

            Assert.That(fixture.IsDebrisPlaced(0), Is.False);
            Assert.That(fixture.Stage.ClearedDebrisCount, Is.Zero);
            Assert.That(
                fixture.Level.ActiveStageFraction,
                Is.EqualTo(0f).Within(1e-4f));
            Assert.That(fixture.Cleaner.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Input.Cleaner, Is.Null);

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator UndoKeepsAStageRootCleanerAndDebrisVisible()
        {
            MixedStageFixture fixture =
                MixedStageFixture.Create(
                    "Shared Root Undo",
                    1,
                    cleanerOnStageRoot: true);
            yield return null;

            Assert.That(fixture.PlaceDebris(0), Is.True);
            yield return null;
            fixture.CompleteSurface();
            yield return null;
            Assert.That(fixture.Stage.IsComplete, Is.True);

            Assert.That(fixture.Undo(), Is.True);
            yield return null;

            Assert.That(fixture.StageRoot.activeSelf, Is.True);
            Assert.That(fixture.IsDebrisVisible(0), Is.True);
            Assert.That(fixture.IsDebrisPlaced(0), Is.False);
            Assert.That(fixture.Input.Cleaner, Is.Null);
            Assert.That(fixture.Level.ActiveStageFraction, Is.Zero);

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator UndoAcrossPassesRestoresTheExactEarlierPass()
        {
            StageFixture fixture = StageFixture.Create("Stage Undo");
            yield return null;

            Assert.That(fixture.PlaceDebris(0), Is.True);
            yield return null;
            Assert.That(fixture.PlaceDebris(1), Is.True);
            yield return null;
            Assert.That(fixture.Level.State, Is.EqualTo(
                LevelState.Completed));

            Assert.That(fixture.Undo(), Is.True);
            yield return null;
            Assert.That(fixture.Level.State, Is.EqualTo(LevelState.Active));
            Assert.That(fixture.Level.CompletedStageCount, Is.EqualTo(1));
            Assert.That(fixture.Level.ActiveStageIndex, Is.EqualTo(1));
            Assert.That(fixture.Contents[1].activeSelf, Is.True);

            Assert.That(fixture.Undo(), Is.True);
            yield return null;
            Assert.That(fixture.Level.CompletedStageCount, Is.Zero);
            Assert.That(fixture.Level.ActiveStageIndex, Is.Zero);
            Assert.That(fixture.Contents[0].activeSelf, Is.True);
            Assert.That(fixture.Contents[1].activeSelf, Is.False);

            Assert.That(fixture.PlaceDebris(0), Is.True);
            yield return null;
            Assert.That(fixture.PlaceDebris(1), Is.True);
            yield return null;
            Assert.That(fixture.Level.State, Is.EqualTo(
                LevelState.Completed));

            fixture.Destroy();
            yield return null;
        }

        private sealed class StageFixture
        {
            private readonly GameObject _root;
            private readonly IUndoHistory _undoHistory;

            private StageFixture(
                GameObject root,
                CleaningLevel level,
                GameObject[] contents,
                ItemSnapController[] debris,
                Transform[] targets,
                IUndoHistory undoHistory)
            {
                _root = root;
                Level = level;
                Contents = contents;
                Debris = debris;
                Targets = targets;
                _undoHistory = undoHistory;
            }

            public CleaningLevel Level { get; }

            public GameObject[] Contents { get; }

            public ItemSnapController[] Debris { get; }

            public Transform[] Targets { get; }

            public static StageFixture Create(string prefix)
            {
                var root = new GameObject(prefix + " Level");
                root.SetActive(false);

                CleaningLevel level =
                    root.AddComponent<CleaningLevel>();
                CleaningInputController input =
                    root.AddComponent<CleaningInputController>();
                input.Construct(
                    new PresentationActivityCoordinator());
                SetPrivateField(level, "_cleaningInput", input);
                SetPrivateField(level, "_stageSettleSeconds", 0f);

                var contents = new GameObject[2];
                var debris = new ItemSnapController[2];
                var targets = new Transform[2];
                var stages = new CleaningStage[2];
                var coordinator =
                    new PresentationActivityCoordinator();
                var undoHistory = new LevelSessionUndoHistory();

                for (var index = 0; index < stages.Length; index++)
                {
                    var stageObject = new GameObject(
                        prefix + " Stage " + index);
                    stageObject.transform.SetParent(
                        root.transform,
                        false);
                    CleaningStage stage =
                        stageObject.AddComponent<CleaningStage>();

                    var content = new GameObject("Content");
                    content.transform.SetParent(
                        stageObject.transform,
                        false);
                    contents[index] = content;

                    var targetObject =
                        new GameObject("Bin " + index);
                    targetObject.transform.SetParent(
                        content.transform,
                        false);
                    targetObject.transform.position =
                        new Vector3(index, 0f, -1f);
                    targets[index] = targetObject.transform;

                    var debrisObject =
                        new GameObject("Debris " + index);
                    debrisObject.transform.SetParent(
                        content.transform,
                        false);
                    debrisObject.transform.position =
                        new Vector3(index, 0f, 1f);
                    ItemSnapController item =
                        debrisObject.AddComponent<
                            ItemSnapController>();
                    SetPrivateField(
                        item,
                        "_snapTarget",
                        targetObject.transform);
                    SetPrivateField(
                        item,
                        "_positionSnapThreshold",
                        0.5f);
                    SetPrivateField(
                        item,
                        "_snapDurationSeconds",
                        0f);
                    SetPrivateField(
                        item,
                        "_returnDurationSeconds",
                        0f);
                    item.Construct(
                        new SilentHaptics(),
                        new SilentAudio(),
                        coordinator,
                        undoHistory);
                    debris[index] = item;

                    SetPrivateField(stage, "_stageRoot", content);
                    SetPrivateField(
                        stage,
                        "_debris",
                        new[] { item });
                    SetPrivateField(
                        stage,
                        "_tool",
                        CleaningToolKind.Sponge);
                    stages[index] = stage;
                }

                SetPrivateField(level, "_stages", stages);

                var definition =
                    ScriptableObject.CreateInstance<LevelDefinition>();
                SetPrivateField(definition, "_levelId", prefix);
                SetPrivateField(
                    definition,
                    "_levelType",
                    LevelType.Cleaning);

                root.SetActive(true);
                level.Construct(
                    new PresentationActivityCoordinator());
                Assert.That(
                    level.InitLevel(definition),
                    Is.True,
                    "The staged level failed to initialise.");

                return new StageFixture(
                    root,
                    level,
                    contents,
                    debris,
                    targets,
                    undoHistory);
            }

            public bool PlaceDebris(int index)
            {
                ItemSnapController item = Debris[index];
                Vector3 targetPosition = Targets[index].position;
                Ray startRay = RayAt(item.transform.position);
                Ray targetRay = RayAt(targetPosition);

                Assert.That(item.TryBeginDrag(startRay), Is.True);
                Assert.That(item.Drag(targetRay), Is.True);
                UniTask<bool> task = item.EndDragAsync(targetRay);
                Assert.That(
                    task.Status,
                    Is.Not.EqualTo(UniTaskStatus.Pending));
                return task.GetAwaiter().GetResult();
            }

            public bool Undo()
            {
                return _undoHistory.Undo();
            }

            public void Destroy()
            {
                Object.Destroy(_root);
            }

            private static Ray RayAt(Vector3 position)
            {
                return new Ray(
                    new Vector3(position.x, 5f, position.z),
                    Vector3.down);
            }
        }

        private sealed class MixedStageFixture
        {
            private readonly GameObject _root;
            private readonly ItemSnapController[] _debris;
            private readonly Transform[] _targets;
            private readonly IUndoHistory _undoHistory;

            private MixedStageFixture(
                GameObject root,
                CleaningLevel level,
                CleaningStage stage,
                GameObject stageRoot,
                CleaningInputController input,
                RenderTextureCleaner cleaner,
                ItemSnapController[] debris,
                Transform[] targets,
                IUndoHistory undoHistory)
            {
                _root = root;
                Level = level;
                Stage = stage;
                StageRoot = stageRoot;
                Input = input;
                Cleaner = cleaner;
                _debris = debris;
                _targets = targets;
                _undoHistory = undoHistory;
            }

            public CleaningLevel Level { get; }

            public CleaningStage Stage { get; }

            public GameObject StageRoot { get; }

            public CleaningInputController Input { get; }

            public RenderTextureCleaner Cleaner { get; }

            public static MixedStageFixture Create(
                string prefix,
                int debrisCount,
                bool cleanerOnStageRoot = false)
            {
                var root = new GameObject(prefix + " Level");
                root.SetActive(false);

                CleaningLevel level =
                    root.AddComponent<CleaningLevel>();
                CleaningInputController input =
                    root.AddComponent<CleaningInputController>();
                input.Construct(
                    new PresentationActivityCoordinator());
                SetPrivateField(level, "_cleaningInput", input);
                SetPrivateField(level, "_stageSettleSeconds", 0f);

                var stageObject =
                    new GameObject(prefix + " Stage");
                stageObject.transform.SetParent(root.transform, false);
                CleaningStage stage =
                    stageObject.AddComponent<CleaningStage>();

                var content = new GameObject("Content");
                content.transform.SetParent(
                    stageObject.transform,
                    false);

                GameObject cleanerObject;
                if (cleanerOnStageRoot)
                {
                    cleanerObject = content;
                }
                else
                {
                    cleanerObject = new GameObject("Surface");
                    cleanerObject.transform.SetParent(
                        content.transform,
                        false);
                }

                RenderTextureCleaner cleaner =
                    cleanerObject.AddComponent<RenderTextureCleaner>();
                cleaner.enabled = false;

                var debris = new ItemSnapController[debrisCount];
                var targets = new Transform[debrisCount];
                var coordinator = new PresentationActivityCoordinator();
                var undoHistory = new LevelSessionUndoHistory();

                for (var index = 0; index < debrisCount; index++)
                {
                    var targetObject =
                        new GameObject("Debris Target " + index);
                    targetObject.transform.SetParent(
                        content.transform,
                        false);
                    targetObject.transform.position =
                        new Vector3(index, 0f, -1f);
                    targets[index] = targetObject.transform;

                    var debrisObject =
                        new GameObject("Debris " + index);
                    debrisObject.transform.SetParent(
                        content.transform,
                        false);
                    debrisObject.transform.position =
                        new Vector3(index, 0f, 1f);
                    ItemSnapController item =
                        debrisObject.AddComponent<
                            ItemSnapController>();
                    SetPrivateField(
                        item,
                        "_snapTarget",
                        targetObject.transform);
                    SetPrivateField(
                        item,
                        "_positionSnapThreshold",
                        0.5f);
                    SetPrivateField(
                        item,
                        "_snapDurationSeconds",
                        0f);
                    SetPrivateField(
                        item,
                        "_returnDurationSeconds",
                        0f);
                    item.Construct(
                        new SilentHaptics(),
                        new SilentAudio(),
                        coordinator,
                        undoHistory);
                    debris[index] = item;
                }

                SetPrivateField(stage, "_stageRoot", content);
                SetPrivateField(stage, "_cleaner", cleaner);
                SetPrivateField(stage, "_debris", debris);
                SetPrivateField(
                    stage,
                    "_tool",
                    CleaningToolKind.Sponge);
                SetPrivateField(
                    level,
                    "_stages",
                    new[] { stage });

                var definition =
                    ScriptableObject.CreateInstance<LevelDefinition>();
                SetPrivateField(definition, "_levelId", prefix);
                SetPrivateField(
                    definition,
                    "_levelType",
                    LevelType.Cleaning);

                root.SetActive(true);
                level.Construct(
                    new PresentationActivityCoordinator());
                Assert.That(
                    level.InitLevel(definition),
                    Is.True,
                    "The mixed stage failed to initialise.");

                return new MixedStageFixture(
                    root,
                    level,
                    stage,
                    content,
                    input,
                    cleaner,
                    debris,
                    targets,
                    undoHistory);
            }

            public bool PlaceDebris(int index)
            {
                ItemSnapController item = _debris[index];
                Ray startRay = RayAt(item.transform.position);
                Ray targetRay = RayAt(_targets[index].position);

                Assert.That(item.TryBeginDrag(startRay), Is.True);
                Assert.That(item.Drag(targetRay), Is.True);
                UniTask<bool> task = item.EndDragAsync(targetRay);
                Assert.That(
                    task.Status,
                    Is.Not.EqualTo(UniTaskStatus.Pending));
                return task.GetAwaiter().GetResult();
            }

            public bool Undo()
            {
                return _undoHistory.Undo();
            }

            public bool IsDebrisPlaced(int index)
            {
                return _debris[index].IsPlaced;
            }

            public bool IsDebrisVisible(int index)
            {
                return _debris[index].gameObject.activeInHierarchy;
            }

            public void CompleteSurface()
            {
                MethodInfo method = typeof(CleaningStage).GetMethod(
                    "HandleSurfaceCleaned",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                method.Invoke(Stage, null);
            }

            public void Destroy()
            {
                Object.Destroy(_root);
            }

            private static Ray RayAt(Vector3 position)
            {
                return new Ray(
                    new Vector3(position.x, 5f, position.z),
                    Vector3.down);
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
    }
}
