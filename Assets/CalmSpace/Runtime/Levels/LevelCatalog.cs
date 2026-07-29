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

        [NonSerialized]
        private bool _restorationMetadataInitialized;

        [NonSerialized]
        private bool _restorationMetadataValid;

        public int Count => _levels?.Length ?? 0;

        /// <summary>
        /// Catalog-wide authority for restoration presentation and analytics.
        /// Invalid metadata fails closed at runtime, leaving every level
        /// playable as an ordinary standalone space.
        /// </summary>
        public bool RestorationMetadataValid
        {
            get
            {
                EnsureRestorationMetadataValidity();
                return _restorationMetadataValid;
            }
        }

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

        public bool TryGetRestorationStage(
            int levelIndex,
            out RestorationStageInfo stage)
        {
            if (!RestorationMetadataValid ||
                !TryGetEntry(levelIndex, out var entry))
            {
                stage = default;
                return false;
            }

            return entry.Definition.TryGetRestorationStage(
                out stage);
        }

        public bool IsRestorationContinuation(
            int currentLevelIndex,
            int nextLevelIndex)
        {
            if (!RestorationMetadataValid ||
                !TryGetEntry(
                    currentLevelIndex,
                    out var current) ||
                !TryGetEntry(nextLevelIndex, out var next))
            {
                return false;
            }

            return RestorationProgressRules.IsContinuation(
                current.Definition,
                next.Definition);
        }

        private void OnEnable()
        {
            RefreshRestorationMetadataValidity();
        }

        private void EnsureRestorationMetadataValidity()
        {
            if (!_restorationMetadataInitialized)
            {
                RefreshRestorationMetadataValidity();
            }
        }

        private void RefreshRestorationMetadataValidity()
        {
            int levelCount = _levels?.Length ?? 0;
            var definitions =
                new LevelDefinition[levelCount];
            for (var index = 0;
                 index < levelCount;
                 index++)
            {
                definitions[index] =
                    _levels[index]?.Definition;
            }

            _restorationMetadataValid =
                RestorationProgressRules.IsCatalogSequenceValid(
                    definitions,
                    out _);
            _restorationMetadataInitialized = true;
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

            RefreshRestorationMetadataValidity();
            if (!_restorationMetadataValid)
            {
                var definitions =
                    new LevelDefinition[_levels.Length];
                for (var index = 0;
                     index < _levels.Length;
                     index++)
                {
                    definitions[index] =
                        _levels[index]?.Definition;
                }

                RestorationProgressRules.IsCatalogSequenceValid(
                    definitions,
                    out var invalidIndex);
                Debug.LogError(
                    "Restoration chapter metadata must form contiguous, " +
                    "complete catalog sequences. Invalid level index: " +
                    invalidIndex +
                    ".",
                    this);
            }
        }
#endif
    }
}
