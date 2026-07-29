using System;
using System.Collections.Generic;

namespace CalmSpace.Levels
{
    /// <summary>
    /// Tracks a fixed set of level item instance IDs and accepts each placement
    /// at most once.
    /// </summary>
    public sealed class LevelProgressTracker
    {
        private readonly int _expectedItemCount;
        private readonly HashSet<int> _registeredItems;
        private readonly HashSet<int> _placedItems;

        public event Action<int, bool, int, int> PlacementChanged;

        public event Action<bool> CompletionChanged;

        public LevelProgressTracker(int expectedItemCount)
        {
            if (expectedItemCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedItemCount));
            }

            _expectedItemCount = expectedItemCount;
            _registeredItems = new HashSet<int>();
            _placedItems = new HashSet<int>();
        }

        public int PlacedCount => _placedItems.Count;

        public bool IsComplete =>
            _registeredItems.Count == _expectedItemCount &&
            _placedItems.Count == _expectedItemCount;

        public bool RegisterItem(int itemInstanceId)
        {
            if (_registeredItems.Count >= _expectedItemCount)
            {
                return false;
            }

            return _registeredItems.Add(itemInstanceId);
        }

        public bool TryMarkPlaced(int itemInstanceId)
        {
            if (!_registeredItems.Contains(itemInstanceId))
            {
                return false;
            }

            bool wasComplete = IsComplete;
            if (!_placedItems.Add(itemInstanceId))
            {
                return false;
            }

            PlacementChanged?.Invoke(
                itemInstanceId,
                true,
                _placedItems.Count,
                _expectedItemCount);
            bool isComplete = IsComplete;
            if (wasComplete != isComplete)
            {
                CompletionChanged?.Invoke(isComplete);
            }

            return true;
        }

        public bool TryUnmarkPlaced(int itemInstanceId)
        {
            bool wasComplete = IsComplete;
            if (!_placedItems.Remove(itemInstanceId))
            {
                return false;
            }

            PlacementChanged?.Invoke(
                itemInstanceId,
                false,
                _placedItems.Count,
                _expectedItemCount);
            bool isComplete = IsComplete;
            if (wasComplete != isComplete)
            {
                CompletionChanged?.Invoke(isComplete);
            }

            return true;
        }
    }
}
