using System;
using UnityEngine;

namespace CalmSpace.Demo
{
    [Serializable]
    public sealed class DemoDecorationDefinition
    {
        [SerializeField]
        private string _id = string.Empty;

        [SerializeField]
        private string _displayName = string.Empty;

        [SerializeField]
        [Min(0)]
        private int _cost;

        public DemoDecorationDefinition()
        {
        }

        public DemoDecorationDefinition(
            string id,
            string displayName,
            int cost)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "A decoration id is required.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A decoration display name is required.",
                    nameof(displayName));
            }

            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cost),
                    cost,
                    "A decoration cost cannot be negative.");
            }

            _id = id.Trim();
            _displayName = displayName.Trim();
            _cost = cost;
        }

        public string Id => _id;

        public string DisplayName => _displayName;

        public int Cost => Mathf.Max(0, _cost);

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_id) &&
            !string.IsNullOrWhiteSpace(_displayName) &&
            _cost >= 0;
    }

    [CreateAssetMenu(
        fileName = "DemoDecorationCatalog",
        menuName = "Calm Space/Demo Decoration Catalog")]
    public sealed class DemoDecorationCatalog : ScriptableObject
    {
        public const int DefaultCompletionReward = 15;

        [SerializeField]
        private DemoDecorationDefinition[] _decorations =
            CreateBuiltInDefinitions();

        [SerializeField]
        [Min(1)]
        private int _completionReward =
            DefaultCompletionReward;

        public int Count => _decorations?.Length ?? 0;

        public int CompletionReward =>
            Mathf.Max(1, _completionReward);

        public bool TryGetDefinition(
            int index,
            out DemoDecorationDefinition definition)
        {
            if (_decorations == null ||
                index < 0 ||
                index >= _decorations.Length)
            {
                definition = null;
                return false;
            }

            definition = _decorations[index];
            return definition != null && definition.IsValid;
        }

        public bool TryGetDecoration(
            int index,
            out DemoDecorationDefinition definition)
        {
            return TryGetDefinition(index, out definition);
        }

        public static DemoDecorationDefinition[]
            CreateBuiltInDefinitions()
        {
            return new[]
            {
                new DemoDecorationDefinition(
                    "soft-fern",
                    "Soft Fern",
                    0),
                new DemoDecorationDefinition(
                    "river-stones",
                    "River Stones",
                    20),
                new DemoDecorationDefinition(
                    "warm-lantern",
                    "Warm Lantern",
                    40),
                new DemoDecorationDefinition(
                    "clay-vase",
                    "Clay Vase",
                    60)
            };
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _decorations = CreateBuiltInDefinitions();
            _completionReward = DefaultCompletionReward;
        }

        private void OnValidate()
        {
            if (_decorations == null || _decorations.Length == 0)
            {
                _decorations = CreateBuiltInDefinitions();
            }

            _completionReward = Mathf.Max(
                1,
                _completionReward);
        }
#endif
    }
}
