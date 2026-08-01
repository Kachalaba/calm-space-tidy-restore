using System;
using UnityEngine;

namespace CalmSpace.UI
{
    [DisallowMultipleComponent]
    public sealed class DemoRoomPresenter : MonoBehaviour
    {
        [SerializeField]
        private GameObject _roomRoot;

        [SerializeField]
        private GameObject[] _decorationRoots =
            Array.Empty<GameObject>();

        private int _selectedDecorationIndex = -1;

        public event Action<int> DecorationPlacementRequested;

        public GameObject RoomRoot => _roomRoot;

        public int DecorationCount =>
            _decorationRoots?.Length ?? 0;

        public bool IsVisible =>
            _roomRoot != null &&
            _roomRoot.activeSelf;

        public int SelectedDecorationIndex =>
            _selectedDecorationIndex;

        public void SetVisible(bool visible)
        {
            if (_roomRoot != null &&
                _roomRoot.activeSelf != visible)
            {
                _roomRoot.SetActive(visible);
            }
        }

        public bool SelectDecoration(int decorationIndex)
        {
            if (_decorationRoots == null ||
                decorationIndex < 0 ||
                decorationIndex >= _decorationRoots.Length ||
                _decorationRoots[decorationIndex] == null)
            {
                return false;
            }

            for (var index = 0;
                 index < _decorationRoots.Length;
                 index++)
            {
                GameObject root = _decorationRoots[index];
                if (root != null &&
                    root.activeSelf !=
                    (index == decorationIndex))
                {
                    root.SetActive(index == decorationIndex);
                }
            }

            _selectedDecorationIndex = decorationIndex;
            return true;
        }

        /// <summary>
        /// Raises player intent. Inventory validation and encrypted persistence
        /// happen before SelectDecoration renders the resulting profile event.
        /// </summary>
        public void RequestDecorationPlacement(int decorationIndex)
        {
            DecorationPlacementRequested?.Invoke(decorationIndex);
        }
    }
}
