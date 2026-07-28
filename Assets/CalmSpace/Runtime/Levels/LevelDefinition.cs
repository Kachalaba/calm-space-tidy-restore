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

        public string LevelId => _levelId;

        public string DisplayName => _displayName;

        public LevelType Type => _levelType;
    }
}
