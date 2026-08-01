using System;
using System.Collections.Generic;
using CalmSpace.Demo;
using UnityEngine;

namespace CalmSpace.Workshop
{
    public interface IWorkshopTextService
    {
        string Get(string key);
    }

    /// <summary>
    /// Resolves stable workshop text keys using the shared locale selection.
    /// </summary>
    public sealed class WorkshopTextService : IWorkshopTextService, IDisposable
    {
        private readonly WorkshopTextCatalog _catalog;
        private readonly IDemoLocalizationService _localization;
        private readonly HashSet<string> _loggedBrokenKeys =
            new HashSet<string>(StringComparer.Ordinal);

        public WorkshopTextService(
            WorkshopTextCatalog catalog,
            IDemoLocalizationService localization)
        {
            _catalog = catalog ?? throw new ArgumentNullException(
                nameof(catalog));
            _localization = localization ?? throw new ArgumentNullException(
                nameof(localization));
            _localization.LocaleChanged += OnLocaleChanged;
        }

        public event Action LocaleChanged;

        public string Get(string key)
        {
            if (!_catalog.TryGet(key, out WorkshopTextEntry entry))
            {
                LogBrokenKeyOnce(key, "is missing from the catalog");
                return key ?? string.Empty;
            }

            string requested = entry.Get(_localization.CurrentLocale);
            if (!string.IsNullOrWhiteSpace(requested))
            {
                return requested;
            }

            LogBrokenKeyOnce(key, "has a blank requested translation");
            if (!string.IsNullOrWhiteSpace(entry.English))
            {
                return entry.English;
            }

            LogBrokenKeyOnce(key, "has a blank English fallback");
            return key ?? string.Empty;
        }

        public void Dispose()
        {
            _localization.LocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(DemoLocale locale)
        {
            LocaleChanged?.Invoke();
        }

        private void LogBrokenKeyOnce(string key, string reason)
        {
            string stableKey = key ?? string.Empty;
            if (_loggedBrokenKeys.Add(stableKey))
            {
                Debug.LogWarning(
                    "Calm Space workshop text key '" + stableKey +
                    "' " + reason + ".");
            }
        }
    }
}
