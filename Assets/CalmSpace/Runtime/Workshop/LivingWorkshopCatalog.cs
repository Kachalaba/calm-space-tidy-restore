using System;
using UnityEngine;

namespace CalmSpace.Workshop
{
    [Serializable]
    public sealed class WorkshopBeatDefinition
    {
        [SerializeField]
        private string _chapterId = string.Empty;

        [SerializeField]
        private string _beatId = string.Empty;

        [SerializeField]
        private int _stageIndex;

        [SerializeField]
        private string _titleTextKey = string.Empty;

        [SerializeField]
        private string _resultTextKey = string.Empty;

        [SerializeField]
        private int _zoneIndex;

        [SerializeField]
        private string _memoryId = string.Empty;

        [SerializeField]
        private string _unlockedDecorSlotId = string.Empty;

        [SerializeField]
        private bool _unlocksDailyCare;

        [SerializeField]
        private bool _isFinale;

        public WorkshopBeatDefinition(
            string chapterId,
            string beatId,
            int stageIndex,
            string titleTextKey,
            string resultTextKey,
            int zoneIndex,
            string memoryId,
            string unlockedDecorSlotId,
            bool unlocksDailyCare,
            bool isFinale)
        {
            _chapterId = Normalize(chapterId);
            _beatId = Normalize(beatId);
            _stageIndex = stageIndex;
            _titleTextKey = Normalize(titleTextKey);
            _resultTextKey = Normalize(resultTextKey);
            _zoneIndex = zoneIndex;
            _memoryId = Normalize(memoryId);
            _unlockedDecorSlotId =
                Normalize(unlockedDecorSlotId);
            _unlocksDailyCare = unlocksDailyCare;
            _isFinale = isFinale;
        }

        public string ChapterId => _chapterId;

        public string BeatId => _beatId;

        public int StageIndex => _stageIndex;

        public string TitleTextKey => _titleTextKey;

        public string ResultTextKey => _resultTextKey;

        public int ZoneIndex => _zoneIndex;

        public string MemoryId => _memoryId;

        public string UnlockedDecorSlotId =>
            _unlockedDecorSlotId;

        public bool UnlocksDailyCare => _unlocksDailyCare;

        public bool IsFinale => _isFinale;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_chapterId) &&
            !string.IsNullOrWhiteSpace(_beatId) &&
            _stageIndex >= 0 &&
            !string.IsNullOrWhiteSpace(_titleTextKey) &&
            !string.IsNullOrWhiteSpace(_resultTextKey) &&
            _zoneIndex >= 0;

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }

    [CreateAssetMenu(
        fileName = "LivingWorkshopCatalog",
        menuName = "Calm Space/Living Workshop Catalog")]
    public sealed class LivingWorkshopCatalog : ScriptableObject
    {
        [SerializeField]
        private WorkshopBeatDefinition[] _beats =
            Array.Empty<WorkshopBeatDefinition>();

        public int Count => _beats?.Length ?? 0;

        public bool TryGetBeat(
            int index,
            out WorkshopBeatDefinition beat)
        {
            if (_beats == null ||
                index < 0 ||
                index >= _beats.Length)
            {
                beat = null;
                return false;
            }

            beat = _beats[index];
            return beat != null;
        }

        public bool TryFindBeat(
            string chapterId,
            int stageIndex,
            out WorkshopBeatDefinition beat)
        {
            beat = null;
            if (_beats == null ||
                string.IsNullOrWhiteSpace(chapterId) ||
                stageIndex < 0)
            {
                return false;
            }

            string normalizedChapterId = chapterId.Trim();
            for (var index = 0;
                 index < _beats.Length;
                 index++)
            {
                WorkshopBeatDefinition candidate = _beats[index];
                if (candidate == null ||
                    !string.Equals(
                        candidate.ChapterId,
                        normalizedChapterId,
                        StringComparison.Ordinal) ||
                    candidate.StageIndex != stageIndex)
                {
                    continue;
                }

                beat = candidate;
                return true;
            }

            return false;
        }

        public bool TryFindBeat(
            string beatId,
            out WorkshopBeatDefinition beat)
        {
            beat = null;
            if (_beats == null ||
                string.IsNullOrWhiteSpace(beatId))
            {
                return false;
            }

            string normalizedBeatId = beatId.Trim();
            for (var index = 0;
                 index < _beats.Length;
                 index++)
            {
                WorkshopBeatDefinition candidate = _beats[index];
                if (candidate == null ||
                    !string.Equals(
                        candidate.BeatId,
                        normalizedBeatId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                beat = candidate;
                return true;
            }

            return false;
        }
    }
}
