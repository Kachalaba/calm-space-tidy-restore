using System.Reflection;
using CalmSpace.Audio;
using CalmSpace.Cleaning;
using CalmSpace.Core;
using CalmSpace.Haptics;
using CalmSpace.Input;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class CleaningStageProgressTests
    {
        private StageFixture _fixture;

        [TearDown]
        public void TearDown()
        {
            _fixture?.Destroy();
            _fixture = null;
        }

        [Test]
        public void MixedStageUsesHalfForDebrisThenHalfForSurface()
        {
            _fixture = StageFixture.Create(3);

            Assert.That(_fixture.Stage.Fraction, Is.EqualTo(0f));

            Assert.That(_fixture.PlaceDebris(0), Is.True);
            Assert.That(
                _fixture.Stage.Fraction,
                Is.EqualTo(1f / 6f).Within(1e-4f));

            Assert.That(_fixture.PlaceDebris(1), Is.True);
            Assert.That(
                _fixture.Stage.Fraction,
                Is.EqualTo(1f / 3f).Within(1e-4f));

            Assert.That(_fixture.PlaceDebris(2), Is.True);
            Assert.That(
                _fixture.Stage.Fraction,
                Is.EqualTo(0.5f).Within(1e-4f));

            _fixture.SetSurfaceFraction(
                RenderTextureCleaner.CompletionThreshold * 0.5f);
            Assert.That(
                _fixture.Stage.Fraction,
                Is.EqualTo(0.75f).Within(1e-4f));
        }

        [Test]
        public void UnlockingSurfaceNeverMovesProgressBackward()
        {
            _fixture = StageFixture.Create(3);

            Assert.That(_fixture.PlaceDebris(0), Is.True);
            Assert.That(_fixture.PlaceDebris(1), Is.True);
            float beforeUnlock = _fixture.Stage.Fraction;

            Assert.That(_fixture.PlaceDebris(2), Is.True);
            float afterUnlock = _fixture.Stage.Fraction;

            Assert.That(
                afterUnlock,
                Is.GreaterThanOrEqualTo(beforeUnlock),
                "Unlocking an untouched surface must not erase debris " +
                "progress.");
        }

        private sealed class StageFixture
        {
            private readonly GameObject _root;
            private readonly ItemSnapController[] _debris;
            private readonly Transform[] _targets;

            private StageFixture(
                GameObject root,
                CleaningStage stage,
                RenderTextureCleaner cleaner,
                ItemSnapController[] debris,
                Transform[] targets)
            {
                _root = root;
                Stage = stage;
                Cleaner = cleaner;
                _debris = debris;
                _targets = targets;
            }

            public CleaningStage Stage { get; }

            public RenderTextureCleaner Cleaner { get; }

            public static StageFixture Create(int debrisCount)
            {
                var root = new GameObject("Mixed Cleaning Stage");
                CleaningStage stage =
                    root.AddComponent<CleaningStage>();

                var cleanerObject = new GameObject("Surface");
                cleanerObject.SetActive(false);
                cleanerObject.transform.SetParent(root.transform, false);
                RenderTextureCleaner cleaner =
                    cleanerObject.AddComponent<RenderTextureCleaner>();
                cleaner.enabled = false;

                var debris = new ItemSnapController[debrisCount];
                var targets = new Transform[debrisCount];
                var coordinator = new PresentationActivityCoordinator();

                for (var index = 0; index < debrisCount; index++)
                {
                    var targetObject =
                        new GameObject("Debris Target " + index);
                    targetObject.transform.SetParent(
                        root.transform,
                        false);
                    targetObject.transform.position =
                        new Vector3(index, 0f, -1f);
                    targets[index] = targetObject.transform;

                    var debrisObject =
                        new GameObject("Debris " + index);
                    debrisObject.transform.SetParent(
                        root.transform,
                        false);
                    debrisObject.transform.position =
                        new Vector3(index, 0f, 1f);
                    ItemSnapController item =
                        debrisObject.AddComponent<ItemSnapController>();
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
                        coordinator);
                    debris[index] = item;
                }

                SetPrivateField(stage, "_cleaner", cleaner);
                SetPrivateField(stage, "_debris", debris);
                stage.Activate();

                return new StageFixture(
                    root,
                    stage,
                    cleaner,
                    debris,
                    targets);
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

            public void SetSurfaceFraction(float fraction)
            {
                PropertyInfo property = typeof(RenderTextureCleaner)
                    .GetProperty(
                        nameof(RenderTextureCleaner.CleanedFraction),
                        BindingFlags.Instance |
                        BindingFlags.Public);
                MethodInfo setter = property?.GetSetMethod(true);

                Assert.That(setter, Is.Not.Null);
                setter.Invoke(Cleaner, new object[] { fraction });
            }

            public void Destroy()
            {
                Object.DestroyImmediate(_root);
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
