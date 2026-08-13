using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CalmSpace.UI;
using CalmSpace.Workshop;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CalmSpace.Tests.EditMode
{
    /// <summary>
    /// Behaviour of the visual-only room presenter: permanent state derives
    /// from completed levels, at most one hotspot is actionable, a reveal is
    /// interruption safe, and nothing here can touch the profile.
    /// </summary>
    public sealed class WorkshopRoomPresenterTests
    {
        private GameObject _root;
        private WorkshopRoomPresenter _presenter;
        private GameObject _roomRoot;
        private GameObject _ambientRoot;
        private CanvasGroup _finale;
        private WorkshopRoomBeatBinding[] _beats;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Room", typeof(RectTransform));
            _roomRoot = Child(_root, "Content");
            _ambientRoot = Child(_roomRoot, "Ambient");
            _finale = Child(_roomRoot, "Finale").AddComponent<CanvasGroup>();

            var beats = new List<WorkshopRoomBeatBinding>();
            for (var index = 0;
                 index < WorkshopContentIds.CozyWorkshopBeatCount;
                 index++)
            {
                Assert.That(
                    WorkshopContentIds.TryGetCozyWorkshopBeat(
                        index, out WorkshopBeatContract beat),
                    Is.True);
                GameObject zone = Child(_roomRoot, "Zone" + index);
                var binding = new WorkshopRoomBeatBinding();
                binding.Configure(
                    beat.BeatId,
                    beat.ZoneIndex,
                    Child(zone, "Restored").AddComponent<CanvasGroup>(),
                    Child(zone, "Before").AddComponent<CanvasGroup>(),
                    Child(zone, "Hotspot").AddComponent<Button>());
                beats.Add(binding);
            }

            _beats = beats.ToArray();
            _presenter = _root.AddComponent<WorkshopRoomPresenter>();
            _presenter.Configure(
                WorkshopContentIds.CozyWorkshopChapterId,
                _roomRoot,
                _ambientRoot,
                _finale,
                _beats);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static WorkshopRoomVisualState State(
            int restoredCount,
            string hotspotBeatId)
        {
            var mask = 0;
            for (var index = 0; index < restoredCount; index++)
            {
                WorkshopContentIds.TryGetCozyWorkshopBeat(
                    index, out WorkshopBeatContract beat);
                mask |= 1 << beat.ZoneIndex;
            }

            return new WorkshopRoomVisualState(
                WorkshopContentIds.CozyWorkshopChapterId,
                mask,
                hotspotBeatId,
                restoredCount == WorkshopContentIds.CozyWorkshopBeatCount);
        }

        private static string BeatId(int stageIndex)
        {
            WorkshopContentIds.TryGetCozyWorkshopBeat(
                stageIndex, out WorkshopBeatContract beat);
            return beat.BeatId;
        }

        private int ActiveHotspotCount()
        {
            return _beats.Count(
                binding => binding.Hotspot.gameObject.activeSelf);
        }

        [Test]
        public void PermanentStateFollowsCompletedZonesOnly()
        {
            _presenter.ApplyState(State(3, BeatId(3)));

            for (var index = 0; index < _beats.Length; index++)
            {
                Assert.That(
                    _beats[index].RestoredGroup.alpha,
                    Is.EqualTo(index < 3 ? 1f : 0f).Within(0.0001f),
                    "Zone " + index + " restored layer");
                Assert.That(
                    _beats[index].BeforeGroup.alpha,
                    Is.EqualTo(0f).Within(0.0001f),
                    "Temporary overlays must never persist.");
            }

            Assert.That(_finale.alpha, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ExactlyOneHotspotMatchesTheRealNextLevel()
        {
            _presenter.ApplyState(State(2, BeatId(2)));

            Assert.That(ActiveHotspotCount(), Is.EqualTo(1));
            Assert.That(
                _beats[2].Hotspot.gameObject.activeSelf,
                Is.True,
                "The hotspot must point at the actual next level.");
        }

        [Test]
        public void NoHotspotIsActionableWithoutANextLevel()
        {
            _presenter.ApplyState(State(8, string.Empty));

            Assert.That(ActiveHotspotCount(), Is.Zero);
            Assert.That(_finale.alpha, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void HotspotPressEmitsItsStableBeatId()
        {
            var pressed = new List<string>();
            _presenter.HotspotPressed += pressed.Add;
            _presenter.ApplyState(State(1, BeatId(1)));

            _beats[1].Hotspot.onClick.Invoke();

            Assert.That(pressed, Is.EqualTo(new[] { BeatId(1) }));
        }

        [Test]
        public void ApplyStateIsIdempotentAndKeepsListenerCardinality()
        {
            var pressed = new List<string>();
            _presenter.HotspotPressed += pressed.Add;

            WorkshopRoomVisualState state = State(4, BeatId(4));
            _presenter.ApplyState(state);
            _presenter.ApplyState(state);
            _presenter.ApplyState(state);

            Assert.That(ActiveHotspotCount(), Is.EqualTo(1));
            _beats[4].Hotspot.onClick.Invoke();
            Assert.That(
                pressed,
                Is.EqualTo(new[] { BeatId(4) }),
                "Refreshing must not rebuild listeners.");
        }

        [Test]
        public void HiddenRoomStopsAmbientAndInteraction()
        {
            _presenter.ApplyState(State(1, BeatId(1)));
            _presenter.SetVisible(true);
            Assert.That(_presenter.IsVisible, Is.True);
            Assert.That(_ambientRoot.activeSelf, Is.True);

            _presenter.SetVisible(false);

            Assert.That(_presenter.IsVisible, Is.False);
            Assert.That(_roomRoot.activeSelf, Is.False);
            Assert.That(
                _ambientRoot.activeSelf,
                Is.False,
                "Ambient must stop with the room.");
            Assert.That(
                _beats.All(binding => !binding.Hotspot.interactable),
                Is.True);
        }

        [Test]
        public void RevealStartsAboveTheAlreadyCorrectPermanentState()
        {
            _presenter.ApplyState(State(3, BeatId(3)));
            _presenter.SetVisible(true);

            using var source = new CancellationTokenSource();
            UniTask<WorkshopRevealPlaybackResult> task =
                _presenter.PlayRevealAsync(BeatId(2), source.Token);

            Assert.That(_presenter.IsRevealPlaying, Is.True);
            Assert.That(
                _beats[2].RestoredGroup.alpha,
                Is.EqualTo(1f).Within(0.0001f),
                "The permanent state must already be correct.");
            Assert.That(
                _beats[2].BeforeGroup.alpha,
                Is.GreaterThan(0.5f),
                "The temporary before overlay must cover the zone.");
            Assert.That(
                _beats.All(binding => !binding.Hotspot.interactable),
                Is.True,
                "Interaction must be suspended during a reveal.");

            // Timed playback and mid-flight interruption are covered by
            // WorkshopPresentationPlayModeTests, where the player loop runs.
            source.Cancel();
            task.Forget();
        }

        [Test]
        public void AlreadyCancelledRevealNeverTouchesTheRoom()
        {
            _presenter.ApplyState(State(3, BeatId(3)));
            _presenter.SetVisible(true);

            using var source = new CancellationTokenSource();
            source.Cancel();
            WorkshopRevealPlaybackResult result =
                Settled(_presenter.PlayRevealAsync(BeatId(1), source.Token));

            Assert.That(
                result, Is.EqualTo(WorkshopRevealPlaybackResult.Cancelled));
            Assert.That(_presenter.IsRevealPlaying, Is.False);
            Assert.That(
                _beats[1].BeforeGroup.alpha,
                Is.EqualTo(0f).Within(0.0001f),
                "Cancellation must remove temporary overlays.");
            Assert.That(
                _beats[1].RestoredGroup.alpha,
                Is.EqualTo(1f).Within(0.0001f),
                "Cancellation must leave the completed persisted room.");
            Assert.That(
                _beats.Count(binding => binding.Hotspot.interactable),
                Is.GreaterThan(0),
                "Cancellation must not strand interaction.");
        }

        [Test]
        public void MissingVisualIsReportedWithoutBlockingTheRoom()
        {
            _presenter.ApplyState(State(2, BeatId(2)));
            _presenter.SetVisible(true);

            using var source = new CancellationTokenSource();
            WorkshopRevealPlaybackResult result = Settled(
                _presenter.PlayRevealAsync(
                    "cozy-workshop.not-authored", source.Token));

            Assert.That(
                result, Is.EqualTo(WorkshopRevealPlaybackResult.MissingVisual));
            Assert.That(_presenter.IsRevealPlaying, Is.False);
            Assert.That(
                ActiveHotspotCount(),
                Is.EqualTo(1),
                "A missing visual must not block the next level.");
            Assert.That(
                _beats.Count(binding => binding.Hotspot.interactable),
                Is.GreaterThan(0));
        }

        [Test]
        public void MissingVisualLogsOncePerStableId()
        {
            _presenter.ApplyState(State(2, BeatId(2)));
            using var source = new CancellationTokenSource();

            var warnings = new List<string>();
            void Capture(string message, string stack, LogType type)
            {
                if (type == LogType.Warning &&
                    message.IndexOf("not-authored", StringComparison.Ordinal)
                        >= 0)
                {
                    warnings.Add(message);
                }
            }

            Application.logMessageReceived += Capture;
            try
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    Assert.That(
                        Settled(_presenter.PlayRevealAsync(
                            "cozy-workshop.not-authored", source.Token)),
                        Is.EqualTo(
                            WorkshopRevealPlaybackResult.MissingVisual));
                }
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            Assert.That(
                warnings.Count,
                Is.EqualTo(1),
                "A missing visual must log once per stable ID, not per call.");
        }

        [Test]
        public void PresenterExposesNoProfileOrEconomySurface()
        {
            string[] forbidden =
            {
                "IDemoProgressStore", "DemoProgressSnapshot",
                "IProductAnalytics", "ILevelFlowController",
                "IWorkshopFlowCoordinator", "MarkPresentationSeen",
                "CozyTokens"
            };

            IEnumerable<string> surface = typeof(WorkshopRoomPresenter)
                .GetMembers(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)
                .Select(member => member.ToString())
                .Concat(typeof(WorkshopRoomPresenter)
                    .GetFields(
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                    .Select(field => field.FieldType.FullName));

            foreach (string entry in surface)
            {
                foreach (string banned in forbidden)
                {
                    Assert.That(
                        entry.IndexOf(banned, StringComparison.Ordinal),
                        Is.LessThan(0),
                        "The presenter must stay visual-only: " + entry);
                }
            }
        }

        /// <summary>
        /// Reads a reveal that must resolve without the player loop running.
        /// </summary>
        private static WorkshopRevealPlaybackResult Settled(
            UniTask<WorkshopRevealPlaybackResult> task)
        {
            UniTask<WorkshopRevealPlaybackResult>.Awaiter awaiter =
                task.GetAwaiter();
            Assert.That(
                awaiter.IsCompleted,
                Is.True,
                "This reveal path must settle synchronously.");
            return awaiter.GetResult();
        }
    }
}
