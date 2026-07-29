using System;
using System.Collections.Generic;
using CalmSpace.Input;
using UnityEngine;

namespace CalmSpace.Cleaning
{
    /// <summary>
    /// The implement a stage is worked with. A tool is a brush profile and a
    /// feel, not a separate code path: a sponge covers less per pass than a
    /// cloth, a squeegee clears a wide hard-edged band.
    /// </summary>
    public enum CleaningToolKind
    {
        Hands = 0,
        Cloth = 1,
        Sponge = 2,
        Squeegee = 3
    }

    /// <summary>
    /// One pass of a cleaning level: an optional surface to wipe and an
    /// optional set of debris to clear away first. A stage is finished when
    /// both halves are done, and finished stages stay on screen — the level
    /// only ever reveals the next layer of work on top of visible progress.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CleaningStage : MonoBehaviour
    {
        [SerializeField]
        private GameObject _stageRoot;

        [SerializeField]
        private RenderTextureCleaner _cleaner;

        [SerializeField]
        private ItemSnapController[] _debris =
            Array.Empty<ItemSnapController>();

        [SerializeField]
        private CleaningToolKind _tool = CleaningToolKind.Cloth;

        [Range(0.001f, 0.5f)]
        [SerializeField]
        private float _brushRadiusUv = 0.095f;

        [Range(0f, 1f)]
        [SerializeField]
        private float _brushHardness = 0.8f;

        private readonly HashSet<int> _clearedDebris =
            new HashSet<int>();

        private bool _isActivated;
        private bool _isComplete;
        private bool _surfaceCleaned;
        private int _requiredDebrisCount;

        public event Action<CleaningStage> Completed;

        /// <summary>
        /// Raised after debris state has actually changed. LevelBase gets the
        /// item's Placed event first, so without this the HUD would render a
        /// stage fraction that is one piece of debris out of date.
        /// </summary>
        public event Action<CleaningStage> ProgressChanged;

        public RenderTextureCleaner Cleaner => _cleaner;

        /// <summary>
        /// The surface this stage currently permits the player to work on.
        /// Debris must be cleared before a mixed stage exposes its cleaner.
        /// </summary>
        public RenderTextureCleaner ActiveCleaner =>
            _isActivated && !HasPendingDebris
                ? _cleaner
                : null;

        /// <summary>
        /// What the player is doing right now. Debris comes off by hand
        /// before the implement is any use, so the readout says so.
        /// </summary>
        public CleaningToolKind Tool =>
            _clearedDebris.Count < _requiredDebrisCount
                ? CleaningToolKind.Hands
                : _tool;

        public bool IsActivated => _isActivated;

        public bool IsComplete => _isComplete;

        public int DebrisCount => _requiredDebrisCount;

        public int ClearedDebrisCount => _clearedDebris.Count;

        /// <summary>
        /// How far this pass has come, 0..1. Debris is counted first because
        /// that is the work the player is actually being asked for; wiping is
        /// normalised against the completion threshold so the readout reaches
        /// 100% exactly when the stage does.
        /// </summary>
        public float Fraction
        {
            get
            {
                if (_isComplete)
                {
                    return 1f;
                }

                bool hasDebris = _requiredDebrisCount > 0;
                bool hasSurface = _cleaner != null;
                float debrisFraction = hasDebris
                    ? Mathf.Clamp01(
                        (float)_clearedDebris.Count /
                        _requiredDebrisCount)
                    : 1f;

                if (hasDebris && hasSurface)
                {
                    if (HasPendingDebris)
                    {
                        return debrisFraction * 0.5f;
                    }

                    return 0.5f +
                           GetNormalizedSurfaceFraction() * 0.5f;
                }

                if (hasDebris)
                {
                    return debrisFraction;
                }

                return hasSurface
                    ? GetNormalizedSurfaceFraction()
                    : 1f;
            }
        }

        /// <summary>
        /// Hides the stage until the level reaches it. Safe to call before
        /// any Unity lifecycle callback has run on the stage contents.
        /// </summary>
        public void PrepareHidden()
        {
            GameObject root = ResolveRoot();
            if (root != gameObject)
            {
                root.SetActive(false);
            }

            if (_cleaner != null)
            {
                _cleaner.gameObject.SetActive(false);
            }

            SetDebrisActive(false);
        }

        public void Activate()
        {
            if (_isActivated)
            {
                return;
            }

            _isActivated = true;

            if (_cleaner != null)
            {
                _cleaner.gameObject.SetActive(false);
            }

            ResolveRoot().SetActive(true);

            SetDebrisActive(true);
            SubscribeDebris();

            if (!HasPendingDebris)
            {
                ActivateSurface();
            }

            EvaluateCompletion();
        }

        private void OnDestroy()
        {
            if (_cleaner != null)
            {
                _cleaner.OnCleaned100Percent -= HandleSurfaceCleaned;
            }

            if (_debris == null)
            {
                return;
            }

            for (var index = 0; index < _debris.Length; index++)
            {
                ItemSnapController item = _debris[index];
                if (item != null)
                {
                    item.Placed -= HandleDebrisPlaced;
                }
            }
        }

        private GameObject ResolveRoot()
        {
            return _stageRoot != null ? _stageRoot : gameObject;
        }

        private void SetDebrisActive(bool value)
        {
            if (_debris == null)
            {
                return;
            }

            for (var index = 0; index < _debris.Length; index++)
            {
                ItemSnapController item = _debris[index];
                if (item != null)
                {
                    item.gameObject.SetActive(value);
                }
            }
        }

        private void SubscribeDebris()
        {
            if (_debris == null)
            {
                return;
            }

            for (var index = 0; index < _debris.Length; index++)
            {
                ItemSnapController item = _debris[index];
                if (item == null)
                {
                    continue;
                }

                _requiredDebrisCount++;
                item.Placed += HandleDebrisPlaced;
                if (item.IsPlaced)
                {
                    _clearedDebris.Add(item.ItemInstanceId);
                }
            }
        }

        private void HandleSurfaceCleaned()
        {
            _surfaceCleaned = true;
            EvaluateCompletion();
        }

        private void HandleDebrisPlaced(ItemSnapController item)
        {
            if (item == null ||
                !_clearedDebris.Add(item.ItemInstanceId))
            {
                return;
            }

            if (!HasPendingDebris)
            {
                ActivateSurface();
            }

            Invoke(ProgressChanged);
            EvaluateCompletion();
        }

        private bool HasPendingDebris =>
            _clearedDebris.Count < _requiredDebrisCount;

        private float GetNormalizedSurfaceFraction()
        {
            return _cleaner == null
                ? 1f
                : Mathf.Clamp01(
                    _cleaner.CleanedFraction /
                    RenderTextureCleaner.CompletionThreshold);
        }

        private void ActivateSurface()
        {
            if (_cleaner == null)
            {
                _surfaceCleaned = true;
                return;
            }

            _cleaner.gameObject.SetActive(true);
            _cleaner.ConfigureBrush(
                _brushRadiusUv,
                _brushHardness);
            _cleaner.OnCleaned100Percent += HandleSurfaceCleaned;
            _surfaceCleaned = _cleaner.CleanedFraction >=
                RenderTextureCleaner.CompletionThreshold;
        }

        private void EvaluateCompletion()
        {
            if (_isComplete ||
                !_surfaceCleaned ||
                _clearedDebris.Count < _requiredDebrisCount)
            {
                return;
            }

            _isComplete = true;
            Invoke(Completed);
        }

        private void Invoke(Action<CleaningStage> handler)
        {
            if (handler == null)
            {
                return;
            }

            foreach (Action<CleaningStage> subscriber in
                     handler.GetInvocationList())
            {
                try
                {
                    subscriber(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
