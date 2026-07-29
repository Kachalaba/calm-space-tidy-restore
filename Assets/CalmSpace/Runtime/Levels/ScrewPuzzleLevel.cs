using System;
using System.Collections.Generic;
using CalmSpace.Fasteners;
using Cysharp.Threading.Tasks;

namespace CalmSpace.Levels
{
    /// <summary>
    /// Screw levels are finished when every screw is out and every panel that
    /// came loose has been put away, so progress covers both halves of the
    /// work rather than the placed items alone.
    /// </summary>
    public sealed class ScrewPuzzleLevel : LevelBase
    {
        private readonly HashSet<int> _removedScrews =
            new HashSet<int>();

        private ScrewController[] _screws =
            Array.Empty<ScrewController>();

        public override LevelType SupportedType =>
            LevelType.ScrewPuzzle;

        public int ScrewCount => _screws.Length;

        public int RemovedScrewCount => _removedScrews.Count;

        public override bool CheckWinCondition()
        {
            return _removedScrews.Count >= _screws.Length &&
                   base.CheckWinCondition();
        }

        protected override void OnLevelInitialized()
        {
            _screws =
                GetComponentsInChildren<ScrewController>(true);
            _removedScrews.Clear();

            for (var index = 0; index < _screws.Length; index++)
            {
                ScrewController screw = _screws[index];
                if (screw == null)
                {
                    continue;
                }

                screw.Removed += HandleScrewRemoved;
                if (screw.IsRemoved)
                {
                    _removedScrews.Add(screw.GetInstanceID());
                }
            }
        }

        protected override int GetProgressCurrent()
        {
            return _removedScrews.Count + base.GetProgressCurrent();
        }

        protected override int GetProgressTotal()
        {
            return _screws.Length + base.GetProgressTotal();
        }

        protected override void OnDestroy()
        {
            for (var index = 0; index < _screws.Length; index++)
            {
                ScrewController screw = _screws[index];
                if (screw != null)
                {
                    screw.Removed -= HandleScrewRemoved;
                }
            }

            base.OnDestroy();
        }

        private void HandleScrewRemoved(ScrewController screw)
        {
            if (screw == null ||
                !_removedScrews.Add(screw.GetInstanceID()))
            {
                return;
            }

            RaiseProgressChanged();

            if (CheckWinCondition())
            {
                CompleteLevel().Forget();
            }
        }
    }
}
