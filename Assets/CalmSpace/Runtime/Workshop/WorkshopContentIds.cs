using System;

namespace CalmSpace.Workshop
{
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
