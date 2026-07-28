using System.Collections;
using System.Reflection;
using CalmSpace.Audio;
using CalmSpace.Core;
using CalmSpace.Haptics;
using CalmSpace.Input;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CalmSpace.Tests.PlayMode
{
    public sealed class SoftSortingPlayModeTests
    {
        [UnityTest]
        public IEnumerator IncompatibleGroupDropReturnsItemToStart()
        {
            var groupObject = new GameObject("Group");
            var slotObject = new GameObject("Capsule Slot");
            var itemObject = new GameObject("Pebble Item");
            slotObject.transform.position =
                new Vector3(0.20f, 0f, 0f);

            SnapSlot slot = CreateSlot(
                slotObject,
                SnapCategory.Capsule);
            SnapTargetGroup group = CreateGroup(
                groupObject,
                slot);
            ItemSnapController item = CreateItem(
                itemObject,
                group,
                SnapCategory.Pebble);

            var startRay = RayAt(0f);
            var slotRay = RayAt(0.20f);
            Assert.That(item.TryBeginDrag(startRay), Is.True);
            Assert.That(item.Drag(slotRay), Is.True);

            UniTask<bool> endTask = item.EndDragAsync(slotRay);
            while (endTask.Status == UniTaskStatus.Pending)
            {
                yield return null;
            }

            Assert.That(
                endTask.GetAwaiter().GetResult(),
                Is.False);
            Assert.That(item.IsPlaced, Is.False);
            Assert.That(
                item.transform.position,
                Is.EqualTo(Vector3.zero));
            Assert.That(slot.IsOccupied, Is.False);

            Object.Destroy(itemObject);
            Object.Destroy(slotObject);
            Object.Destroy(groupObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameCategoryItemsCompleteInEitherSlotOrder()
        {
            SortingFixture first = CreateFixture("First");
            Assert.That(first.PlaceItem(0, -0.25f), Is.True);
            Assert.That(first.PlaceItem(1, 0.25f), Is.True);
            Assert.That(
                first.LeftSlot.ReservedItemId,
                Is.EqualTo(first.Items[0].ItemInstanceId));
            Assert.That(
                first.RightSlot.ReservedItemId,
                Is.EqualTo(first.Items[1].ItemInstanceId));
            first.Destroy();
            yield return null;

            SortingFixture reverse = CreateFixture("Reverse");
            Assert.That(reverse.PlaceItem(1, -0.25f), Is.True);
            Assert.That(reverse.PlaceItem(0, 0.25f), Is.True);
            Assert.That(
                reverse.LeftSlot.ReservedItemId,
                Is.EqualTo(reverse.Items[1].ItemInstanceId));
            Assert.That(
                reverse.RightSlot.ReservedItemId,
                Is.EqualTo(reverse.Items[0].ItemInstanceId));
            reverse.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CancelledSnapReleasesReservationAndReturnsItem()
        {
            SortingFixture fixture = CreateFixture("Cancelled");
            ItemSnapController item = fixture.Items[0];
            SetPrivateField(item, "_snapDurationSeconds", 1f);

            var startRay = RayAt(item.transform.position.x);
            var targetRay = RayAt(-0.25f);
            Assert.That(item.TryBeginDrag(startRay), Is.True);
            Assert.That(item.Drag(targetRay), Is.True);

            UniTask<bool> endTask = item.EndDragAsync(targetRay);
            Assert.That(fixture.LeftSlot.IsOccupied, Is.True);
            item.CancelDrag();

            while (endTask.Status == UniTaskStatus.Pending)
            {
                yield return null;
            }

            Assert.That(
                endTask.GetAwaiter().GetResult(),
                Is.False);
            Assert.That(fixture.LeftSlot.IsOccupied, Is.False);
            Assert.That(
                item.transform.position,
                Is.EqualTo(new Vector3(-1f, 0f, 0f)));

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedPlacedItemReleasesItsSlot()
        {
            SortingFixture fixture = CreateFixture("Destroyed");
            Assert.That(fixture.PlaceItem(0, -0.25f), Is.True);
            Assert.That(fixture.LeftSlot.IsOccupied, Is.True);

            Object.Destroy(fixture.Items[0].gameObject);
            yield return null;

            Assert.That(fixture.LeftSlot.IsOccupied, Is.False);
            fixture.Destroy();
            yield return null;
        }

        private static SortingFixture CreateFixture(string prefix)
        {
            var groupObject = new GameObject(prefix + " Group");
            var leftObject = new GameObject(prefix + " Left Slot");
            var rightObject = new GameObject(prefix + " Right Slot");
            leftObject.transform.position =
                new Vector3(-0.25f, 0f, 0f);
            rightObject.transform.position =
                new Vector3(0.25f, 0f, 0f);

            SnapSlot left = CreateSlot(
                leftObject,
                SnapCategory.Pebble);
            SnapSlot right = CreateSlot(
                rightObject,
                SnapCategory.Pebble);
            SnapTargetGroup group = CreateGroup(
                groupObject,
                left,
                right);

            var firstObject = new GameObject(prefix + " Item A");
            var secondObject = new GameObject(prefix + " Item B");
            firstObject.transform.position =
                new Vector3(-1f, 0f, 0f);
            secondObject.transform.position =
                new Vector3(1f, 0f, 0f);
            ItemSnapController first = CreateItem(
                firstObject,
                group,
                SnapCategory.Pebble);
            ItemSnapController second = CreateItem(
                secondObject,
                group,
                SnapCategory.Pebble);

            return new SortingFixture(
                groupObject,
                leftObject,
                rightObject,
                new[] { firstObject, secondObject },
                left,
                right,
                new[] { first, second });
        }

        private static ItemSnapController CreateItem(
            GameObject gameObject,
            SnapTargetGroup group,
            SnapCategory category)
        {
            ItemSnapController item =
                gameObject.AddComponent<ItemSnapController>();
            SetPrivateField(item, "_snapTargetGroup", group);
            SetPrivateField(item, "_snapCategory", category);
            SetPrivateField(
                item,
                "_positionSnapThreshold",
                0.40f);
            SetPrivateField(item, "_snapDurationSeconds", 0f);
            SetPrivateField(item, "_returnDurationSeconds", 0f);
            item.Construct(
                new SilentHaptics(),
                new SilentAudio(),
                new PresentationActivityCoordinator());
            return item;
        }

        private static SnapSlot CreateSlot(
            GameObject gameObject,
            SnapCategory category)
        {
            SnapSlot slot = gameObject.AddComponent<SnapSlot>();
            SetPrivateField(slot, "_category", category);
            return slot;
        }

        private static SnapTargetGroup CreateGroup(
            GameObject gameObject,
            params SnapSlot[] slots)
        {
            SnapTargetGroup group =
                gameObject.AddComponent<SnapTargetGroup>();
            SetPrivateField(group, "_slots", slots);
            return group;
        }

        private static Ray RayAt(float x)
        {
            return new Ray(
                new Vector3(x, 5f, 0f),
                Vector3.down);
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
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private sealed class SortingFixture
        {
            private readonly GameObject _groupObject;
            private readonly GameObject _leftObject;
            private readonly GameObject _rightObject;
            private readonly GameObject[] _itemObjects;

            public SortingFixture(
                GameObject groupObject,
                GameObject leftObject,
                GameObject rightObject,
                GameObject[] itemObjects,
                SnapSlot leftSlot,
                SnapSlot rightSlot,
                ItemSnapController[] items)
            {
                _groupObject = groupObject;
                _leftObject = leftObject;
                _rightObject = rightObject;
                _itemObjects = itemObjects;
                LeftSlot = leftSlot;
                RightSlot = rightSlot;
                Items = items;
            }

            public SnapSlot LeftSlot { get; }

            public SnapSlot RightSlot { get; }

            public ItemSnapController[] Items { get; }

            public bool PlaceItem(int itemIndex, float targetX)
            {
                ItemSnapController item = Items[itemIndex];
                var startRay = RayAt(item.transform.position.x);
                var targetRay = RayAt(targetX);
                Assert.That(item.TryBeginDrag(startRay), Is.True);
                Assert.That(item.Drag(targetRay), Is.True);
                UniTask<bool> task =
                    item.EndDragAsync(targetRay);
                Assert.That(
                    task.Status,
                    Is.Not.EqualTo(UniTaskStatus.Pending));
                return task.GetAwaiter().GetResult();
            }

            public void Destroy()
            {
                for (var index = 0;
                     index < _itemObjects.Length;
                     index++)
                {
                    Object.Destroy(_itemObjects[index]);
                }

                Object.Destroy(_leftObject);
                Object.Destroy(_rightObject);
                Object.Destroy(_groupObject);
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

        private sealed class SilentAudio : IAsmrAudioService
        {
            public bool IsAvailable => true;

            public void PlaySnap(Vector3 worldPosition)
            {
            }
        }
    }
}
