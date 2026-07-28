using System.Reflection;
using CalmSpace.Input;
using NUnit.Framework;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class SnapTargetGroupTests
    {
        [Test]
        public void ReservesNearestCompatibleFreeSlot()
        {
            var groupObject = new GameObject("Group");
            var farObject = new GameObject("Far Pebble");
            var nearObject = new GameObject("Near Pebble");
            var wrongObject = new GameObject("Near Capsule");
            try
            {
                farObject.transform.position =
                    new Vector3(0.45f, 0f, 0f);
                nearObject.transform.position =
                    new Vector3(0.20f, 0f, 0f);
                wrongObject.transform.position =
                    new Vector3(0.10f, 0f, 0f);

                SnapSlot far = CreateSlot(
                    farObject,
                    SnapCategory.Pebble);
                SnapSlot near = CreateSlot(
                    nearObject,
                    SnapCategory.Pebble);
                SnapSlot wrong = CreateSlot(
                    wrongObject,
                    SnapCategory.Capsule);
                SnapTargetGroup group = CreateGroup(
                    groupObject,
                    far,
                    near,
                    wrong);

                Assert.That(
                    group.TryReserveNearestCompatible(
                        SnapCategory.Pebble,
                        101,
                        7,
                        Vector3.zero,
                        0.50f,
                        out var reservation),
                    Is.True);
                Assert.That(reservation.IsValid, Is.True);
                Assert.That(reservation.Slot, Is.SameAs(near));
                Assert.That(
                    reservation.TargetPose.Position,
                    Is.EqualTo(new Vector3(0.20f, 0f, 0f)));
                Assert.That(near.IsOccupied, Is.True);
                Assert.That(far.IsOccupied, Is.False);
                Assert.That(wrong.IsOccupied, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(groupObject);
                Object.DestroyImmediate(farObject);
                Object.DestroyImmediate(nearObject);
                Object.DestroyImmediate(wrongObject);
            }
        }

        [Test]
        public void OccupiedNearestSlotFallsBackToNextCompatibleSlot()
        {
            var groupObject = new GameObject("Group");
            var nearObject = new GameObject("Near");
            var farObject = new GameObject("Far");
            try
            {
                nearObject.transform.position =
                    new Vector3(0.10f, 0f, 0f);
                farObject.transform.position =
                    new Vector3(0.30f, 0f, 0f);
                SnapSlot near = CreateSlot(
                    nearObject,
                    SnapCategory.Block);
                SnapSlot far = CreateSlot(
                    farObject,
                    SnapCategory.Block);
                SnapTargetGroup group = CreateGroup(
                    groupObject,
                    near,
                    far);

                Assert.That(
                    group.TryReserveNearestCompatible(
                        SnapCategory.Block,
                        11,
                        1,
                        Vector3.zero,
                        0.50f,
                        out var first),
                    Is.True);
                Assert.That(first.Slot, Is.SameAs(near));

                Assert.That(
                    group.TryReserveNearestCompatible(
                        SnapCategory.Block,
                        22,
                        1,
                        Vector3.zero,
                        0.50f,
                        out var second),
                    Is.True);
                Assert.That(second.Slot, Is.SameAs(far));
                Assert.That(near.ReservedItemId, Is.EqualTo(11));
                Assert.That(far.ReservedItemId, Is.EqualTo(22));
            }
            finally
            {
                Object.DestroyImmediate(groupObject);
                Object.DestroyImmediate(nearObject);
                Object.DestroyImmediate(farObject);
            }
        }

        [Test]
        public void IncompatibleCategoryDoesNotReserveAFreeSlot()
        {
            var groupObject = new GameObject("Group");
            var slotObject = new GameObject("Capsule");
            try
            {
                SnapSlot slot = CreateSlot(
                    slotObject,
                    SnapCategory.Capsule);
                SnapTargetGroup group =
                    CreateGroup(groupObject, slot);

                Assert.That(
                    group.TryReserveNearestCompatible(
                        SnapCategory.Pebble,
                        31,
                        2,
                        Vector3.zero,
                        1f,
                        out var reservation),
                    Is.False);
                Assert.That(reservation.IsValid, Is.False);
                Assert.That(slot.IsOccupied, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(groupObject);
                Object.DestroyImmediate(slotObject);
            }
        }

        [Test]
        public void OnlyMatchingReservationTokenCanReleaseSlot()
        {
            var groupObject = new GameObject("Group");
            var slotObject = new GameObject("Slot");
            try
            {
                SnapSlot slot = CreateSlot(
                    slotObject,
                    SnapCategory.Cylinder);
                SnapTargetGroup group =
                    CreateGroup(groupObject, slot);
                Assert.That(
                    group.TryReserveNearestCompatible(
                        SnapCategory.Cylinder,
                        41,
                        3,
                        Vector3.zero,
                        1f,
                        out var reservation),
                    Is.True);

                Assert.That(
                    slot.Release(41, 99),
                    Is.False);
                Assert.That(slot.IsOccupied, Is.True);
                Assert.That(reservation.Release(), Is.True);
                Assert.That(slot.IsOccupied, Is.False);
                Assert.That(reservation.Release(), Is.False);

                Assert.That(
                    group.TryReserveNearestCompatible(
                        SnapCategory.Cylinder,
                        42,
                        4,
                        Vector3.zero,
                        1f,
                        out var next),
                    Is.True);
                Assert.That(next.Slot, Is.SameAs(slot));
                Assert.That(slot.ReservedItemId, Is.EqualTo(42));
            }
            finally
            {
                Object.DestroyImmediate(groupObject);
                Object.DestroyImmediate(slotObject);
            }
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
    }
}
