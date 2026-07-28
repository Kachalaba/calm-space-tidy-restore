using System;

namespace CalmSpace.Audio
{
    /// <summary>
    /// Controls the persistent background-music preference and playback state.
    /// Preference storage is intentionally owned by the presentation layer.
    /// </summary>
    public interface IBackgroundMusicService
    {
        bool IsEnabled { get; }

        bool IsPlaying { get; }

        event Action<bool> EnabledChanged;

        void Initialize(bool enabled);

        void SetEnabled(bool enabled);

        void Toggle();
    }
}
