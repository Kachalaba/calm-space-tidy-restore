using System;
using UnityEngine;

namespace CalmSpace.Demo
{
    /// <summary>
    /// Applies a selected palette without instantiating or mutating shared
    /// materials. Work occurs only on theme changes and Addressable loads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelThemeApplicator : MonoBehaviour
    {
        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        [SerializeField]
        private Camera _camera;

        [SerializeField]
        private Renderer[] _boardRenderers =
            Array.Empty<Renderer>();

        private MaterialPropertyBlock _propertyBlock;
        private GameObject _currentLevelRoot;

        public GameObject CurrentLevelRoot => _currentLevelRoot;

        public void ApplyScenePalette(ThemePalette palette)
        {
            if (palette == null)
            {
                return;
            }

            EnsurePropertyBlock();
            if (_camera != null)
            {
                _camera.backgroundColor = palette.Background;
            }

            if (_boardRenderers == null)
            {
                return;
            }

            for (var index = 0;
                 index < _boardRenderers.Length;
                 index++)
            {
                ApplyColor(
                    _boardRenderers[index],
                    palette.Board);
            }
        }

        public void ApplyLevelPalette(
            GameObject levelRoot,
            ThemePalette palette)
        {
            _currentLevelRoot = levelRoot;
            ApplyRootPalette(levelRoot, palette);
        }

        /// <summary>
        /// Applies a palette to an arbitrary themed hierarchy without
        /// replacing the Addressable level tracked by this applicator.
        /// </summary>
        public void ApplyRootPalette(
            GameObject root,
            ThemePalette palette)
        {
            if (root == null || palette == null)
            {
                return;
            }

            EnsurePropertyBlock();
            var tags =
                root.GetComponentsInChildren<
                    DemoThemeColorTag>(true);
            if (tags.Length > 0)
            {
                for (var index = 0; index < tags.Length; index++)
                {
                    ApplyTag(tags[index], palette);
                }

                return;
            }

            ApplyFallback(root, palette);
        }

        public void ClearCurrentLevel()
        {
            _currentLevelRoot = null;
        }

        private void ApplyTag(
            DemoThemeColorTag tag,
            ThemePalette palette)
        {
            if (tag == null)
            {
                return;
            }

            var renderers =
                tag.GetComponentsInChildren<Renderer>(true);
            var color = ResolveColor(
                palette,
                tag.Role,
                tag.PaletteIndex);
            for (var index = 0; index < renderers.Length; index++)
            {
                ApplyColor(renderers[index], color);
            }
        }

        private void ApplyFallback(
            GameObject levelRoot,
            ThemePalette palette)
        {
            var renderers =
                levelRoot.GetComponentsInChildren<Renderer>(true);
            var pieceIndex = 0;
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                var objectName = renderer.gameObject.name;
                if (ContainsOrdinalIgnoreCase(
                        objectName,
                        "socket") ||
                    ContainsOrdinalIgnoreCase(
                        objectName,
                        "target"))
                {
                    ApplyColor(renderer, palette.Socket);
                }
                else if (
                    ContainsOrdinalIgnoreCase(
                        objectName,
                        "piece") ||
                    ContainsOrdinalIgnoreCase(
                        objectName,
                        "item") ||
                    ContainsOrdinalIgnoreCase(
                        objectName,
                        "screw") ||
                    ContainsOrdinalIgnoreCase(
                        objectName,
                        "pebble"))
                {
                    ApplyColor(
                        renderer,
                        palette.GetPieceColor(pieceIndex));
                    pieceIndex++;
                }
            }
        }

        private void ApplyColor(
            Renderer renderer,
            Color color)
        {
            if (renderer == null)
            {
                return;
            }

            EnsurePropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
            _propertyBlock.Clear();
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }
        }

        private static Color ResolveColor(
            ThemePalette palette,
            DemoThemeColorRole role,
            int paletteIndex)
        {
            switch (role)
            {
                case DemoThemeColorRole.Board:
                    return palette.Board;
                case DemoThemeColorRole.Socket:
                    return palette.Socket;
                default:
                    return palette.GetPieceColor(paletteIndex);
            }
        }

        private static bool ContainsOrdinalIgnoreCase(
            string value,
            string candidate)
        {
            return
                !string.IsNullOrEmpty(value) &&
                value.IndexOf(
                    candidate,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
