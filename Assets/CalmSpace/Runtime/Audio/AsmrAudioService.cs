using UnityEngine;
using UnityEngine.Audio;

namespace CalmSpace.Audio
{
    /// <summary>
    /// Fixed-size spatial one-shot pool for close, randomized ASMR feedback.
    /// </summary>
    public sealed class AsmrAudioService :
        MonoBehaviour,
        ICategorizedAsmrAudioService
    {
        [SerializeField]
        [Min(1)]
        private int _poolSize = 8;

        [SerializeField]
        private AudioClip[] _snapClips = new AudioClip[0];

        [SerializeField] private AudioClip[] _screwTurnClips = new AudioClip[0];
        [SerializeField] private AudioClip[] _screwReleaseClips = new AudioClip[0];
        [SerializeField] private AudioClip[] _cleaningClothClips = new AudioClip[0];
        [SerializeField] private AudioClip[] _cleaningSpongeClips = new AudioClip[0];
        [SerializeField] private AudioClip[] _cleaningSqueegeeClips = new AudioClip[0];
        [SerializeField] private AudioClip[] _levelCompleteClips = new AudioClip[0];
        [SerializeField] private AudioClip[] _roomRevealClips = new AudioClip[0];
        [SerializeField] private AudioClip[] _uiTapClips = new AudioClip[0];

        [SerializeField]
        private AudioMixerGroup _outputMixerGroup;

        [SerializeField]
        [Range(0f, 1f)]
        private float _minimumVolume = 0.82f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _maximumVolume = 0.95f;

        [SerializeField]
        [Range(0.5f, 2f)]
        private float _minimumPitch = 0.94f;

        [SerializeField]
        [Range(0.5f, 2f)]
        private float _maximumPitch = 1.06f;

        [SerializeField]
        [Min(0.01f)]
        private float _minimumDistance = 0.25f;

        [SerializeField]
        [Min(0.1f)]
        private float _maximumDistance = 8f;

        private AudioSource[] _sources;
        private double[] _releaseDspTimes;

        public bool IsAvailable =>
            _sources != null &&
            _sources.Length > 0 &&
            HasAnyPlayableClip();

        private void Awake()
        {
            Prewarm();
        }

        public void PlaySnap(Vector3 worldPosition)
        {
            PlayCue(AsmrAudioCue.Placement, worldPosition);
        }

        public void PlayCue(
            AsmrAudioCue cue,
            Vector3 worldPosition)
        {
            Prewarm();

            AudioClip clip = SelectClip(BankFor(cue));
            if (clip == null || _sources.Length == 0)
            {
                return;
            }

            var sourceIndex = SelectSourceIndex();
            var source = _sources[sourceIndex];
            var minimumVolume = Mathf.Min(
                _minimumVolume,
                _maximumVolume);
            var maximumVolume = Mathf.Max(
                _minimumVolume,
                _maximumVolume);
            var minimumPitch = Mathf.Min(
                _minimumPitch,
                _maximumPitch);
            var maximumPitch = Mathf.Max(
                _minimumPitch,
                _maximumPitch);

            source.Stop();
            bool spatial = IsSpatial(cue);
            source.spatialBlend = spatial ? 1f : 0f;
            if (spatial)
            {
                source.transform.position = worldPosition;
            }
            else
            {
                source.transform.localPosition = Vector3.zero;
            }
            source.clip = clip;
            source.volume = Random.Range(minimumVolume, maximumVolume);
            source.pitch = Random.Range(minimumPitch, maximumPitch);
            source.Play();

            _releaseDspTimes[sourceIndex] =
                AudioSettings.dspTime +
                clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch));
        }

        private AudioClip[] BankFor(AsmrAudioCue cue)
        {
            switch (cue)
            {
                case AsmrAudioCue.Placement:
                    return _snapClips;
                case AsmrAudioCue.ScrewTurn:
                    return _screwTurnClips;
                case AsmrAudioCue.ScrewRelease:
                    return _screwReleaseClips;
                case AsmrAudioCue.CleaningCloth:
                    return _cleaningClothClips;
                case AsmrAudioCue.CleaningSponge:
                    return _cleaningSpongeClips;
                case AsmrAudioCue.CleaningSqueegee:
                    return _cleaningSqueegeeClips;
                case AsmrAudioCue.LevelComplete:
                    return _levelCompleteClips;
                case AsmrAudioCue.RoomReveal:
                    return _roomRevealClips;
                case AsmrAudioCue.UiTap:
                    return _uiTapClips;
                default:
                    return null;
            }
        }

        private static bool IsSpatial(AsmrAudioCue cue)
        {
            return cue == AsmrAudioCue.Placement ||
                cue == AsmrAudioCue.ScrewTurn ||
                cue == AsmrAudioCue.ScrewRelease ||
                cue == AsmrAudioCue.CleaningCloth ||
                cue == AsmrAudioCue.CleaningSponge ||
                cue == AsmrAudioCue.CleaningSqueegee;
        }

        private void Prewarm()
        {
            if (_sources != null)
            {
                return;
            }

            var size = Mathf.Max(1, _poolSize);
            _sources = new AudioSource[size];
            _releaseDspTimes = new double[size];

            for (var index = 0; index < size; index++)
            {
                var sourceObject = new GameObject(
                    "ASMR Spatial Source " + (index + 1));
                sourceObject.transform.SetParent(transform, false);

                var source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 1f;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = Mathf.Max(0.01f, _minimumDistance);
                source.maxDistance = Mathf.Max(
                    source.minDistance,
                    _maximumDistance);
                source.outputAudioMixerGroup = _outputMixerGroup;
                _sources[index] = source;
            }
        }

        private int SelectSourceIndex()
        {
            var earliestIndex = 0;
            var earliestRelease = _releaseDspTimes[0];

            for (var index = 0; index < _sources.Length; index++)
            {
                if (!_sources[index].isPlaying)
                {
                    return index;
                }

                if (_releaseDspTimes[index] < earliestRelease)
                {
                    earliestIndex = index;
                    earliestRelease = _releaseDspTimes[index];
                }
            }

            return earliestIndex;
        }

        private static AudioClip SelectClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            var startIndex = Random.Range(0, clips.Length);
            for (var offset = 0; offset < clips.Length; offset++)
            {
                var index = (startIndex + offset) % clips.Length;
                if (clips[index] != null)
                {
                    return clips[index];
                }
            }

            return null;
        }

        private bool HasAnyPlayableClip()
        {
            for (var cue = AsmrAudioCue.Placement;
                 cue <= AsmrAudioCue.UiTap;
                 cue++)
            {
                AudioClip[] bank = BankFor(cue);
                if (SelectFirstPlayable(bank) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static AudioClip SelectFirstPlayable(AudioClip[] clips)
        {
            if (clips == null)
            {
                return null;
            }

            for (var index = 0; index < clips.Length; index++)
            {
                if (clips[index] != null)
                {
                    return clips[index];
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _poolSize = Mathf.Max(1, _poolSize);
            _minimumDistance = Mathf.Max(0.01f, _minimumDistance);
            _maximumDistance = Mathf.Max(
                _minimumDistance,
                _maximumDistance);
        }
#endif
    }
}
