using System;
using UnityEngine;

namespace CalmSpace.Demo
{
    /// <summary>
    /// Complete visual palette shared by the shell, board, and Addressable
    /// level content. Values stay data-driven so art direction can evolve
    /// without touching gameplay code.
    /// </summary>
    [Serializable]
    public sealed class ThemePalette
    {
        [SerializeField]
        private string _id = string.Empty;

        [SerializeField]
        private string _displayName = string.Empty;

        [Header("Interface")]
        [SerializeField]
        private Color _background =
            new Color(0.055f, 0.09f, 0.085f, 1f);

        [SerializeField]
        private Color _panel =
            new Color(0.10f, 0.16f, 0.145f, 0.96f);

        [SerializeField]
        private Color _accent =
            new Color(1f, 0.47f, 0.38f, 1f);

        [SerializeField]
        private Color _secondaryAccent =
            new Color(0.47f, 0.78f, 0.65f, 1f);

        [SerializeField]
        private Color _primaryText =
            new Color(1f, 0.97f, 0.91f, 1f);

        [SerializeField]
        private Color _secondaryText =
            new Color(0.76f, 0.82f, 0.76f, 1f);

        [Header("World")]
        [SerializeField]
        private Color _board =
            new Color(0.78f, 0.72f, 0.62f, 1f);

        [SerializeField]
        private Color _pieceA =
            new Color(0.96f, 0.42f, 0.34f, 1f);

        [SerializeField]
        private Color _pieceB =
            new Color(0.32f, 0.72f, 0.58f, 1f);

        [SerializeField]
        private Color _pieceC =
            new Color(0.94f, 0.68f, 0.25f, 1f);

        [SerializeField]
        private Color _socket =
            new Color(0.54f, 0.50f, 0.43f, 1f);

        public ThemePalette()
        {
        }

        public ThemePalette(
            string id,
            string displayName,
            Color background,
            Color panel,
            Color accent,
            Color secondaryAccent,
            Color primaryText,
            Color secondaryText,
            Color board,
            Color pieceA,
            Color pieceB,
            Color pieceC,
            Color socket)
        {
            _id = id;
            _displayName = displayName;
            _background = background;
            _panel = panel;
            _accent = accent;
            _secondaryAccent = secondaryAccent;
            _primaryText = primaryText;
            _secondaryText = secondaryText;
            _board = board;
            _pieceA = pieceA;
            _pieceB = pieceB;
            _pieceC = pieceC;
            _socket = socket;
        }

        public string Id => _id;

        public string DisplayName => _displayName;

        public Color Background => _background;

        public Color Panel => _panel;

        public Color Accent => _accent;

        public Color SecondaryAccent => _secondaryAccent;

        public Color PrimaryText => _primaryText;

        public Color SecondaryText => _secondaryText;

        public Color Board => _board;

        public Color Socket => _socket;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_id) &&
            !string.IsNullOrWhiteSpace(_displayName);

        public Color GetPieceColor(int index)
        {
            var normalized = index % 3;
            if (normalized < 0)
            {
                normalized += 3;
            }

            switch (normalized)
            {
                case 0:
                    return _pieceA;
                case 1:
                    return _pieceB;
                default:
                    return _pieceC;
            }
        }

        public static ThemePalette[] CreateBuiltInPalettes()
        {
            return new[]
            {
                new ThemePalette(
                    "sage",
                    "Quiet Sage",
                    Hex("0E1B19"),
                    Hex("183029", 0.96f),
                    Hex("FF7968"),
                    Hex("79C8A4"),
                    Hex("FFF7E9"),
                    Hex("BDD0C4"),
                    Hex("D7C9AD"),
                    Hex("F26F5B"),
                    Hex("6EC4A0"),
                    Hex("F2B54A"),
                    Hex("857D6D")),
                new ThemePalette(
                    "ocean",
                    "Blue Hour",
                    Hex("0C1830"),
                    Hex("142A4A", 0.96f),
                    Hex("66D5D1"),
                    Hex("A896FF"),
                    Hex("F7F7FF"),
                    Hex("B8C8DF"),
                    Hex("C9D4DC"),
                    Hex("5AC8D8"),
                    Hex("8E80ED"),
                    Hex("F5A96B"),
                    Hex("758695")),
                new ThemePalette(
                    "sunset",
                    "Soft Sunset",
                    Hex("28162D"),
                    Hex("43233D", 0.96f),
                    Hex("FF9D68"),
                    Hex("D8A8F2"),
                    Hex("FFF5EF"),
                    Hex("DCC3D8"),
                    Hex("D9C0B7"),
                    Hex("FF8C61"),
                    Hex("C892E8"),
                    Hex("F6C65D"),
                    Hex("8C706F"))
            };
        }

        private static Color Hex(
            string rgb,
            float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(
                    "#" + rgb,
                    out var color))
            {
                return Color.magenta;
            }

            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }

    [CreateAssetMenu(
        fileName = "DemoThemeCatalog",
        menuName = "Calm Space/Demo Theme Catalog")]
    public sealed class DemoThemeCatalog : ScriptableObject
    {
        [SerializeField]
        private ThemePalette[] _palettes =
            ThemePalette.CreateBuiltInPalettes();

        [SerializeField]
        [Min(0)]
        private int _defaultPaletteIndex;

        public int Count => _palettes?.Length ?? 0;

        public ThemePalette DefaultPalette
        {
            get
            {
                if (_palettes == null || _palettes.Length == 0)
                {
                    return null;
                }

                var index = Mathf.Clamp(
                    _defaultPaletteIndex,
                    0,
                    _palettes.Length - 1);
                return _palettes[index];
            }
        }

        public bool TryGetPalette(
            int index,
            out ThemePalette palette)
        {
            if (_palettes == null ||
                index < 0 ||
                index >= _palettes.Length)
            {
                palette = null;
                return false;
            }

            palette = _palettes[index];
            return palette != null && palette.IsValid;
        }

        public bool TryFindPalette(
            string themeId,
            out int index,
            out ThemePalette palette)
        {
            index = -1;
            palette = null;
            if (string.IsNullOrWhiteSpace(themeId) ||
                _palettes == null)
            {
                return false;
            }

            for (var candidateIndex = 0;
                 candidateIndex < _palettes.Length;
                 candidateIndex++)
            {
                var candidate = _palettes[candidateIndex];
                if (candidate == null ||
                    !candidate.IsValid ||
                    !string.Equals(
                        candidate.Id,
                        themeId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                index = candidateIndex;
                palette = candidate;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _palettes = ThemePalette.CreateBuiltInPalettes();
            _defaultPaletteIndex = 0;
        }

        private void OnValidate()
        {
            if (_palettes == null || _palettes.Length == 0)
            {
                _palettes = ThemePalette.CreateBuiltInPalettes();
            }

            _defaultPaletteIndex = Mathf.Clamp(
                _defaultPaletteIndex,
                0,
                Mathf.Max(0, _palettes.Length - 1));

            for (var first = 0; first < _palettes.Length; first++)
            {
                var firstPalette = _palettes[first];
                if (firstPalette == null || !firstPalette.IsValid)
                {
                    continue;
                }

                for (var second = first + 1;
                     second < _palettes.Length;
                     second++)
                {
                    var secondPalette = _palettes[second];
                    if (secondPalette != null &&
                        string.Equals(
                            firstPalette.Id,
                            secondPalette.Id,
                            StringComparison.Ordinal))
                    {
                        Debug.LogError(
                            $"Duplicate demo theme id " +
                            $"'{firstPalette.Id}'.",
                            this);
                    }
                }
            }
        }
#endif
    }
}
