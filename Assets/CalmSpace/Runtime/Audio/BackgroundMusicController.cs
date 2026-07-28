using System;
using UnityEngine;
using UnityEngine.Audio;

namespace CalmSpace.Audio
{
    /// <summary>
    /// Allocation-free background-music player with interruption-safe fades.
    /// An external progress/settings store supplies and persists the enabled
    /// preference through <see cref="Initialize"/> and
    /// <see cref="SetEnabled"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackgroundMusicController :
        MonoBehaviour,
        IBackgroundMusicService
    {
        private const float MinimumFadeDuration = 0.001f;
        private const float SilentVolume = 0f;

        [SerializeField]
        private AudioSource _source;

        [SerializeField]
        private AudioClip _musicClip;

        [SerializeField]
        private AudioMixerGroup _outputMixerGroup;

        [SerializeField]
        [Range(0f, 1f)]
        private float _baseVolume = 0.24f;

        [SerializeField]
        [Min(0f)]
        private float _fadeDuration = 0.3f;

        private bool _isInitialized;
        private bool _isEnabled;
        private bool _isFadeActive;
        private bool _isPausedByController;
        private bool _isPausedByLifecycle;
        private float _fadeElapsed;
        private float _fadeStartVolume;
        private float _fadeTargetVolume;
        private uint _fadeGeneration;
        private uint _activeFadeGeneration;

        public bool IsEnabled => _isInitialized && _isEnabled;

        public bool IsPlaying =>
            _source != null &&
            _source.isPlaying;

        public event Action<bool> EnabledChanged;

        private void Awake()
        {
            EnsureSource();
        }

        private void OnEnable()
        {
            if (!_isInitialized ||
                !_isEnabled ||
                !_isPausedByLifecycle)
            {
                return;
            }

            _isPausedByLifecycle = false;
            StartOrResumePlayback();
            BeginFade(Mathf.Clamp01(_baseVolume));
        }

        private void OnDisable()
        {
            CancelFade();

            if (_source == null || !_source.isPlaying)
            {
                return;
            }

            _source.Pause();
            _isPausedByLifecycle = true;
        }

        private void Update()
        {
            if (!_isFadeActive || _source == null)
            {
                return;
            }

            uint generation = _activeFadeGeneration;
            _fadeElapsed += Time.unscaledDeltaTime;

            float duration = Mathf.Max(
                MinimumFadeDuration,
                _fadeDuration);
            float normalizedTime = Mathf.Clamp01(
                _fadeElapsed / duration);
            float smoothedTime =
                normalizedTime *
                normalizedTime *
                (3f - 2f * normalizedTime);

            _source.volume = Mathf.LerpUnclamped(
                _fadeStartVolume,
                _fadeTargetVolume,
                smoothedTime);

            if (normalizedTime < 1f ||
                generation != _fadeGeneration)
            {
                return;
            }

            _source.volume = _fadeTargetVolume;
            _isFadeActive = false;

            if (!_isEnabled && _source.isPlaying)
            {
                _source.Pause();
                _isPausedByController = true;
            }
        }

        /// <summary>
        /// Applies the stored preference and starts playback when enabled.
        /// The first enabled initialization fades in from silence.
        /// </summary>
        public void Initialize(bool enabled)
        {
            EnsureSource();

            if (_isInitialized)
            {
                SetEnabled(enabled);
                return;
            }

            _isInitialized = true;
            _isEnabled = enabled;
            CancelFade();

            if (enabled)
            {
                _source.volume = SilentVolume;
                StartOrResumePlayback();
                BeginFade(Mathf.Clamp01(_baseVolume));
            }
            else
            {
                _source.Stop();
                _source.volume = SilentVolume;
                _isPausedByController = false;
            }

            EnabledChanged?.Invoke(enabled);
        }

        /// <summary>
        /// Changes playback without owning persistence. A new request replaces
        /// any in-flight fade by advancing the transition generation.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (!_isInitialized)
            {
                Initialize(enabled);
                return;
            }

            EnsureSource();
            bool changed = _isEnabled != enabled;
            _isEnabled = enabled;

            if (enabled)
            {
                StartOrResumePlayback();
                BeginFade(Mathf.Clamp01(_baseVolume));
            }
            else
            {
                BeginFade(SilentVolume);
            }

            if (changed)
            {
                EnabledChanged?.Invoke(enabled);
            }
        }

        public void Toggle()
        {
            SetEnabled(!IsEnabled);
        }

        private void EnsureSource()
        {
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
            }

            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
            }

            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.ignoreListenerPause = false;
            _source.clip = _musicClip;
            _source.outputAudioMixerGroup = _outputMixerGroup;
        }

        private void StartOrResumePlayback()
        {
            if (_source == null || _source.clip == null)
            {
                return;
            }

            if (_source.isPlaying)
            {
                _isPausedByController = false;
                _isPausedByLifecycle = false;
                return;
            }

            if (_isPausedByController || _isPausedByLifecycle)
            {
                _source.UnPause();
            }
            else
            {
                _source.Play();
            }

            _isPausedByController = false;
            _isPausedByLifecycle = false;
        }

        private void BeginFade(float targetVolume)
        {
            CancelFade();

            if (_source == null)
            {
                return;
            }

            targetVolume = Mathf.Clamp01(targetVolume);
            if (_fadeDuration <= MinimumFadeDuration ||
                Mathf.Approximately(_source.volume, targetVolume))
            {
                _source.volume = targetVolume;
                if (!_isEnabled && _source.isPlaying)
                {
                    _source.Pause();
                    _isPausedByController = true;
                }

                return;
            }

            _fadeStartVolume = _source.volume;
            _fadeTargetVolume = targetVolume;
            _fadeElapsed = 0f;
            _activeFadeGeneration = _fadeGeneration;
            _isFadeActive = true;
        }

        private void CancelFade()
        {
            unchecked
            {
                _fadeGeneration++;
            }

            _isFadeActive = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _baseVolume = Mathf.Clamp01(_baseVolume);
            _fadeDuration = Mathf.Max(0f, _fadeDuration);

            if (!Application.isPlaying || _source == null)
            {
                return;
            }

            EnsureSource();
            if (_isInitialized && _isEnabled && !_isFadeActive)
            {
                _source.volume = _baseVolume;
            }
        }
#endif
    }
}
