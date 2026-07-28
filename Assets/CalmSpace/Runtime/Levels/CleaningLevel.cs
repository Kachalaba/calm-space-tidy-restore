using System;
using CalmSpace.Cleaning;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CalmSpace.Levels
{
    /// <summary>
    /// Cleaning levels complete from GPU-mask coverage rather than item count.
    /// </summary>
    public sealed class CleaningLevel : LevelBase
    {
        [SerializeField]
        private RenderTextureCleaner _cleaner;

        private bool _isCleaned;

        public override LevelType SupportedType => LevelType.Cleaning;

        public override bool CheckWinCondition()
        {
            return _isCleaned;
        }

        protected override void OnLevelInitialized()
        {
            if (_cleaner == null)
            {
                throw new InvalidOperationException(
                    "CleaningLevel requires a RenderTextureCleaner.");
            }

            _isCleaned = _cleaner.CleanedFraction >= 0.95f;
            _cleaner.OnCleaned100Percent += HandleCleaned;
        }

        protected override void OnDestroy()
        {
            if (_cleaner != null)
            {
                _cleaner.OnCleaned100Percent -= HandleCleaned;
            }

            base.OnDestroy();
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
