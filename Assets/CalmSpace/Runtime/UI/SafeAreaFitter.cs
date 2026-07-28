using UnityEngine;

namespace CalmSpace.UI
{
    /// <summary>
    /// Fits a RectTransform to Android cutouts and system bars. Anchors are
    /// recalculated only when the safe area or resolution changes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField]
        private bool _applyHorizontal = true;

        [SerializeField]
        private bool _applyVertical = true;

        [SerializeField]
        private Vector4 _paddingPixels;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            ApplyIfChanged(true);
        }

        private void Update()
        {
            ApplyIfChanged(false);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                ApplyIfChanged(false);
            }
        }

        private void ApplyIfChanged(bool force)
        {
            var width = Screen.width;
            var height = Screen.height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            if (!force &&
                safeArea == _lastSafeArea &&
                width == _lastScreenWidth &&
                height == _lastScreenHeight)
            {
                return;
            }

            _lastSafeArea = safeArea;
            _lastScreenWidth = width;
            _lastScreenHeight = height;

            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            var xMin = _applyHorizontal
                ? safeArea.xMin + _paddingPixels.x
                : 0f;
            var yMin = _applyVertical
                ? safeArea.yMin + _paddingPixels.y
                : 0f;
            var xMax = _applyHorizontal
                ? safeArea.xMax - _paddingPixels.z
                : width;
            var yMax = _applyVertical
                ? safeArea.yMax - _paddingPixels.w
                : height;

            xMin = Mathf.Clamp(xMin, 0f, width);
            yMin = Mathf.Clamp(yMin, 0f, height);
            xMax = Mathf.Clamp(xMax, xMin, width);
            yMax = Mathf.Clamp(yMax, yMin, height);

            _rectTransform.anchorMin =
                new Vector2(xMin / width, yMin / height);
            _rectTransform.anchorMax =
                new Vector2(xMax / width, yMax / height);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
