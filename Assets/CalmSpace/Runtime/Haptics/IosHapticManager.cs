using System;
using System.Runtime.InteropServices;
using CalmSpace.Core;
using UnityEngine;

namespace CalmSpace.Haptics
{
    /// <summary>
    /// iOS tactile feedback backed by UIFeedbackGenerator. The native plugin
    /// uses Core Haptics hardware capability checks on iOS 13+ and gracefully
    /// becomes a no-op on unsupported devices.
    /// </summary>
    public sealed class IosHapticManager : IHapticService, IDisposable
    {
        private const double DragTickIntervalSeconds = 0.04d;
        private const double PostSnapSilenceSeconds = 0.10d;

        private readonly IMonotonicClock _clock;
        private readonly HapticRateLimiter _rateLimiter;
        private bool _disposed;

#if UNITY_IOS && !UNITY_EDITOR
        private bool _nativeSupported;
#endif

        public IosHapticManager(IMonotonicClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _rateLimiter = new HapticRateLimiter(
                DragTickIntervalSeconds,
                PostSnapSilenceSeconds);

#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                _nativeSupported = CalmSpaceHapticsIsSupported();
            }
            catch (Exception exception)
            {
                _nativeSupported = false;
                Debug.LogWarning(
                    "Calm Space iOS haptics could not initialize: " +
                    exception.GetType().Name);
            }
#endif
        }

        public bool IsSupported
        {
            get
            {
                if (_disposed)
                {
                    return false;
                }

#if UNITY_IOS && !UNITY_EDITOR
                return _nativeSupported;
#else
                return false;
#endif
            }
        }

        public void PlayDragTick(float intensity)
        {
            if (_disposed ||
                !_rateLimiter.TryConsumeDragTick(_clock.NowSeconds))
            {
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            if (!_nativeSupported)
            {
                return;
            }

            try
            {
                CalmSpaceHapticsPlayDragTick(
                    NormalizeIntensity(intensity));
            }
            catch (Exception)
            {
                _nativeSupported = false;
            }
#endif
        }

        public void PlaySnap()
        {
            if (_disposed ||
                !_rateLimiter.TryConsumeSnap(_clock.NowSeconds))
            {
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            if (!_nativeSupported)
            {
                return;
            }

            try
            {
                CalmSpaceHapticsPlaySnap();
            }
            catch (Exception)
            {
                _nativeSupported = false;
            }
#endif
        }

        public void Cancel()
        {
            if (_disposed)
            {
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            if (!_nativeSupported)
            {
                return;
            }

            try
            {
                CalmSpaceHapticsCancel();
            }
            catch (Exception)
            {
                _nativeSupported = false;
            }
#endif
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Cancel();
            _disposed = true;
        }

        private static float NormalizeIntensity(float intensity)
        {
            return float.IsNaN(intensity) || float.IsInfinity(intensity)
                ? 0f
                : Mathf.Clamp01(intensity);
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CalmSpaceHapticsIsSupported();

        [DllImport("__Internal")]
        private static extern void CalmSpaceHapticsPlayDragTick(
            float intensity);

        [DllImport("__Internal")]
        private static extern void CalmSpaceHapticsPlaySnap();

        [DllImport("__Internal")]
        private static extern void CalmSpaceHapticsCancel();
#endif
    }
}
