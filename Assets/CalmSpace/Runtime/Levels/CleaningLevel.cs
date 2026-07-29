using System;
using System.Threading;
using CalmSpace.Cleaning;
using CalmSpace.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CalmSpace.Levels
{
    /// <summary>
    /// Cleaning levels complete from GPU-mask coverage rather than item count.
    ///
    /// A level may be a single surface (one <see cref="RenderTextureCleaner"/>
    /// wired to <c>_cleaner</c>) or an ordered run of
    /// <see cref="CleaningStage"/> passes — clear the debris, work the sponge,
    /// pull the squeegee. Finished stages stay on screen; only the next one is
    /// revealed, so the surface visibly improves rather than resetting.
    /// </summary>
    public sealed class CleaningLevel : LevelBase
    {
        [SerializeField]
        private RenderTextureCleaner _cleaner;

        [SerializeField]
        private CleaningStage[] _stages =
            Array.Empty<CleaningStage>();

        [SerializeField]
        private CleaningInputController _cleaningInput;

        [Tooltip("Quiet beat between stages so the change is not abrupt.")]
        [Min(0f)]
        [SerializeField]
        private float _stageSettleSeconds = 0.5f;

        private int _activeStageIndex = -1;
        private int _completedStageCount;
        private int _stageTransitionGeneration;
        private bool _isCleaned;

        /// <summary>
        /// Raised whenever the surface the player works on changes, so the
        /// HUD can follow the active stage instead of the first cleaner it
        /// happened to find in the prefab.
        /// </summary>
        public event Action<RenderTextureCleaner> ActiveCleanerChanged;

        public override LevelType SupportedType => LevelType.Cleaning;

        public int StageCount => _stages?.Length ?? 0;

        public int CompletedStageCount => _completedStageCount;

        public int ActiveStageIndex => _activeStageIndex;

        public CleaningToolKind ActiveTool =>
            TryGetStage(_activeStageIndex, out CleaningStage stage)
                ? stage.Tool
                : CleaningToolKind.Cloth;

        public RenderTextureCleaner ActiveCleaner =>
            TryGetStage(_activeStageIndex, out CleaningStage stage)
                ? stage.ActiveCleaner
                : _cleaner;

        /// <summary>
        /// Progress through the current pass, 0..1. Falls back to raw mask
        /// coverage on single-surface levels.
        /// </summary>
        public float ActiveStageFraction
        {
            get
            {
                if (TryGetStage(
                        _activeStageIndex,
                        out CleaningStage stage))
                {
                    return stage.Fraction;
                }

                return _cleaner == null
                    ? 0f
                    : _cleaner.CleanedFraction;
            }
        }

        public override bool CheckWinCondition()
        {
            if (StageCount == 0)
            {
                return _isCleaned;
            }

            return _completedStageCount >= StageCount &&
                   base.CheckWinCondition();
        }

        protected override void OnLevelInitialized()
        {
            if (StageCount > 0)
            {
                InitializeStages();
                return;
            }

            if (_cleaner == null)
            {
                throw new InvalidOperationException(
                    "CleaningLevel requires a RenderTextureCleaner.");
            }

            _isCleaned = _cleaner.CleanedFraction >=
                RenderTextureCleaner.CompletionThreshold;
            _cleaner.OnCleaned100Percent += HandleCleaned;
        }

        protected override int GetProgressCurrent()
        {
            return StageCount == 0
                ? base.GetProgressCurrent()
                : _completedStageCount;
        }

        protected override int GetProgressTotal()
        {
            return StageCount == 0
                ? base.GetProgressTotal()
                : StageCount;
        }

        protected override void OnDestroy()
        {
            if (_cleaner != null)
            {
                _cleaner.OnCleaned100Percent -= HandleCleaned;
            }

            if (_stages != null)
            {
                for (var index = 0; index < _stages.Length; index++)
                {
                    CleaningStage stage = _stages[index];
                    if (stage != null)
                    {
                        stage.Completed -= HandleStageCompleted;
                        stage.Reopened -= HandleStageReopened;
                        stage.ProgressChanged -=
                            HandleStageProgressChanged;
                    }
                }
            }

            base.OnDestroy();
        }

        private void InitializeStages()
        {
            for (var index = 0; index < _stages.Length; index++)
            {
                CleaningStage stage = _stages[index];
                if (stage == null)
                {
                    throw new InvalidOperationException(
                        "A staged CleaningLevel has an empty stage slot.");
                }

                stage.Completed += HandleStageCompleted;
                stage.Reopened += HandleStageReopened;
                stage.ProgressChanged += HandleStageProgressChanged;
                stage.PrepareHidden();
            }

            ActivateStage(0);
        }

        private void ActivateStage(int index)
        {
            if (!TryGetStage(index, out CleaningStage stage))
            {
                return;
            }

            _activeStageIndex = index;
            stage.Activate();
            BindActiveCleaner(stage.ActiveCleaner);
        }

        private void BindActiveCleaner(RenderTextureCleaner cleaner)
        {
            if (_cleaningInput != null)
            {
                _cleaningInput.SetActiveCleaner(cleaner);
            }

            var handler = ActiveCleanerChanged;
            if (handler == null)
            {
                return;
            }

            foreach (Action<RenderTextureCleaner> subscriber in
                     handler.GetInvocationList())
            {
                try
                {
                    subscriber(cleaner);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void HandleStageProgressChanged(CleaningStage stage)
        {
            if (stage != null &&
                TryGetStage(_activeStageIndex, out CleaningStage active) &&
                active == stage)
            {
                BindActiveCleaner(stage.ActiveCleaner);

                RaiseProgressChanged();
            }
        }

        private void HandleStageCompleted(CleaningStage stage)
        {
            if (stage == null ||
                !TryGetStage(_activeStageIndex, out CleaningStage active) ||
                active != stage)
            {
                return;
            }

            _completedStageCount++;
            RaiseProgressChanged();
            int generation = ++_stageTransitionGeneration;
            AdvanceStageAsync(
                    _activeStageIndex + 1,
                    generation,
                    this.GetCancellationTokenOnDestroy())
                .Forget();
        }

        private async UniTaskVoid AdvanceStageAsync(
            int nextIndex,
            int generation,
            CancellationToken cancellationToken)
        {
            // The player just finished a pass. Let it read before the next
            // layer appears.
            if (_stageSettleSeconds > 0f)
            {
                try
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_stageSettleSeconds),
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (this == null ||
                generation != _stageTransitionGeneration)
            {
                return;
            }

            if (nextIndex < StageCount)
            {
                ActivateStage(nextIndex);
                return;
            }

            _isCleaned = true;
            if (CheckWinCondition())
            {
                CompleteLevel().Forget();
            }
        }

        private void HandleStageReopened(CleaningStage stage)
        {
            if (stage == null)
            {
                return;
            }

            int reopenedIndex = Array.IndexOf(_stages, stage);
            if (reopenedIndex < 0)
            {
                return;
            }

            _stageTransitionGeneration++;
            _isCleaned = false;
            _completedStageCount = Mathf.Min(
                _completedStageCount,
                reopenedIndex);
            _activeStageIndex = reopenedIndex;

            for (var index = reopenedIndex + 1;
                 index < _stages.Length;
                 index++)
            {
                CleaningStage laterStage = _stages[index];
                if (laterStage == null)
                {
                    continue;
                }

                laterStage.SetPresented(false);
                laterStage.RearmCompletion();
            }

            stage.SetPresented(true);
            BindActiveCleaner(stage.ActiveCleaner);
            RaiseProgressChanged();
        }

        private bool TryGetStage(int index, out CleaningStage stage)
        {
            if (_stages == null ||
                index < 0 ||
                index >= _stages.Length)
            {
                stage = null;
                return false;
            }

            stage = _stages[index];
            return stage != null;
        }

        private void HandleCleaned()
        {
            if (_isCleaned)
            {
                return;
            }

            _isCleaned = true;
            CompleteLevel().Forget();
        }
    }
}
