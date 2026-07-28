using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CalmSpace.Levels
{
    [Serializable]
    public sealed class LevelCatalogEntry
    {
        [SerializeField]
        private LevelDefinition _definition;

        [SerializeField]
        private AssetReferenceGameObject _prefab =
            new AssetReferenceGameObject(string.Empty);

        public LevelDefinition Definition => _definition;

        public AssetReferenceGameObject Prefab => _prefab;

        public bool IsValid =>
            _definition != null &&
            !string.IsNullOrWhiteSpace(_definition.LevelId) &&
            _prefab != null &&
            _prefab.RuntimeKeyIsValid();
    }

    /// <summary>
    /// Ordered, ScriptableObject-authored list of Addressable level prefabs.
    /// The order is the player progression order and does not require scenes.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LevelCatalog",
        menuName = "Calm Space/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField]
        private LevelCatalogEntry[] _levels =
            Array.Empty<LevelCatalogEntry>();

        public int Count => _levels?.Length ?? 0;

        public bool TryGetEntry(
            int index,
            out LevelCatalogEntry entry)
        {
            if (_levels == null ||
                index < 0 ||
                index >= _levels.Length)
            {
                entry = null;
                return false;
            }

            entry = _levels[index];
            return entry != null && entry.IsValid;
        }

        public bool TryFindEntry(
            string levelId,
            out int index,
            out LevelCatalogEntry entry)
        {
            index = -1;
            entry = null;

            if (string.IsNullOrWhiteSpace(levelId) ||
                _levels == null)
            {
                return false;
            }

            for (var candidateIndex = 0;
                 candidateIndex < _levels.Length;
                 candidateIndex++)
            {
                var candidate = _levels[candidateIndex];
                if (candidate == null ||
                    candidate.Definition == null ||
                    !string.Equals(
                        candidate.Definition.LevelId,
                        levelId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!candidate.IsValid)
                {
                    return false;
                }

                index = candidateIndex;
                entry = candidate;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_levels == null)
            {
                _levels = Array.Empty<LevelCatalogEntry>();
            }

            for (var first = 0; first < _levels.Length; first++)
            {
                var firstDefinition = _levels[first]?.Definition;
                if (firstDefinition == null ||
                    string.IsNullOrWhiteSpace(firstDefinition.LevelId))
                {
                    continue;
                }

                for (var second = first + 1;
                     second < _levels.Length;
                     second++)
                {
                    var secondDefinition =
                        _levels[second]?.Definition;
                    if (secondDefinition != null &&
                        string.Equals(
                            firstDefinition.LevelId,
                            secondDefinition.LevelId,
                            StringComparison.Ordinal))
                    {
                        Debug.LogError(
                            $"Duplicate level id '{firstDefinition.LevelId}' " +
                            $"in catalog '{name}'.",
                            this);
                    }
                }
            }
        }
#endif
    }
}
