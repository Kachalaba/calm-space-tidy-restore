using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using CalmSpace.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CalmSpace.Tests.PlayMode
{
    public sealed class LevelTransitionCurtainPlayModeTests
    {
        [UnityTest]
        public IEnumerator FadeToOpaqueBlocksInputAndSettlesOpaque()
        {
            CurtainFixture fixture =
                CurtainFixture.Create(
                    fadeDurationSeconds: 0.0001f);

            int startingFrame = Time.frameCount;
            UniTask fade = fixture.Curtain.FadeToOpaqueAsync();

            Assert.That(fixture.Root.activeSelf, Is.True);
            Assert.That(fixture.Group.blocksRaycasts, Is.True);
            Assert.That(fixture.Group.interactable, Is.False);
            Assert.That(
                fixture.Curtain.State,
                Is.EqualTo(
                    LevelTransitionCurtainState.FadingToOpaque));

            yield return WaitForCompletion(fade);

            fade.GetAwaiter().GetResult();

            Assert.That(Time.frameCount, Is.GreaterThan(startingFrame));
            Assert.That(
                fixture.Curtain.State,
                Is.EqualTo(LevelTransitionCurtainState.Opaque));
            Assert.That(fixture.Curtain.IsOpaque, Is.True);
            Assert.That(fixture.Group.alpha, Is.EqualTo(1f));
            Assert.That(fixture.Group.blocksRaycasts, Is.True);
            Assert.That(fixture.Group.interactable, Is.False);
            Assert.That(fixture.Root.activeSelf, Is.True);

            yield return null;

            Assert.That(
                fixture.Curtain.State,
                Is.EqualTo(LevelTransitionCurtainState.Opaque));
            Assert.That(fixture.Group.alpha, Is.EqualTo(1f));
            Assert.That(fixture.Group.blocksRaycasts, Is.True);

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FadeToClearStopsBlockingAndSettlesClear()
        {
            CurtainFixture fixture =
                CurtainFixture.Create(
                    fadeDurationSeconds: 0.0001f);
            UniTask opaque = fixture.Curtain.FadeToOpaqueAsync();
            yield return WaitForCompletion(opaque);

            opaque.GetAwaiter().GetResult();

            UniTask clear = fixture.Curtain.FadeToClearAsync();

            Assert.That(fixture.Root.activeSelf, Is.True);
            Assert.That(fixture.Group.blocksRaycasts, Is.True);
            Assert.That(
                fixture.Curtain.State,
                Is.EqualTo(
                    LevelTransitionCurtainState.FadingToClear));

            yield return WaitForCompletion(clear);

            clear.GetAwaiter().GetResult();

            Assert.That(
                fixture.Curtain.State,
                Is.EqualTo(LevelTransitionCurtainState.Clear));
            Assert.That(fixture.Curtain.IsOpaque, Is.False);
            Assert.That(fixture.Group.alpha, Is.EqualTo(0f));
            Assert.That(fixture.Group.blocksRaycasts, Is.False);
            Assert.That(fixture.Group.interactable, Is.False);
            Assert.That(fixture.Root.activeSelf, Is.False);

            yield return null;

            Assert.That(
                fixture.Curtain.State,
                Is.EqualTo(LevelTransitionCurtainState.Clear));
            Assert.That(fixture.Group.blocksRaycasts, Is.False);
            Assert.That(fixture.Root.activeSelf, Is.False);

            fixture.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CancelledFadeCanRecoverToClear()
        {
            CurtainFixture fixture =
                CurtainFixture.Create(fadeDurationSeconds: 0.25f);
            var cancellation = new CancellationTokenSource();

            UniTask cancelledFade =
                fixture.Curtain.FadeToOpaqueAsync(
                    cancellation.Token);

            Assert.That(fixture.Group.blocksRaycasts, Is.True);
            Assert.That(fixture.Root.activeSelf, Is.True);

            yield return null;
            cancellation.Cancel();
            yield return WaitForCompletion(cancelledFade);

            Assert.That(
                cancelledFade.Status,
                Is.EqualTo(UniTaskStatus.Canceled));
            Assert.Throws<OperationCanceledException>(
                () => cancelledFade.GetAwaiter().GetResult());
            Assert.That(fixture.Group.blocksRaycasts, Is.True);
            Assert.That(fixture.Root.activeSelf, Is.True);

            UniTask recovery =
                fixture.Curtain.FadeToClearAsync();
            yield return WaitForCompletion(recovery);

            recovery.GetAwaiter().GetResult();

            Assert.That(
                fixture.Curtain.State,
                Is.EqualTo(LevelTransitionCurtainState.Clear));
            Assert.That(fixture.Group.alpha, Is.EqualTo(0f));
            Assert.That(fixture.Group.blocksRaycasts, Is.False);
            Assert.That(fixture.Group.interactable, Is.False);
            Assert.That(fixture.Root.activeSelf, Is.False);

            cancellation.Dispose();
            fixture.Destroy();
            yield return null;
        }

        private static IEnumerator WaitForCompletion(UniTask task)
        {
            float timeoutAt = Time.realtimeSinceStartup + 2f;
            while (task.Status == UniTaskStatus.Pending &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(
                task.Status,
                Is.Not.EqualTo(UniTaskStatus.Pending),
                "Curtain transition did not settle within two seconds.");
        }

        private sealed class CurtainFixture
        {
            private CurtainFixture(
                GameObject root,
                CanvasGroup group,
                LevelTransitionCurtain curtain)
            {
                Root = root;
                Group = group;
                Curtain = curtain;
            }

            public GameObject Root { get; }

            public CanvasGroup Group { get; }

            public LevelTransitionCurtain Curtain { get; }

            public static CurtainFixture Create(
                float fadeDurationSeconds)
            {
                var root =
                    new GameObject("Level Transition Curtain Test");
                CanvasGroup group =
                    root.AddComponent<CanvasGroup>();
                LevelTransitionCurtain curtain =
                    root.AddComponent<LevelTransitionCurtain>();
                FieldInfo fadeDuration = typeof(LevelTransitionCurtain)
                    .GetField(
                        "_fadeDurationSeconds",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                Assert.That(
                    fadeDuration,
                    Is.Not.Null);
                fadeDuration.SetValue(
                    curtain,
                    fadeDurationSeconds);

                Assert.That(root.activeSelf, Is.False);
                Assert.That(group.alpha, Is.Zero);
                Assert.That(group.blocksRaycasts, Is.False);
                Assert.That(
                    curtain.State,
                    Is.EqualTo(LevelTransitionCurtainState.Clear));

                return new CurtainFixture(
                    root,
                    group,
                    curtain);
            }

            public void Destroy()
            {
                UnityEngine.Object.Destroy(Root);
            }
        }
    }
}
