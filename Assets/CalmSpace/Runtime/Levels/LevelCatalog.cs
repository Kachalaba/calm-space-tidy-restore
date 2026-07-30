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

        [NonSerialized]
        private LevelCatalogEntry[] _restorationCachedLevels;

        [NonSerialized]
        private RestorationChapterCacheEntry[]
            _restorationChapterCache =
                Array.Empty<RestorationChapterCacheEntry>();

        public int Count => _levels?.Length ?? 0;

        /// <summary>
        /// Compatibility and editor diagnostic for callers that require every
        /// authored chapter to be valid. Runtime presentation uses the
        /// chapter-scoped APIs below.
        /// </summary>
        public bool RestorationMetadataValid
        {
            get
            {
                EnsureRestorationMetadataValidity();
                return _restorationMetadataValid;
            }
        }

        public bool IsRestorationChapterValid(string chapterId)
        {
            return TryGetRestorationChapter(
                chapterId,
                out _);
        }

        public bool TryGetRestorationChapter(
            string chapterId,
            out RestorationChapterInfo chapter)
        {
            chapter = default;
            if (string.IsNullOrWhiteSpace(chapterId))
            {
                return false;
            }

            EnsureRestorationMetadataValidity();
            string normalizedChapterId = chapterId.Trim();
            for (var index = 0;
                 index < _restorationChapterCache.Length;
                 index++)
            {
                RestorationChapterCacheEntry candidate =
                    _restorationChapterCache[index];
                if (!string.Equals(
                        candidate.ChapterId,
                        normalizedChapterId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!candidate.IsValid)
                {
                    return false;
                }

                chapter = candidate.Chapter;
                return true;
            }

            return false;
        }

        public bool TryFindRestorationStage(
            string chapterId,
            int stageIndex,
            out int levelIndex,
            out LevelCatalogEntry entry)
        {
            levelIndex = -1;
            entry = null;
            if (!TryGetRestorationChapter(
                    chapterId,
                    out var chapter) ||
                stageIndex < 0 ||
                stageIndex >= chapter.StageCount)
            {
                return false;
            }

            int candidateIndex =
                chapter.FirstCatalogIndex + stageIndex;
            if (!TryGetEntry(candidateIndex, out var candidate) ||
                !candidate.Definition.TryGetRestorationStage(
                    out var stage) ||
                !string.Equals(
                    stage.ChapterId,
                    chapter.ChapterId,
                    StringComparison.Ordinal) ||
                stage.StageIndex != stageIndex ||
                stage.StageCount != chapter.StageCount)
            {
                return false;
            }

            levelIndex = candidateIndex;
            entry = candidate;
            return true;
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
            if (!TryGetEntry(levelIndex, out var entry) ||
                !entry.Definition.TryGetRestorationStage(
                    out stage) ||
                !IsRestorationChapterValid(stage.ChapterId))
            {
                stage = default;
                return false;
            }

            return true;
        }

        public bool IsRestorationContinuation(
            int currentLevelIndex,
            int nextLevelIndex)
        {
            if (!TryGetEntry(
                    currentLevelIndex,
                    out var current) ||
                !TryGetEntry(nextLevelIndex, out var next) ||
                !current.Definition.TryGetRestorationStage(
                    out var currentStage) ||
                !IsRestorationChapterValid(
                    currentStage.ChapterId))
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
            if (!_restorationMetadataInitialized ||
                !ReferenceEquals(
                    _restorationCachedLevels,
                    _levels))
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

            var chapterCache =
                new RestorationChapterCacheEntry[levelCount];
            int chapterCount = 0;
            for (var definitionIndex = 0;
                 definitionIndex < definitions.Length;
                 definitionIndex++)
            {
                string authoredChapterId =
                    definitions[definitionIndex]
                        ?.RestorationChapterId;
                if (string.IsNullOrWhiteSpace(
                        authoredChapterId))
                {
                    continue;
                }

                string chapterId = authoredChapterId.Trim();
                bool alreadyCached = false;
                for (var cacheIndex = 0;
                     cacheIndex < chapterCount;
                     cacheIndex++)
                {
                    if (string.Equals(
                            chapterCache[cacheIndex].ChapterId,
                            chapterId,
                            StringComparison.Ordinal))
                    {
                        alreadyCached = true;
                        break;
                    }
                }

                if (alreadyCached)
                {
                    continue;
                }

                bool valid =
                    RestorationProgressRules.TryValidateChapter(
                        definitions,
                        chapterId,
                        out var chapter,
                        out _);
                chapterCache[chapterCount] =
                    new RestorationChapterCacheEntry(
                        chapterId,
                        valid,
                        chapter);
                chapterCount++;
            }

            if (chapterCount == 0)
            {
                _restorationChapterCache =
                    Array.Empty<RestorationChapterCacheEntry>();
            }
            else
            {
                if (chapterCount < chapterCache.Length)
                {
                    Array.Resize(
                        ref chapterCache,
                        chapterCount);
                }

                _restorationChapterCache = chapterCache;
            }

            _restorationMetadataValid =
                RestorationProgressRules.IsCatalogSequenceValid(
                    definitions,
                    out _);
            _restorationCachedLevels = _levels;
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

        private readonly struct RestorationChapterCacheEntry
        {
            public RestorationChapterCacheEntry(
                string chapterId,
                bool isValid,
                RestorationChapterInfo chapter)
            {
                ChapterId = chapterId;
                IsValid = isValid;
                Chapter = chapter;
            }

            public string ChapterId { get; }

            public bool IsValid { get; }

            public RestorationChapterInfo Chapter { get; }
        }
    }
}
