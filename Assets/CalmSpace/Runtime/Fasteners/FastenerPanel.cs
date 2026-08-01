using System;
using System.Collections.Generic;
using CalmSpace.Input;
using UnityEngine;

namespace CalmSpace.Fasteners
{
    /// <summary>
    /// A panel held down by screws. While it is still fastened its colliders
    /// are off, so the panel does not even absorb the raycast — taps travel
    /// through to the screws. Once the last screw is out the panel becomes an
    /// ordinary draggable item, and putting it away uncovers the layer below.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FastenerPanel : MonoBehaviour
    {
        [SerializeField]
        private ScrewController[] _screws =
            Array.Empty<ScrewController>();

        [SerializeField]
        private Collider[] _lockedColliders =
            Array.Empty<Collider>();

        [SerializeField]
        private ItemSnapController _item;

        [SerializeField]
        private GameObject _revealsOnRelease;

        private readonly HashSet<int> _removedScrews =
            new HashSet<int>();

        private int _requiredScrewCount;
        private bool _initialized;
        private bool _isReleased;
        private bool _hasRevealed;

        public event Action<FastenerPanel> Released;

        public int ScrewCount => _requiredScrewCount;

        public int RemovedScrewCount => _removedScrews.Count;

        public bool IsReleased => _isReleased;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (_screws != null)
            {
                for (var index = 0; index < _screws.Length; index++)
                {
                    ScrewController screw = _screws[index];
                    if (screw != null)
                    {
                        screw.Removed -= HandleScrewRemoved;
                        screw.Restored -= HandleScrewRestored;
                    }
                }
            }

            if (_item != null)
            {
                _item.Placed -= HandleItemPlaced;
                _item.Unplaced -= HandleItemUnplaced;
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            SetFastened(true);

            if (_item != null)
            {
                _item.Placed += HandleItemPlaced;
                _item.Unplaced += HandleItemUnplaced;
            }

            if (_screws != null)
            {
                for (var index = 0; index < _screws.Length; index++)
                {
                    ScrewController screw = _screws[index];
                    if (screw == null)
                    {
                        continue;
                    }

                    _requiredScrewCount++;
                    screw.Removed += HandleScrewRemoved;
                    screw.Restored += HandleScrewRestored;
                    if (screw.IsRemoved)
                    {
                        _removedScrews.Add(screw.GetInstanceID());
                    }
                }
            }

            TryRelease();
        }

        private void HandleScrewRemoved(ScrewController screw)
        {
            if (screw == null ||
                !_removedScrews.Add(screw.GetInstanceID()))
            {
                return;
            }

            TryRelease();
        }

        private void HandleItemPlaced(ItemSnapController item)
        {
            Reveal();
        }

        private void HandleItemUnplaced(ItemSnapController item)
        {
            HideReveal();
        }

        private void HandleScrewRestored(ScrewController screw)
        {
            if (screw == null ||
                !_removedScrews.Remove(screw.GetInstanceID()))
            {
                return;
            }

            if (_removedScrews.Count < _requiredScrewCount)
            {
                _isReleased = false;
                SetFastened(true);
                if (_item == null || !_item.IsPlaced)
                {
                    HideReveal();
                }
            }
        }

        private void TryRelease()
        {
            if (_isReleased ||
                _removedScrews.Count < _requiredScrewCount)
            {
                return;
            }

            _isReleased = true;
            SetFastened(false);

            // A panel with no item to carry away has nothing left to do, so
            // it uncovers the next layer as soon as it comes loose.
            if (_item == null)
            {
                Reveal();
            }

            InvokeReleased();
        }

        private void SetFastened(bool fastened)
        {
            if (_lockedColliders != null)
            {
                for (var index = 0;
                     index < _lockedColliders.Length;
                     index++)
                {
                    Collider collider = _lockedColliders[index];
                    if (collider != null)
                    {
                        collider.enabled = !fastened;
                    }
                }
            }

            if (_item != null)
            {
                _item.enabled = !fastened;
            }
        }

        private void Reveal()
        {
            if (_hasRevealed)
            {
                return;
            }

            _hasRevealed = true;
            if (_revealsOnRelease != null)
            {
                _revealsOnRelease.SetActive(true);
            }
        }

        private void HideReveal()
        {
            if (!_hasRevealed)
            {
                return;
            }

            _hasRevealed = false;
            if (_revealsOnRelease != null)
            {
                _revealsOnRelease.SetActive(false);
            }
        }

        private void InvokeReleased()
        {
            var handler = Released;
            if (handler == null)
            {
                return;
            }

            foreach (Action<FastenerPanel> subscriber in
                     handler.GetInvocationList())
            {
                try
                {
                    subscriber(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
