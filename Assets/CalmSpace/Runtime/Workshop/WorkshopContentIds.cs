using System;

namespace CalmSpace.Workshop
{
    public readonly struct WorkshopBeatContract
    {
        internal WorkshopBeatContract(
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
            BeatId = beatId;
            StageIndex = stageIndex;
            TitleTextKey = titleTextKey;
            ResultTextKey = resultTextKey;
            ZoneIndex = zoneIndex;
            MemoryId = memoryId;
            UnlockedDecorSlotId = unlockedDecorSlotId;
            UnlocksDailyCare = unlocksDailyCare;
            IsFinale = isFinale;
        }

        public string BeatId { get; }

        public int StageIndex { get; }

        public string TitleTextKey { get; }

        public string ResultTextKey { get; }

        public int ZoneIndex { get; }

        public string MemoryId { get; }

        public string UnlockedDecorSlotId { get; }

        public bool UnlocksDailyCare { get; }

        public bool IsFinale { get; }
    }

    /// <summary>
    /// Stable identifiers shared by workshop authoring, persistence, and
    /// presentation. These values are data contracts and must not be localized.
    /// </summary>
    public static class WorkshopContentIds
    {
        public const string CozyWorkshopChapterId =
            "cozy-workshop";

        public const string ClearPassageBeatId =
            "cozy-workshop.clear-passage";
        public const string PebbleShelfBeatId =
            "cozy-workshop.pebble-shelf";
        public const string TeaDrawerBeatId =
            "cozy-workshop.tea-drawer";
        public const string PaintShelfBeatId =
            "cozy-workshop.paint-shelf";
        public const string FastenerTrayBeatId =
            "cozy-workshop.fastener-tray";
        public const string WarmWorkbenchBeatId =
            "cozy-workshop.warm-workbench";
        public const string CabinetHingeBeatId =
            "cozy-workshop.cabinet-hinge";
        public const string OpenWindowBeatId =
            "cozy-workshop.open-window";

        public const string SummerTrailStonesMemoryId =
            "family.summer-trail-stones";
        public const string FixEverythingMemoryId =
            "family.fix-everything";
        public const string OpenWindowsMemoryId =
            "family.open-windows";
        public const string TeaPostcardMemoryId =
            "family.tea-postcard-03";

        public const string WorkbenchAccentDecorSlotId =
            "workbench-accent";
        public const string WarmLightDecorSlotId =
            "warm-light";

        public const string FamilyTeaDailyCareId =
            "family-tea";

        private static readonly WorkshopBeatContract[]
            CozyWorkshopBeats =
            {
                new WorkshopBeatContract(
                    ClearPassageBeatId,
                    0,
                    "beat.clear-passage.title",
                    "beat.clear-passage.result",
                    0,
                    string.Empty,
                    string.Empty,
                    false,
                    false),
                new WorkshopBeatContract(
                    PebbleShelfBeatId,
                    1,
                    "beat.pebble-shelf.title",
                    "beat.pebble-shelf.result",
                    1,
                    SummerTrailStonesMemoryId,
                    string.Empty,
                    false,
                    false),
                new WorkshopBeatContract(
                    TeaDrawerBeatId,
                    2,
                    "beat.tea-drawer.title",
                    "beat.tea-drawer.result",
                    2,
                    string.Empty,
                    string.Empty,
                    true,
                    false),
                new WorkshopBeatContract(
                    PaintShelfBeatId,
                    3,
                    "beat.paint-shelf.title",
                    "beat.paint-shelf.result",
                    3,
                    string.Empty,
                    WorkbenchAccentDecorSlotId,
                    false,
                    false),
                new WorkshopBeatContract(
                    FastenerTrayBeatId,
                    4,
                    "beat.fastener-tray.title",
                    "beat.fastener-tray.result",
                    4,
                    FixEverythingMemoryId,
                    string.Empty,
                    false,
                    false),
                new WorkshopBeatContract(
                    WarmWorkbenchBeatId,
                    5,
                    "beat.warm-workbench.title",
                    "beat.warm-workbench.result",
                    5,
                    string.Empty,
                    WarmLightDecorSlotId,
                    false,
                    false),
                new WorkshopBeatContract(
                    CabinetHingeBeatId,
                    6,
                    "beat.cabinet-hinge.title",
                    "beat.cabinet-hinge.result",
                    6,
                    string.Empty,
                    string.Empty,
                    false,
                    false),
                new WorkshopBeatContract(
                    OpenWindowBeatId,
                    7,
                    "beat.open-window.title",
                    "beat.open-window.result",
                    7,
                    OpenWindowsMemoryId,
                    string.Empty,
                    false,
                    true)
            };

        public static int CozyWorkshopBeatCount =>
            CozyWorkshopBeats.Length;

        public static bool TryGetCozyWorkshopBeat(
            int stageIndex,
            out WorkshopBeatContract beat)
        {
            if (stageIndex < 0 ||
                stageIndex >= CozyWorkshopBeats.Length)
            {
                beat = default;
                return false;
            }

            beat = CozyWorkshopBeats[stageIndex];
            return true;
        }

        /// <summary>
        /// Resolves the authored text key for a family memory. Only memories
        /// this build can actually present have a key; anything else is left
        /// for a later chapter and must not be shown.
        /// </summary>
        public static bool TryGetMemoryTextKey(
            string memoryId,
            out string textKey)
        {
            switch (memoryId)
            {
                case SummerTrailStonesMemoryId:
                    textKey = "memory.summer-trail-stones";
                    return true;
                case FixEverythingMemoryId:
                    textKey = "memory.fix-everything";
                    return true;
                case OpenWindowsMemoryId:
                    textKey = "memory.open-windows";
                    return true;
                default:
                    textKey = string.Empty;
                    return false;
            }
        }

        internal static bool IsKnownMemoryId(string memoryId)
        {
            return
                string.Equals(
                    memoryId,
                    SummerTrailStonesMemoryId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    memoryId,
                    FixEverythingMemoryId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    memoryId,
                    OpenWindowsMemoryId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    memoryId,
                    TeaPostcardMemoryId,
                    StringComparison.Ordinal);
        }
    }
}
