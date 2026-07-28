using System;

namespace CalmSpace.Demo
{
    public interface IDemoThemeService
    {
        event Action<ThemePalette> ThemeChanged;

        bool IsInitialized { get; }

        int ThemeCount { get; }

        int CurrentIndex { get; }

        string DefaultThemeId { get; }

        ThemePalette Current { get; }

        void Initialize();

        bool TryGetTheme(int index, out ThemePalette palette);

        bool SelectTheme(int index);

        bool SelectTheme(string themeId);
    }

    /// <summary>
    /// Owns theme selection while persistence remains delegated to the demo
    /// profile store. Selection occurs only from UI actions, never per frame.
    /// </summary>
    public sealed class DemoThemeService : IDemoThemeService
    {
        private readonly DemoThemeCatalog _catalog;
        private readonly IDemoProgressStore _progressStore;

        public DemoThemeService(
            DemoThemeCatalog catalog,
            IDemoProgressStore progressStore)
        {
            _catalog = catalog ??
                throw new ArgumentNullException(nameof(catalog));
            _progressStore = progressStore ??
                throw new ArgumentNullException(
                    nameof(progressStore));
        }

        public event Action<ThemePalette> ThemeChanged;

        public bool IsInitialized { get; private set; }

        public int ThemeCount => _catalog.Count;

        public int CurrentIndex { get; private set; } = -1;

        public string DefaultThemeId =>
            _catalog.DefaultPalette?.Id ?? string.Empty;

        public ThemePalette Current { get; private set; }

        public void Initialize()
        {
            if (!_progressStore.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Initialize demo progress before the theme service.");
            }

            var selectedId =
                _progressStore.Current.SelectedThemeId;
            if (!_catalog.TryFindPalette(
                    selectedId,
                    out var selectedIndex,
                    out var selected))
            {
                if (!TryResolveFallback(
                        out selectedIndex,
                        out selected))
                {
                    throw new InvalidOperationException(
                        "DemoThemeCatalog requires at least one valid palette.");
                }

                _progressStore.SetSelectedTheme(selected.Id);
            }

            CurrentIndex = selectedIndex;
            Current = selected;
            IsInitialized = true;
        }

        public bool TryGetTheme(
            int index,
            out ThemePalette palette)
        {
            return _catalog.TryGetPalette(index, out palette);
        }

        public bool SelectTheme(int index)
        {
            return _catalog.TryGetPalette(index, out var palette) &&
                SelectThemeCore(index, palette);
        }

        public bool SelectTheme(string themeId)
        {
            return _catalog.TryFindPalette(
                    themeId,
                    out var index,
                    out var palette) &&
                SelectThemeCore(index, palette);
        }

        private bool SelectThemeCore(
            int index,
            ThemePalette palette)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "Initialize the demo theme service before use.");
            }

            if (palette == null || !palette.IsValid)
            {
                return false;
            }

            if (CurrentIndex == index &&
                ReferenceEquals(Current, palette))
            {
                return true;
            }

            CurrentIndex = index;
            Current = palette;
            _progressStore.SetSelectedTheme(palette.Id);
            ThemeChanged?.Invoke(palette);
            return true;
        }

        private bool TryResolveFallback(
            out int index,
            out ThemePalette palette)
        {
            var configuredDefault = _catalog.DefaultPalette;
            if (configuredDefault != null &&
                _catalog.TryFindPalette(
                    configuredDefault.Id,
                    out index,
                    out palette))
            {
                return true;
            }

            for (var candidateIndex = 0;
                 candidateIndex < _catalog.Count;
                 candidateIndex++)
            {
                if (_catalog.TryGetPalette(
                        candidateIndex,
                        out palette))
                {
                    index = candidateIndex;
                    return true;
                }
            }

            index = -1;
            palette = null;
            return false;
        }
    }
}
