using UnityEngine;

namespace CalmSpace.Levels
{
    public enum LevelType
    {
        Sorting = 0,
        Cleaning = 1,
        ScrewPuzzle = 2,
        Fitting = 3
    }

    [CreateAssetMenu(
        fileName = "LevelDefinition",
        menuName = "Calm Space/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField]
        private string _levelId = string.Empty;

        [SerializeField]
        private string _displayName = string.Empty;

        [SerializeField]
        private LevelType _levelType;

        [Header("Restoration chapter")]
        [SerializeField]
        private string _restorationChapterId = string.Empty;

        [SerializeField]
        [Min(0)]
        private int _restorationStageIndex;

        [SerializeField]
        [Min(0)]
        private int _restorationStageCount;

        public string LevelId => _levelId;

        public string DisplayName => _displayName;

        public LevelType Type => _levelType;

        public string RestorationChapterId =>
            _restorationChapterId;

        public int RestorationStageIndex =>
            _restorationStageIndex;

        public int RestorationStageCount =>
            _restorationStageCount;

        public bool TryGetRestorationStage(
            out RestorationStageInfo stage)
        {
            return RestorationProgressRules.TryCreateStage(
                this,
                out stage);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            bool hasChapter =
                !string.IsNullOrWhiteSpace(_restorationChapterId);
            bool hasStageData =
                _restorationStageCount != 0 ||
                _restorationStageIndex != 0;
            if (!hasChapter && !hasStageData)
            {
                return;
            }

            if (!hasChapter ||
                _restorationStageCount <= 0 ||
                _restorationStageIndex < 0 ||
                _restorationStageIndex >= _restorationStageCount)
            {
                Debug.LogError(
                    $"Level '{name}' has invalid restoration metadata. " +
                    "Assign a chapter id and a zero-based stage index within " +
                    "the authored stage count.",
                    this);
            }
        }
#endif
    }
}
