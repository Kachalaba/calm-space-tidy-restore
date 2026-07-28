using System.Collections;
using System.Reflection;
using CalmSpace.Demo;
using CalmSpace.Input;
using CalmSpace.Levels;
using CalmSpace.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CalmSpace.Tests.PlayMode
{
    public sealed class DragInputRouterPlayModeTests :
        InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            PlayerPrefs.DeleteKey(
                PlayerPrefsDemoProgressStore.DefaultPlayerPrefsKey);
            PlayerPrefs.SetInt(
                DemoLocalizationService.DefaultPlayerPrefsKey,
                (int)DemoLocale.English);
            PlayerPrefs.Save();
        }

        public override void TearDown()
        {
            PlayerPrefs.DeleteKey(
                PlayerPrefsDemoProgressStore.DefaultPlayerPrefsKey);
            PlayerPrefs.DeleteKey(
                DemoLocalizationService.DefaultPlayerPrefsKey);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator AddressableSortingLevelAcceptsTouchscreenDrag()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            while (!sceneLoad.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            float initializationTimeout =
                Time.realtimeSinceStartup + 10f;
            while (
                (experience == null || !experience.IsInitialized) &&
                Time.realtimeSinceStartup < initializationTimeout)
            {
                experience =
                    Object.FindFirstObjectByType<
                        DemoExperienceController>();
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);

            IDemoProgressStore progressStore =
                GetPrivateField<IDemoProgressStore>(
                    experience,
                    "_progressStore");
            Assert.That(progressStore, Is.Not.Null);
            progressStore.MarkLevelCompleted(0);
            Assert.That(
                progressStore.IsLevelUnlocked(1),
                Is.True);

            UniTask<bool> loadTask =
                experience.PlayLevelAsync(1);
            float loadTimeout = Time.realtimeSinceStartup + 10f;
            while (
                loadTask.Status == UniTaskStatus.Pending &&
                Time.realtimeSinceStartup < loadTimeout)
            {
                yield return null;
            }

            Assert.That(
                loadTask.Status,
                Is.Not.EqualTo(UniTaskStatus.Pending));
            Assert.That(
                loadTask.GetAwaiter().GetResult(),
                Is.True);

            SortingLevel level =
                Object.FindFirstObjectByType<SortingLevel>();
            DragInputRouter router =
                Object.FindFirstObjectByType<DragInputRouter>();
            Camera camera = Camera.main;
            Assert.That(level, Is.Not.Null);
            Assert.That(level.State, Is.EqualTo(LevelState.Active));
            Assert.That(router, Is.Not.Null);
            Assert.That(router.isActiveAndEnabled, Is.True);
            Assert.That(camera, Is.Not.Null);

            ItemSnapController[] items =
                level.GetComponentsInChildren<
                    ItemSnapController>();
            Assert.That(items, Has.Length.EqualTo(4));
            ItemSnapController item = items[0];
            SnapCategory category =
                GetPrivateField<SnapCategory>(
                    item,
                    "_snapCategory");
            SnapSlot[] slots =
                level.GetComponentsInChildren<SnapSlot>();
            SnapSlot target = null;
            for (var index = 0; index < slots.Length; index++)
            {
                if (slots[index].Category == category)
                {
                    target = slots[index];
                    break;
                }
            }

            Assert.That(target, Is.Not.Null);
            Collider itemCollider =
                item.GetComponentInChildren<Collider>();
            Assert.That(itemCollider, Is.Not.Null);

            Physics.SyncTransforms();
            Vector2 startScreenPosition =
                camera.WorldToScreenPoint(
                    itemCollider.bounds.center);
            Vector2 targetScreenPosition =
                camera.WorldToScreenPoint(
                    target.TargetPose.Position);
            Assert.That(
                IsInsideScreen(startScreenPosition),
                Is.True,
                "The authored item must be visible to the input camera.");
            Assert.That(
                IsInsideScreen(targetScreenPosition),
                Is.True,
                "The authored slot must be visible to the input camera.");

            Ray startRay =
                camera.ScreenPointToRay(startScreenPosition);
            Assert.That(
                Physics.Raycast(startRay, out RaycastHit hit, 100f),
                Is.True);
            Assert.That(
                hit.collider.GetComponentInParent<
                    ItemSnapController>(),
                Is.SameAs(item),
                "The item collider must be the first world hit.");
            Assert.That(
                IsUiBlocked(startScreenPosition),
                Is.False,
                "Gameplay HUD must not block the world item.");

            Touchscreen touchscreen =
                InputSystem.AddDevice<Touchscreen>();
            const int TouchId = 17;
            BeginTouch(
                TouchId,
                startScreenPosition,
                queueEventOnly: true,
                screen: touchscreen);
            yield return null;

            Assert.That(router.OwnedItem, Is.SameAs(item));
            Assert.That(item.IsDragging, Is.True);
            Vector3 dragStartPosition = item.transform.position;

            MoveTouch(
                TouchId,
                targetScreenPosition,
                targetScreenPosition - startScreenPosition,
                queueEventOnly: true,
                screen: touchscreen);
            yield return null;

            Assert.That(
                Vector3.Distance(
                    dragStartPosition,
                    item.transform.position),
                Is.GreaterThan(0.1f),
                "The routed touch move must move the world item.");

            EndTouch(
                TouchId,
                targetScreenPosition,
                queueEventOnly: true,
                screen: touchscreen);
            float placementTimeout =
                Time.realtimeSinceStartup + 3f;
            while (
                !item.IsPlaced &&
                Time.realtimeSinceStartup < placementTimeout)
            {
                yield return null;
            }

            Assert.That(router.HasDragOwnership, Is.False);
            Assert.That(item.IsPlaced, Is.True);
            Assert.That(level.PlacedItemCount, Is.EqualTo(1));
        }

        private static bool IsInsideScreen(Vector2 point)
        {
            return
                point.x >= 0f &&
                point.y >= 0f &&
                point.x <= Screen.width &&
                point.y <= Screen.height;
        }

        private static bool IsUiBlocked(Vector2 point)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var pointer = new PointerEventData(eventSystem)
            {
                position = point
            };
            var results =
                new System.Collections.Generic.List<
                    RaycastResult>();
            eventSystem.RaycastAll(pointer, results);
            return results.Count > 0;
        }

        private static T GetPrivateField<T>(
            object target,
            string fieldName)
        {
            FieldInfo field = target
                .GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }
    }
}
