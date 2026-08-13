using System;
using System.Collections.Generic;
using System.Threading;
using CalmSpace.Workshop;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CalmSpace.UI
{
    /// <summary>
    /// One authored visual zone of the room. Each binding owns a permanent
    /// restored layer, a temporary "before" layer used only while a reveal
    /// plays, and exactly one hotspot.
    /// </summary>
    [Serializable]
    public sealed class WorkshopRoomBeatBinding
    {
        [SerializeField] private string _beatId = string.Empty;
        [SerializeField] private int _zoneIndex;
        [SerializeField] private CanvasGroup _restoredGroup;
        [SerializeField] private CanvasGroup _beforeGroup;
        [SerializeField] private Button _hotspot;

        public string BeatId => _beatId;
        public int ZoneIndex => _zoneIndex;
        public CanvasGroup RestoredGroup => _restoredGroup;
        public CanvasGroup BeforeGroup => _beforeGroup;
        public Button Hotspot => _hotspot;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_beatId) &&
            _zoneIndex >= 0 &&
            _restoredGroup != null &&
            _beforeGroup != null &&
            _hotspot != null;

        public void Configure(
            string beatId,
            int zoneIndex,
            CanvasGroup restoredGroup,
            CanvasGroup beforeGroup,
            Button hotspot)
        {
            _beatId = beatId ?? string.Empty;
            _zoneIndex = zoneIndex;
            _restoredGroup = restoredGroup;
            _beforeGroup = beforeGroup;
            _hotspot = hotspot;
        }
    }

    /// <summary>
    /// Visual-only presenter for the illustrated workshop room. It never reads
    /// or writes the profile, decides progression, emits analytics, loads
    /// levels, or marks a presentation seen. All state arrives through
    /// <see cref="ApplyState"/>; interaction leaves through stable beat IDs.
    /// There is no per-frame work while the room simply sits there.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorkshopRoomPresenter : MonoBehaviour
    {
        public const float RevealSeconds = 3.6f;

        [SerializeField] private string _chapterId = string.Empty;
        [SerializeField] private GameObject _roomRoot;
        [SerializeField] private GameObject _ambientRoot;
        [SerializeField] private CanvasGroup _finaleGroup;
        [SerializeField] private WorkshopRoomBeatBinding[] _beats =
            Array.Empty<WorkshopRoomBeatBinding>();

        private readonly Dictionary<string, int> _indexByBeatId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _reportedMissingBeatIds =
            new HashSet<string>(StringComparer.Ordinal);

        private UnityAction[] _hotspotActions;
        private bool _bound;
        private bool _visible;
        private bool _interactionEnabled = true;
        private bool _revealPlaying;
        private string _revealBeatId = string.Empty;
        [NonSerialized] private Func<CancellationToken, UniTask<float>>
            _revealFrameDriver = WaitForProductionRevealFrameAsync;

        public event Action<string> HotspotPressed;

        public string ChapterId => _chapterId;

        public bool IsVisible =>
            _roomRoot != null && _roomRoot.activeSelf;

        public bool IsRevealPlaying => _revealPlaying;

        internal Func<CancellationToken, UniTask<float>> RevealFrameDriver
        {
            set => _revealFrameDriver =
                value ?? WaitForProductionRevealFrameAsync;
        }

        public int BeatCount => _beats?.Length ?? 0;

        public WorkshopRoomBeatBinding GetBeat(int index)
        {
            return _beats != null && index >= 0 && index < _beats.Length
                ? _beats[index]
                : null;
        }

        public void Configure(
            string chapterId,
            GameObject roomRoot,
            GameObject ambientRoot,
            CanvasGroup finaleGroup,
            WorkshopRoomBeatBinding[] beats)
        {
            Unbind();
            _chapterId = chapterId ?? string.Empty;
            _roomRoot = roomRoot;
            _ambientRoot = ambientRoot;
            _finaleGroup = finaleGroup;
            _beats = beats ?? Array.Empty<WorkshopRoomBeatBinding>();
            _indexByBeatId.Clear();
        }

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Hotspot listeners and the beat lookup are built exactly once, so a
        /// state refresh never rebuilds collections or listeners.
        /// </summary>
        private void Bind()
        {
            if (_bound || _beats == null)
            {
                return;
            }

            _indexByBeatId.Clear();
            PrepareLayer(_finaleGroup);
            _hotspotActions = new UnityAction[_beats.Length];
            for (var index = 0; index < _beats.Length; index++)
            {
                WorkshopRoomBeatBinding binding = _beats[index];
                if (binding == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(binding.BeatId) &&
                    !_indexByBeatId.ContainsKey(binding.BeatId))
                {
                    _indexByBeatId.Add(binding.BeatId, index);
                }

                PrepareLayer(binding.RestoredGroup);
                PrepareLayer(binding.BeforeGroup);

                if (binding.Hotspot == null)
                {
                    continue;
                }

                string beatId = binding.BeatId;
                UnityAction action = () => HotspotPressed?.Invoke(beatId);
                _hotspotActions[index] = action;
                binding.Hotspot.onClick.AddListener(action);
            }

            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _beats == null || _hotspotActions == null)
            {
                _bound = false;
                return;
            }

            for (var index = 0;
                 index < _beats.Length && index < _hotspotActions.Length;
                 index++)
            {
                if (_beats[index]?.Hotspot != null &&
                    _hotspotActions[index] != null)
                {
                    _beats[index].Hotspot.onClick.RemoveListener(
                        _hotspotActions[index]);
                }
            }

            _hotspotActions = null;
            _bound = false;
        }

        private static void PrepareLayer(CanvasGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void ApplyState(WorkshopRoomVisualState state)
        {
            Bind();
            if (_beats == null)
            {
                return;
            }

            for (var index = 0; index < _beats.Length; index++)
            {
                WorkshopRoomBeatBinding binding = _beats[index];
                if (binding == null)
                {
                    continue;
                }

                SetAlpha(
                    binding.RestoredGroup,
                    state.IsZoneRestored(binding.ZoneIndex) ? 1f : 0f);

                // A temporary overlay only exists while its own reveal plays.
                if (!string.Equals(
                        binding.BeatId, _revealBeatId, StringComparison.Ordinal))
                {
                    SetAlpha(binding.BeforeGroup, 0f);
                }

                SetActive(
                    binding.Hotspot,
                    !_revealPlaying &&
                    !string.IsNullOrEmpty(state.ActiveHotspotBeatId) &&
                    string.Equals(
                        binding.BeatId,
                        state.ActiveHotspotBeatId,
                        StringComparison.Ordinal));
            }

            SetAlpha(_finaleGroup, state.IsComplete ? 1f : 0f);
            ApplyInteraction();
        }

        public void SetVisible(bool visible)
        {
            Bind();
            _visible = visible;
            SetActive(_roomRoot, visible);
            SetActive(_ambientRoot, visible);
            ApplyInteraction();
        }

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
            ApplyInteraction();
        }

        private void ApplyInteraction()
        {
            bool interactive =
                _visible && _interactionEnabled && !_revealPlaying;
            if (_beats == null)
            {
                return;
            }

            for (var index = 0; index < _beats.Length; index++)
            {
                Button hotspot = _beats[index]?.Hotspot;
                if (hotspot != null && hotspot.interactable != interactive)
                {
                    hotspot.interactable = interactive;
                }
            }
        }

        /// <summary>
        /// Places the zone's temporary "before" overlay above an already
        /// correct permanent room and dissolves it. Nothing is persisted:
        /// cancellation restores the completed room and reports it, so an
        /// interrupted reveal can simply be played again next time.
        /// </summary>
        public async UniTask<WorkshopRevealPlaybackResult> PlayRevealAsync(
            string beatId,
            CancellationToken cancellationToken)
        {
            Bind();
            if (!TryGetBinding(beatId, out WorkshopRoomBeatBinding binding))
            {
                ReportMissingVisual(beatId);
                return WorkshopRevealPlaybackResult.MissingVisual;
            }

            if (cancellationToken.IsCancellationRequested || _revealPlaying)
            {
                return WorkshopRevealPlaybackResult.Cancelled;
            }

            bool previousInteraction = _interactionEnabled;
            _revealPlaying = true;
            _revealBeatId = binding.BeatId;
            SetAlpha(binding.BeforeGroup, 1f);
            SetActive(binding.Hotspot, false);
            ApplyInteraction();

            try
            {
                var elapsed = 0f;
                while (elapsed < RevealSeconds)
                {
                    elapsed += await _revealFrameDriver(cancellationToken);
                    float progress = Mathf.Clamp01(elapsed / RevealSeconds);
                    SetAlpha(
                        binding.BeforeGroup,
                        1f - Mathf.SmoothStep(0f, 1f, progress));
                }

                return WorkshopRevealPlaybackResult.Completed;
            }
            catch (OperationCanceledException exception)
            {
                if (exception.CancellationToken == cancellationToken)
                {
                    throw;
                }

                return WorkshopRevealPlaybackResult.Cancelled;
            }
            finally
            {
                SetAlpha(binding.BeforeGroup, 0f);
                _revealBeatId = string.Empty;
                _revealPlaying = false;
                _interactionEnabled = previousInteraction;
                ApplyInteraction();
            }
        }

        private static async UniTask<float> WaitForProductionRevealFrameAsync(
            CancellationToken cancellationToken)
        {
            await UniTask.Yield(
                PlayerLoopTiming.Update, cancellationToken);
            return Time.unscaledDeltaTime;
        }

        private bool TryGetBinding(
            string beatId,
            out WorkshopRoomBeatBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(beatId) ||
                !_indexByBeatId.TryGetValue(beatId, out int index))
            {
                return false;
            }

            binding = GetBeat(index);
            return binding != null && binding.IsValid;
        }

        private void ReportMissingVisual(string beatId)
        {
            string stableId = string.IsNullOrWhiteSpace(beatId)
                ? "<empty>"
                : beatId;
            if (!_reportedMissingBeatIds.Add(stableId))
            {
                return;
            }

            Debug.LogWarning(
                "Workshop room has no authored visual for beat " +
                stableId + "; the room stays usable.",
                this);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null && !Mathf.Approximately(group.alpha, alpha))
            {
                group.alpha = alpha;
            }
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null)
            {
                SetActive(button.gameObject, active);
            }
        }

        private static void SetActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        private void OnDestroy()
        {
            Unbind();
            HotspotPressed = null;
        }
    }
}
