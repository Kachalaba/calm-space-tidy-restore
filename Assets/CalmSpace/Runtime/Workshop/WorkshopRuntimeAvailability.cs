using System;
using System.Collections.Generic;
using CalmSpace.Levels;
using UnityEngine;

namespace CalmSpace.Workshop
{
    /// <summary>
    /// Separates optional workshop authoring health from core catalog play.
    /// Editor validators remain strict; runtime consumers use this immutable
    /// policy to hide unavailable meta surfaces without blocking levels.
    /// </summary>
    public sealed class WorkshopRuntimeAvailability
    {
        private static readonly string[] RequiredHomeTextKeys =
        {
            "home.start",
            "home.catalog",
            "home.settings",
            "home.chapter-complete",
            "load.failure.title",
            "load.failure.retry"
        };

        public WorkshopRuntimeAvailability(
            LevelCatalog levels,
            LivingWorkshopCatalog workshop,
            WorkshopTextCatalog text)
        {
            WorkshopCatalogValidationResult validation =
                WorkshopCatalogValidator.ValidateChapter(
                    levels,
                    workshop,
                    WorkshopContentIds.CozyWorkshopChapterId);
            MetaAvailable = validation.IsValid;
            TextAvailable = ValidateText(workshop, text);
            HomeMetaAvailable = MetaAvailable && TextAvailable;

            if (!HomeMetaAvailable)
            {
                Debug.LogWarning(
                    "Calm Space workshop authoring is unavailable at " +
                    "runtime; Catalog and Settings remain available. " +
                    "meta=" + validation.Code +
                    ", text=" + (TextAvailable ? "valid" : "invalid") +
                    ".");
            }
        }

        public bool MetaAvailable { get; }

        public bool TextAvailable { get; }

        public bool HomeMetaAvailable { get; }

        private static bool ValidateText(
            LivingWorkshopCatalog workshop,
            WorkshopTextCatalog text)
        {
            if (text?.Entries == null || text.Entries.Count == 0)
            {
                return false;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < text.Entries.Count; index++)
            {
                WorkshopTextEntry entry = text.Entries[index];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.Key) ||
                    string.IsNullOrWhiteSpace(entry.English) ||
                    string.IsNullOrWhiteSpace(entry.Ukrainian) ||
                    string.IsNullOrWhiteSpace(entry.Russian) ||
                    !keys.Add(entry.Key))
                {
                    return false;
                }
            }

            for (var index = 0;
                 index < RequiredHomeTextKeys.Length;
                 index++)
            {
                if (!keys.Contains(RequiredHomeTextKeys[index]))
                {
                    return false;
                }
            }

            if (workshop == null)
            {
                return false;
            }

            for (var index = 0; index < workshop.Count; index++)
            {
                if (!workshop.TryGetBeat(index, out WorkshopBeatDefinition beat) ||
                    !keys.Contains(beat.TitleTextKey) ||
                    !keys.Contains(beat.ResultTextKey))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
