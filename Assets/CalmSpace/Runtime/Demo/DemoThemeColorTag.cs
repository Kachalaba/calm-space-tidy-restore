using UnityEngine;

namespace CalmSpace.Demo
{
    public enum DemoThemeColorRole
    {
        Board = 0,
        Piece = 1,
        Socket = 2
    }

    /// <summary>
    /// Optional authoring tag for deterministic theme assignment. Addressable
    /// content without tags is still supported through conservative name-based
    /// fallback rules in LevelThemeApplicator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoThemeColorTag : MonoBehaviour
    {
        [SerializeField]
        private DemoThemeColorRole _role =
            DemoThemeColorRole.Piece;

        [SerializeField]
        private int _paletteIndex;

        public DemoThemeColorRole Role => _role;

        public int PaletteIndex => _paletteIndex;
    }
}
