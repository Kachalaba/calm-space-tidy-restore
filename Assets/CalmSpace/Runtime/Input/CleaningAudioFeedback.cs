using System;
using CalmSpace.Audio;
using CalmSpace.Cleaning;
using CalmSpace.Core;
using UnityEngine;

namespace CalmSpace.Input
{
    public sealed class CleaningAudioFeedback
    {
        public const double DefaultMinimumIntervalSeconds = 0.10d;

        private readonly IAsmrAudioService _audioService;
        private readonly IMonotonicClock _clock;
        private readonly double _minimumIntervalSeconds;
        private double _lastPlayedAtSeconds;
        private bool _hasPlayed;

        public CleaningAudioFeedback(
            IAsmrAudioService audioService,
            IMonotonicClock clock,
            double minimumIntervalSeconds)
        {
            _audioService = audioService ??
                throw new ArgumentNullException(nameof(audioService));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _minimumIntervalSeconds = Math.Max(
                0d,
                minimumIntervalSeconds);
        }

        public bool TryPlay(
            CleaningToolKind tool,
            Vector3 worldPosition)
        {
            if (!TryMapCue(tool, out AsmrAudioCue cue))
            {
                return false;
            }

            double now = _clock.NowSeconds;
            double elapsed = now - _lastPlayedAtSeconds;
            if (_hasPlayed &&
                elapsed >= 0d &&
                elapsed + 1e-9d < _minimumIntervalSeconds)
            {
                return false;
            }

            _hasPlayed = true;
            _lastPlayedAtSeconds = now;
            _audioService.PlayCue(cue, worldPosition);
            return true;
        }

        private static bool TryMapCue(
            CleaningToolKind tool,
            out AsmrAudioCue cue)
        {
            switch (tool)
            {
                case CleaningToolKind.Cloth:
                    cue = AsmrAudioCue.CleaningCloth;
                    return true;
                case CleaningToolKind.Sponge:
                    cue = AsmrAudioCue.CleaningSponge;
                    return true;
                case CleaningToolKind.Squeegee:
                    cue = AsmrAudioCue.CleaningSqueegee;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }
    }
}
