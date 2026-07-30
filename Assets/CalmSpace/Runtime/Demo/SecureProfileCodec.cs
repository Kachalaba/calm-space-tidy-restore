using System;
using System.Text;
using UnityEngine;

namespace CalmSpace.Demo
{
    public sealed class SecureProfileCodec
    {
        public const int VersionOne = 1;
        public const int VersionTwo = 2;

        public byte[] EncodeV2(DemoProgressSnapshot snapshot)
        {
            string[] owned = snapshot.CopyOwnedDecorationIds();
            DecorationSelection[] selections =
                snapshot.CopyDecorationSelections();
            PendingPresentationEntry[] pending =
                snapshot.CopyPendingPresentations();

            var selectionDtos =
                new SecureDecorationSelectionDto[selections.Length];
            for (var index = 0; index < selections.Length; index++)
            {
                selectionDtos[index] =
                    new SecureDecorationSelectionDto
                    {
                        slotId = selections[index].SlotId,
                        decorationId = selections[index].DecorationId
                    };
            }

            var pendingDtos =
                new SecurePendingPresentationDto[pending.Length];
            for (var index = 0; index < pending.Length; index++)
            {
                pendingDtos[index] =
                    new SecurePendingPresentationDto
                    {
                        kind = (int)pending[index].Kind,
                        stableId = pending[index].StableId,
                        requiresExplicitLaunch =
                            pending[index].RequiresExplicitLaunch
                    };
            }

            var dto = new SecureProfileV2Dto
            {
                version = VersionTwo,
                highestUnlockedLevelIndex =
                    snapshot.HighestUnlockedLevelIndex,
                completedLevelMask = snapshot.CompletedLevelMask,
                selectedThemeId = snapshot.SelectedThemeId,
                musicEnabled = snapshot.MusicEnabled,
                cozyTokens = snapshot.CozyTokens,
                rewardedLevelMask = snapshot.RewardedLevelMask,
                ownedDecorationMask = snapshot.OwnedDecorationMask,
                selectedDecorationIndex =
                    snapshot.SelectedDecorationIndex,
                ownedDecorationIds = owned,
                decorationSelections = selectionDtos,
                seenRoomRevealIds =
                    snapshot.CopySeenRoomRevealIds(),
                viewedMemoryIds =
                    snapshot.CopyViewedMemoryIds(),
                seenFinaleIds =
                    snapshot.CopySeenFinaleIds(),
                pendingPresentations = pendingDtos,
                lastDailyCareUtcDayKey =
                    snapshot.LastDailyCareUtcDayKey,
                completedDailyCareCount =
                    snapshot.CompletedDailyCareCount
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(dto));
        }

        public ProfileLoadStatus TryDecode(
            byte[] plaintext,
            out SecureProfileV1Dto versionOne,
            out DemoProgressSnapshot versionTwo)
        {
            versionOne = null;
            versionTwo = default;
            if (plaintext == null || plaintext.Length == 0)
            {
                return ProfileLoadStatus.InvalidData;
            }

            try
            {
                string json = Encoding.UTF8.GetString(plaintext);
                VersionProbe probe =
                    JsonUtility.FromJson<VersionProbe>(json);
                if (probe == null || probe.version <= 0)
                {
                    return ProfileLoadStatus.InvalidData;
                }

                if (probe.version > VersionTwo)
                {
                    return ProfileLoadStatus.UnsupportedVersion;
                }

                if (probe.version == VersionOne)
                {
                    SecureProfileV1Dto dto =
                        JsonUtility.FromJson<SecureProfileV1Dto>(json);
                    if (dto == null || dto.version != VersionOne)
                    {
                        return ProfileLoadStatus.InvalidData;
                    }

                    versionOne = dto;
                    return ProfileLoadStatus.LoadedV1;
                }

                SecureProfileV2Dto versionTwoDto =
                    JsonUtility.FromJson<SecureProfileV2Dto>(json);
                if (versionTwoDto == null ||
                    versionTwoDto.version != VersionTwo ||
                    !TryCreateSnapshot(
                        versionTwoDto,
                        out versionTwo))
                {
                    return ProfileLoadStatus.InvalidData;
                }

                return ProfileLoadStatus.LoadedV2;
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is FormatException ||
                      exception is DecoderFallbackException)
            {
                versionOne = null;
                versionTwo = default;
                return ProfileLoadStatus.InvalidData;
            }
        }

        private static bool TryCreateSnapshot(
            SecureProfileV2Dto dto,
            out DemoProgressSnapshot snapshot)
        {
            snapshot = default;
            string[] owned = dto.ownedDecorationIds ??
                Array.Empty<string>();
            SecureDecorationSelectionDto[] selectionDtos =
                dto.decorationSelections ??
                Array.Empty<SecureDecorationSelectionDto>();
            string[] seenRoom = dto.seenRoomRevealIds ??
                Array.Empty<string>();
            string[] viewedMemory = dto.viewedMemoryIds ??
                Array.Empty<string>();
            string[] seenFinale = dto.seenFinaleIds ??
                Array.Empty<string>();
            SecurePendingPresentationDto[] pendingDtos =
                dto.pendingPresentations ??
                Array.Empty<SecurePendingPresentationDto>();

            if (!HasRepairableIds(owned) ||
                !HasCanonicalUniqueIds(seenRoom) ||
                !HasCanonicalUniqueIds(viewedMemory) ||
                !HasCanonicalUniqueIds(seenFinale))
            {
                return false;
            }

            var selections =
                new DecorationSelection[selectionDtos.Length];
            for (var index = 0;
                 index < selectionDtos.Length;
                 index++)
            {
                SecureDecorationSelectionDto selection =
                    selectionDtos[index];
                if (selection == null ||
                    !IsRepairableStableId(selection.slotId) ||
                    !IsRepairableStableId(selection.decorationId))
                {
                    return false;
                }

                selections[index] =
                    new DecorationSelection(
                        selection.slotId,
                        selection.decorationId);
            }

            var pending =
                new PendingPresentationEntry[pendingDtos.Length];
            for (var index = 0;
                 index < pendingDtos.Length;
                 index++)
            {
                SecurePendingPresentationDto entry =
                    pendingDtos[index];
                if (entry == null ||
                    !IsCanonicalStableId(entry.stableId))
                {
                    return false;
                }

                switch ((PendingPresentationKind)entry.kind)
                {
                    case PendingPresentationKind.RoomReveal:
                        if (entry.requiresExplicitLaunch)
                        {
                            return false;
                        }

                        pending[index] =
                            PendingPresentationEntry.RoomReveal(
                                entry.stableId);
                        break;
                    case PendingPresentationKind.Memory:
                        if (entry.requiresExplicitLaunch)
                        {
                            return false;
                        }

                        pending[index] =
                            PendingPresentationEntry.Memory(
                                entry.stableId);
                        break;
                    case PendingPresentationKind.Finale:
                        pending[index] =
                            PendingPresentationEntry.Finale(
                                entry.stableId,
                                entry.requiresExplicitLaunch);
                        break;
                    default:
                        return false;
                }

                for (var prior = 0; prior < index; prior++)
                {
                    if (pending[prior].Kind == pending[index].Kind &&
                        string.Equals(
                            pending[prior].StableId,
                            pending[index].StableId,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            snapshot = new DemoProgressSnapshot(
                dto.highestUnlockedLevelIndex,
                dto.completedLevelMask,
                dto.selectedThemeId,
                dto.musicEnabled,
                dto.cozyTokens,
                dto.rewardedLevelMask,
                owned,
                selections,
                seenRoom,
                viewedMemory,
                seenFinale,
                pending,
                dto.lastDailyCareUtcDayKey,
                dto.completedDailyCareCount);
            return true;
        }

        private static bool HasRepairableIds(string[] values)
        {
            for (var index = 0; index < values.Length; index++)
            {
                if (!IsRepairableStableId(values[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasCanonicalUniqueIds(string[] values)
        {
            for (var index = 0; index < values.Length; index++)
            {
                if (!IsCanonicalStableId(values[index]))
                {
                    return false;
                }

                for (var prior = 0; prior < index; prior++)
                {
                    if (string.Equals(
                            values[prior],
                            values[index],
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsRepairableStableId(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool IsCanonicalStableId(string value)
        {
            return
                !string.IsNullOrWhiteSpace(value) &&
                string.Equals(
                    value,
                    value.Trim(),
                    StringComparison.Ordinal);
        }

        [Serializable]
        private sealed class VersionProbe
        {
            public int version;
        }
    }
}
