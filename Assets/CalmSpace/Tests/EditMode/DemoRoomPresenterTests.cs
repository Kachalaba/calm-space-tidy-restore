using System.Reflection;
using CalmSpace.UI;
using NUnit.Framework;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class DemoRoomPresenterTests
    {
        [Test]
        public void PresenterShowsOnlySelectedDecorationAndCanHideRoom()
        {
            var host = new GameObject("Presenter");
            var room = new GameObject("Home Room");
            var decorations = new GameObject[4];
            try
            {
                DemoRoomPresenter presenter =
                    host.AddComponent<DemoRoomPresenter>();
                for (var index = 0; index < decorations.Length; index++)
                {
                    decorations[index] =
                        new GameObject("Decoration " + index);
                    decorations[index].transform.SetParent(
                        room.transform,
                        false);
                }

                SetPrivateField(presenter, "_roomRoot", room);
                SetPrivateField(
                    presenter,
                    "_decorationRoots",
                    decorations);

                presenter.SetVisible(true);
                Assert.That(presenter.SelectDecoration(2), Is.True);

                Assert.That(presenter.RoomRoot, Is.SameAs(room));
                Assert.That(presenter.DecorationCount, Is.EqualTo(4));
                Assert.That(presenter.IsVisible, Is.True);
                Assert.That(
                    presenter.SelectedDecorationIndex,
                    Is.EqualTo(2));
                for (var index = 0; index < decorations.Length; index++)
                {
                    Assert.That(
                        decorations[index].activeSelf,
                        Is.EqualTo(index == 2));
                }

                presenter.SetVisible(false);
                Assert.That(room.activeSelf, Is.False);
                Assert.That(presenter.IsVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(room);
            }
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
    }
}
