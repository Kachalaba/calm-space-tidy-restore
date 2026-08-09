using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Audio;
using CalmSpace.Cleaning;
using CalmSpace.Core;
using CalmSpace.Fasteners;
using CalmSpace.Haptics;
using CalmSpace.Input;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CalmSpace.Tests.PlayMode
{
    public sealed class CategorizedAsmrAudioPlayModeTests
    {
        [UnityTest]
        public IEnumerator CueBanksStayIsolatedAndRespectSpatialContract()
        {
            AudioClip placement = CreateClip("Placement");
            AudioClip screw = CreateClip("Screw Turn");
            AudioClip complete = CreateClip("Level Complete");
            AsmrAudioService service = CreateService(
                3,
                ("_snapClips", placement),
                ("_screwTurnClips", screw),
                ("_levelCompleteClips", complete));
            yield return null;

            Vector3 screwPosition = new Vector3(2f, 3f, 4f);
            service.PlayCue(AsmrAudioCue.ScrewTurn, screwPosition);
            AudioSource screwSource = SourceWithClip(service, screw);
            Assert.That(screwSource, Is.Not.Null);
            Assert.That(screwSource.spatialBlend, Is.EqualTo(1f));
            Assert.That(screwSource.transform.position, Is.EqualTo(screwPosition));
            Assert.That(SourceWithClip(service, placement), Is.Null,
                "A screw cue must never borrow the placement bank.");

            service.PlayCue(AsmrAudioCue.LevelComplete, new Vector3(9f, 8f, 7f));
            AudioSource completeSource = SourceWithClip(service, complete);
            Assert.That(completeSource, Is.Not.Null);
            Assert.That(completeSource.spatialBlend, Is.EqualTo(0f));

            Object.Destroy(service.gameObject);
            Object.Destroy(placement);
            Object.Destroy(screw);
            Object.Destroy(complete);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingCueBankIsSilentAndPoolNeverGrows()
        {
            AudioClip placement = CreateClip("Placement only");
            AsmrAudioService service = CreateService(
                2,
                ("_snapClips", placement));
            yield return null;

            service.PlayCue(AsmrAudioCue.ScrewRelease, Vector3.one);
            Assert.That(
                service.GetComponentsInChildren<AudioSource>(true),
                Has.All.Matches<AudioSource>(source => source.clip == null),
                "A missing bank must stay silent even when placement exists.");

            for (var index = 0; index < 12; index++)
            {
                service.PlayCue(AsmrAudioCue.Placement, Vector3.right * index);
            }

            AudioSource[] sources =
                service.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sources.Length, Is.EqualTo(2));
            Assert.That(sources, Has.Some.Matches<AudioSource>(
                source => source.clip == placement));

            Object.Destroy(service.gameObject);
            Object.Destroy(placement);
            yield return null;
        }

        [Test]
        public void CleaningFeedbackMapsToolsAndLimitsCadence()
        {
            var audio = new RecordingAudio();
            var clock = new FixedClock();
            var feedback = new CleaningAudioFeedback(audio, clock, 0.10d);
            Vector3 position = new Vector3(1f, 2f, 3f);

            Assert.That(feedback.TryPlay(
                CleaningToolKind.Cloth, position), Is.True);
            clock.Now = 0.099d;
            Assert.That(feedback.TryPlay(
                CleaningToolKind.Sponge, position), Is.False);
            clock.Now = 0.10d;
            Assert.That(feedback.TryPlay(
                CleaningToolKind.Sponge, position), Is.True);
            clock.Now = 0.20d;
            Assert.That(feedback.TryPlay(
                CleaningToolKind.Squeegee, position), Is.True);
            clock.Now = 0.30d;
            Assert.That(feedback.TryPlay(
                CleaningToolKind.Hands, position), Is.False,
                "Hands/debris do not paint a mask and have no hidden cue.");

            Assert.That(audio.Cues, Is.EqualTo(new[]
            {
                AsmrAudioCue.CleaningCloth,
                AsmrAudioCue.CleaningSponge,
                AsmrAudioCue.CleaningSqueegee
            }));
            Assert.That(audio.Positions, Has.All.EqualTo(position));
        }

        [UnityTest]
        public IEnumerator ScrewEmitsWholeTurnsAndOneFinalReleaseOnly()
        {
            var root = new GameObject("Categorized screw");
            root.SetActive(false);
            var screw = root.AddComponent<ScrewController>();
            SetPrivateField(screw, "_turnsRequired", 2);
            SetPrivateField(screw, "_secondsPerTurn", 1f);
            SetPrivateField(screw, "_settleDurationSeconds", 0f);
            var audio = new RecordingAudio();
            screw.Construct(new SilentHaptics(), audio);
            root.SetActive(true);
            yield return null;

            Assert.That(screw.BeginHold(), Is.True);
            screw.ContinueHold(0.6f);
            Assert.That(audio.Cues, Is.Empty,
                "A partial turn must stay silent.");
            screw.ContinueHold(0.4f);
            Assert.That(audio.Count(AsmrAudioCue.ScrewTurn), Is.EqualTo(1));
            screw.ContinueHold(1f);

            Assert.That(audio.Count(AsmrAudioCue.ScrewTurn), Is.EqualTo(2));
            Assert.That(audio.Count(AsmrAudioCue.ScrewRelease), Is.EqualTo(1));
            Assert.That(audio.Cues, Is.EqualTo(new[]
            {
                AsmrAudioCue.ScrewTurn,
                AsmrAudioCue.ScrewTurn,
                AsmrAudioCue.ScrewRelease
            }));

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SuccessfulPlacementEmitsOnePlacementCue()
        {
            var itemObject = new GameObject("Categorized item");
            var targetObject = new GameObject("Categorized target");
            targetObject.transform.position = new Vector3(0.05f, 0f, 0f);
            var item = itemObject.AddComponent<ItemSnapController>();
            SetPrivateField(item, "_snapTarget", targetObject.transform);
            SetPrivateField(item, "_snapDurationSeconds", 0f);
            var audio = new RecordingAudio();
            item.Construct(
                new SilentHaptics(),
                audio,
                new PresentationActivityCoordinator());

            Ray start = new Ray(Vector3.up * 5f, Vector3.down);
            Ray destination = new Ray(
                targetObject.transform.position + Vector3.up * 5f,
                Vector3.down);
            Assert.That(item.TryBeginDrag(start), Is.True);
            Assert.That(item.Drag(destination), Is.True);
            UniTask<bool> placement = item.EndDragAsync(destination);
            while (placement.Status == UniTaskStatus.Pending)
            {
                yield return null;
            }

            Assert.That(placement.GetAwaiter().GetResult(), Is.True);
            Assert.That(audio.Cues, Is.EqualTo(
                new[] { AsmrAudioCue.Placement }));

            Object.Destroy(itemObject);
            Object.Destroy(targetObject);
            yield return null;
        }

        private static AsmrAudioService CreateService(
            int poolSize,
            params (string Field, AudioClip Clip)[] banks)
        {
            var root = new GameObject("Categorized ASMR service");
            root.SetActive(false);
            var service = root.AddComponent<AsmrAudioService>();
            SetPrivateField(service, "_poolSize", poolSize);
            foreach ((string field, AudioClip clip) in banks)
            {
                SetPrivateField(service, field, new[] { clip });
            }

            root.SetActive(true);
            return service;
        }

        private static AudioClip CreateClip(string name)
        {
            return AudioClip.Create(name, 64, 1, 44100, false);
        }

        private static AudioSource SourceWithClip(
            AsmrAudioService service,
            AudioClip clip)
        {
            AudioSource[] sources =
                service.GetComponentsInChildren<AudioSource>(true);
            for (var index = 0; index < sources.Length; index++)
            {
                if (sources[index].clip == clip)
                {
                    return sources[index];
                }
            }

            return null;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class FixedClock : IMonotonicClock
        {
            public double Now { get; set; }

            public double NowSeconds => Now;
        }

        private sealed class RecordingAudio : ICategorizedAsmrAudioService
        {
            public bool IsAvailable => true;

            public List<AsmrAudioCue> Cues { get; } =
                new List<AsmrAudioCue>();

            public List<Vector3> Positions { get; } =
                new List<Vector3>();

            public void PlaySnap(Vector3 worldPosition)
            {
                PlayCue(AsmrAudioCue.Placement, worldPosition);
            }

            public void PlayCue(
                AsmrAudioCue cue,
                Vector3 worldPosition)
            {
                Cues.Add(cue);
                Positions.Add(worldPosition);
            }

            public int Count(AsmrAudioCue cue)
            {
                var count = 0;
                for (var index = 0; index < Cues.Count; index++)
                {
                    if (Cues[index] == cue)
                    {
                        count++;
                    }
                }

                return count;
            }
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
    }
}
