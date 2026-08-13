using System;
using System.Collections.Generic;
using CalmSpace.Demo;
using UnityEngine;

namespace CalmSpace.Workshop
{
    [Serializable]
    public sealed class WorkshopTextEntry
    {
        [SerializeField] private string _key;
        [SerializeField] private string _english;
        [SerializeField] private string _ukrainian;
        [SerializeField] private string _russian;

        public WorkshopTextEntry(
            string key,
            string english,
            string ukrainian,
            string russian)
        {
            _key = key;
            _english = english;
            _ukrainian = ukrainian;
            _russian = russian;
        }

        public string Key => _key;
        public string English => _english;
        public string Ukrainian => _ukrainian;
        public string Russian => _russian;

        public string Get(DemoLocale locale)
        {
            switch (locale)
            {
                case DemoLocale.Ukrainian:
                    return _ukrainian;
                case DemoLocale.Russian:
                    return _russian;
                default:
                    return _english;
            }
        }
    }

    [CreateAssetMenu(
        fileName = "WorkshopTextCatalog",
        menuName = "Calm Space/Workshop Text Catalog")]
    public sealed class WorkshopTextCatalog : ScriptableObject
    {
        [SerializeField] private WorkshopTextEntry[] _entries =
            Array.Empty<WorkshopTextEntry>();

        public IReadOnlyList<WorkshopTextEntry> Entries => _entries;

        public string Get(string key, DemoLocale locale)
        {
            if (TryGet(key, out WorkshopTextEntry entry))
            {
                string requested = entry.Get(locale);
                if (!string.IsNullOrWhiteSpace(requested))
                {
                    return requested;
                }

                if (!string.IsNullOrWhiteSpace(entry.English))
                {
                    return entry.English;
                }
            }

            return key ?? string.Empty;
        }

        public bool TryGet(string key, out WorkshopTextEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(key) && _entries != null)
            {
                for (var index = 0; index < _entries.Length; index++)
                {
                    WorkshopTextEntry candidate = _entries[index];
                    if (candidate != null &&
                        string.Equals(
                            candidate.Key,
                            key,
                            StringComparison.Ordinal))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }
    }
}
