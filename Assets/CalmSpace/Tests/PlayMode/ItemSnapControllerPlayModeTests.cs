using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Audio;
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
    public sealed class ItemSnapControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator SuccessfulSnapReleasesLeaseAndPlaysFeedback()
        {
            var itemObject = new GameObject("Item");
            var targetObject = new GameObject("Target");
            targetObject.transform.position =
                new Vector3(0.05f, 0f, 0f);

            var item =
                itemObject.AddComponent<ItemSnapController>();
            SetPrivateField(
                item,
                "_snapTarget",
                targetObject.transform);
            SetPrivateField(item, "_snapDurationSeconds", 0f);

            var haptics = new RecordingHaptics();
            var audio = new RecordingAudio();
            var activity =
                new PresentationActivityCoordinator();
            item.Construct(haptics, audio, activity);

            var startRay = new Ray(
                new Vector3(0f, 5f, 0f),
                Vector3.down);
            Assert.That(item.TryBeginDrag(startRay), Is.True);
            Assert.That(activity.ActiveDragCount, Is.EqualTo(1));
            Assert.That(
                activity.TryEnterAd(out var adLease, out var reason),
                Is.False);
            Assert.That(adLease, Is.Null);
            Assert.That(reason, Is.EqualTo(AdBlockReason.DragActive));

            var targetRay = new Ray(
                new Vector3(0.05f, 5f, 0f),
                Vector3.down);
            Assert.That(item.Drag(targetRay), Is.True);

            var task = item.EndDragAsync(targetRay);
            while (task.Status == UniTaskStatus.Pending)
            {
                yield return null;
            }

            Assert.That(
                task.GetAwaiter().GetResult(),
                Is.True);
            Assert.That(item.IsPlaced, Is.True);
            Assert.That(
                item.transform.position,
                Is.EqualTo(targetObject.transform.position));
            Assert.That(haptics.SnapCount, Is.EqualTo(1));
            Assert.That(audio.SnapCount, Is.EqualTo(1));
            Assert.That(activity.ActiveDragCount, Is.Zero);

            Object.Destroy(itemObject);
            Object.Destroy(targetObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StaleEndDragDoesNotReleaseNewDragLease()
        {
            var itemObject = new GameObject("Item");
            var targetObject = new GameObject("Target");
            targetObject.transform.position =
                new Vector3(0.05f, 0f, 0f);

            var item =
                itemObject.AddComponent<ItemSnapController>();
            SetPrivateField(
                item,
                "_snapTarget",
                targetObject.transform);
            SetPrivateField(item, "_snapDurationSeconds", 10f);

            var haptics = new RecordingHaptics();
            var audio = new RecordingAudio();
            var activity =
                new PresentationActivityCoordinator();
            item.Construct(haptics, audio, activity);

            var startRay = new Ray(
                new Vector3(0f, 5f, 0f),
                Vector3.down);
            var targetRay = new Ray(
                new Vector3(0.05f, 5f, 0f),
                Vector3.down);

            Assert.That(item.TryBeginDrag(startRay), Is.True);
            Assert.That(item.Drag(targetRay), Is.True);

            var staleEndTask = item.EndDragAsync(targetRay);
            Assert.That(
                staleEndTask.Status,
                Is.EqualTo(UniTaskStatus.Pending));

            item.CancelDrag();

            Assert.That(activity.ActiveDragCount, Is.Zero);
            Assert.That(item.TryBeginDrag(startRay), Is.True);
            Assert.That(item.IsDragging, Is.True);
            Assert.That(activity.ActiveDragCount, Is.EqualTo(1));

            while (staleEndTask.Status == UniTaskStatus.Pending)
            {
                yield return null;
            }

            Assert.That(
                staleEndTask.GetAwaiter().GetResult(),
                Is.False);
            Assert.That(item.IsDragging, Is.True);
            Assert.That(item.IsPlaced, Is.False);
            Assert.That(activity.ActiveDragCount, Is.EqualTo(1));
            Assert.That(haptics.SnapCount, Is.Zero);
            Assert.That(audio.SnapCount, Is.Zero);

            Assert.That(
                activity.TryEnterAd(out var adLease, out var reason),
                Is.False);
            Assert.That(adLease, Is.Null);
            Assert.That(reason, Is.EqualTo(AdBlockReason.DragActive));

            item.CancelDrag();
            Assert.That(activity.ActiveDragCount, Is.Zero);

            Object.Destroy(itemObject);
            Object.Destroy(targetObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ZeroItemFittingLevelCompletesWithoutGameplayLeaseLeak()
        {
            var levelObject = new GameObject("FittingLevel");
            var level = levelObject.AddComponent<FittingLevel>();
            var definition =
                ScriptableObject.CreateInstance<LevelDefinition>();
            SetPrivateField(definition, "_levelType", LevelType.Fitting);

            var activity =
                new PresentationActivityCoordinator();
            var states = new List<LevelState>();
            var gameplayCounts = new List<int>();
            var completionCount = 0;
            level.StateChanged += state =>
            {
                states.Add(state);
                gameplayCounts.Add(activity.ActiveGameplayCount);
            };
            level.LevelCompleted += _ => completionCount++;
            level.Construct(activity);

            Assert.That(level.InitLevel(definition), Is.True);
            Assert.That(level.ItemCount, Is.Zero);
            Assert.That(level.PlacedItemCount, Is.Zero);
            Assert.That(level.State, Is.EqualTo(LevelState.Completed));
            Assert.That(
                states,
                Is.EqualTo(
                    new[]
                    {
                        LevelState.Initializing,
                        LevelState.Active,
                        LevelState.Completing,
                        LevelState.Completed
                    }));
            Assert.That(
                gameplayCounts,
                Is.EqualTo(new[] { 0, 1, 1, 0 }));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(activity.ActiveGameplayCount, Is.Zero);

            Assert.That(
                activity.TryEnterAd(out var adLease, out var reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(AdBlockReason.None));
            adLease.Dispose();

            Object.Destroy(levelObject);
            Object.Destroy(definition);
            yield return null;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            var field = target
                .GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private sealed class RecordingHaptics : IHapticService
        {
            public bool IsSupported => true;

            public int SnapCount { get; private set; }

            public void PlayDragTick(float intensity)
            {
            }

            public void PlaySnap()
            {
                SnapCount++;
            }

            public void Cancel()
            {
            }
        }

        private sealed class RecordingAudio : IAsmrAudioService
        {
            public bool IsAvailable => true;

            public int SnapCount { get; private set; }

            public void PlaySnap(Vector3 worldPosition)
            {
                SnapCount++;
            }
        }
    }
}
