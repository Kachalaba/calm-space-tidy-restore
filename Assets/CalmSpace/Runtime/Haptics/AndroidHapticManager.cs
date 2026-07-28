using System;
using CalmSpace.Core;
using UnityEngine;

namespace CalmSpace.Haptics
{
    /// <summary>
    /// Android API 24+ vibrator bridge with cached effects and a conservative
    /// Unity fallback used only after JNI bridge failure.
    /// </summary>
    public sealed class AndroidHapticManager : IHapticService, IDisposable
    {
        private const double DragTickIntervalSeconds = 0.04d;
        private const double PostSnapSilenceSeconds = 0.10d;
        private const long DragTickDurationMilliseconds = 9L;
        private const int MinimumDragAmplitude = 10;
        private const int MaximumDragAmplitude = 30;
        private const int DefaultAmplitude = -1;
        private const int NoRepeat = -1;
        private const int ApiVibrationEffect = 26;
        private const int ApiVibratorManager = 31;
        private const int ApiVibrationAttributes = 33;
        private const int VibrationUsageTouch = 18;

        private static readonly long[] SnapTimingsMilliseconds =
        {
            0L,
            12L,
            24L,
            20L
        };

        private static readonly int[] SnapAmplitudes =
        {
            0,
            90,
            0,
            155
        };

        private readonly IMonotonicClock _clock;
        private readonly HapticRateLimiter _rateLimiter;
        private bool _disposed;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _activity;
        private AndroidJavaObject _applicationContext;
        private AndroidJavaObject _vibratorManager;
        private AndroidJavaObject _vibrator;
        private AndroidJavaClass _vibrationEffectClass;
        private AndroidJavaClass _vibrationAttributesClass;
        private AndroidJavaObject _touchVibrationAttributes;
        private AndroidJavaObject[] _dragEffects;
        private AndroidJavaObject _defaultDragEffect;
        private AndroidJavaObject _snapEffect;
        private int _sdkLevel;
        private bool _hasAmplitudeControl;
        private bool _bridgeReady;
        private bool _bridgeFailed;
        private bool _hasVibrator;
        private IntPtr _vibrateEffectMethod;
        private IntPtr _vibrateAttributedEffectMethod;
        private IntPtr _vibrateDurationMethod;
        private IntPtr _cancelMethod;
        private jvalue[] _effectArguments;
        private jvalue[] _attributedEffectArguments;
        private jvalue[] _durationArguments;
        private static readonly jvalue[] EmptyJniArguments =
            new jvalue[0];
#endif

        public AndroidHapticManager(IMonotonicClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _rateLimiter = new HapticRateLimiter(
                DragTickIntervalSeconds,
                PostSnapSilenceSeconds);

#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeBridge();
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

#if UNITY_ANDROID && !UNITY_EDITOR
                return (_bridgeReady && _hasVibrator) || _bridgeFailed;
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

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_bridgeReady && _hasVibrator)
            {
                try
                {
                    if (_sdkLevel >= ApiVibrationEffect)
                    {
                        var effect = GetDragEffect(intensity);
                        VibrateEffect(effect);
                    }
                    else
                    {
                        VibrateDuration(
                            DragTickDurationMilliseconds);
                    }

                    return;
                }
                catch (Exception)
                {
                    MarkBridgeFailed();
                }
            }

            // Handheld.Vibrate is a long, non-cancelable pulse on many
            // devices. It is intentionally never used for continuous ticks.
#endif
        }

        public void PlaySnap()
        {
            if (_disposed)
            {
                return;
            }

            Cancel();
            _rateLimiter.RecordSnap(_clock.NowSeconds);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_bridgeReady && _hasVibrator)
            {
                try
                {
                    if (_sdkLevel >= ApiVibrationEffect)
                    {
                        VibrateEffect(_snapEffect);
                    }
                    else
                    {
                        _vibrator.Call(
                            "vibrate",
                            SnapTimingsMilliseconds,
                            NoRepeat);
                    }

                    return;
                }
                catch (Exception)
                {
                    MarkBridgeFailed();
                }
            }

            VibrateSnapWithUnityFallbackAfterBridgeFailure();
#endif
        }

        public void Cancel()
        {
            if (_disposed)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_bridgeReady || !_hasVibrator)
            {
                return;
            }

            try
            {
                AndroidJNI.CallVoidMethod(
                    _vibrator.GetRawObject(),
                    _cancelMethod,
                    EmptyJniArguments);
                ThrowIfJniException();
            }
            catch (Exception)
            {
                MarkBridgeFailed();
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

#if UNITY_ANDROID && !UNITY_EDITOR
            DisposeBridgeObjects();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void InitializeBridge()
        {
            try
            {
                using (var unityPlayer =
                       new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var buildVersion =
                       new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _activity = unityPlayer.GetStatic<AndroidJavaObject>(
                        "currentActivity");
                    _sdkLevel = buildVersion.GetStatic<int>("SDK_INT");
                }

                if (_activity == null)
                {
                    throw new InvalidOperationException(
                        "UnityPlayer.currentActivity was unavailable.");
                }

                _applicationContext =
                    _activity.Call<AndroidJavaObject>("getApplicationContext");
                if (_applicationContext == null)
                {
                    throw new InvalidOperationException(
                        "Android application context was unavailable.");
                }

                if (_sdkLevel >= ApiVibratorManager)
                {
                    _vibratorManager =
                        _applicationContext.Call<AndroidJavaObject>(
                            "getSystemService",
                            "vibrator_manager");
                    _vibrator = _vibratorManager?.Call<AndroidJavaObject>(
                        "getDefaultVibrator");
                }
                else
                {
                    _vibrator =
                        _applicationContext.Call<AndroidJavaObject>(
                            "getSystemService",
                            "vibrator");
                }

                if (_vibrator == null)
                {
                    throw new InvalidOperationException(
                        "Android vibrator service was unavailable.");
                }

                _hasVibrator = _vibrator.Call<bool>("hasVibrator");
                if (!_hasVibrator)
                {
                    _bridgeReady = true;
                    return;
                }

                if (_sdkLevel >= ApiVibrationEffect)
                {
                    _hasAmplitudeControl =
                        _vibrator.Call<bool>("hasAmplitudeControl");
                    CacheVibrationEffects();
                    CacheVibrationAttributes();
                }

                CacheVibratorMethodIds();
                _bridgeReady = true;
            }
            catch (Exception)
            {
                MarkBridgeFailed();
            }
        }

        private void CacheVibrationEffects()
        {
            _vibrationEffectClass =
                new AndroidJavaClass("android.os.VibrationEffect");

            if (_hasAmplitudeControl)
            {
                var effectCount =
                    MaximumDragAmplitude - MinimumDragAmplitude + 1;
                _dragEffects = new AndroidJavaObject[effectCount];

                for (var index = 0; index < effectCount; index++)
                {
                    _dragEffects[index] =
                        _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot",
                            DragTickDurationMilliseconds,
                            MinimumDragAmplitude + index);
                }

                _snapEffect =
                    _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createWaveform",
                        SnapTimingsMilliseconds,
                        SnapAmplitudes,
                        NoRepeat);
            }
            else
            {
                _defaultDragEffect =
                    _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot",
                        DragTickDurationMilliseconds,
                        DefaultAmplitude);
                _snapEffect =
                    _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createWaveform",
                        SnapTimingsMilliseconds,
                        NoRepeat);
            }
        }

        private AndroidJavaObject GetDragEffect(float intensity)
        {
            if (!_hasAmplitudeControl)
            {
                return _defaultDragEffect;
            }

            if (float.IsNaN(intensity) || float.IsInfinity(intensity))
            {
                intensity = 0f;
            }

            var amplitude = Mathf.RoundToInt(
                Mathf.Lerp(
                    MinimumDragAmplitude,
                    MaximumDragAmplitude,
                    Mathf.Clamp01(intensity)));
            var index = amplitude - MinimumDragAmplitude;
            return _dragEffects[index];
        }

        private void CacheVibrationAttributes()
        {
            if (_sdkLevel < ApiVibrationAttributes)
            {
                return;
            }

            _vibrationAttributesClass =
                new AndroidJavaClass("android.os.VibrationAttributes");
            _touchVibrationAttributes =
                _vibrationAttributesClass.CallStatic<AndroidJavaObject>(
                    "createForUsage",
                    VibrationUsageTouch);
            if (_touchVibrationAttributes == null)
            {
                throw new InvalidOperationException(
                    "Touch VibrationAttributes were unavailable.");
            }
        }

        private void CacheVibratorMethodIds()
        {
            var vibratorObject = _vibrator.GetRawObject();
            var vibratorClass = AndroidJNI.GetObjectClass(
                vibratorObject);

            try
            {
                _cancelMethod = AndroidJNI.GetMethodID(
                    vibratorClass,
                    "cancel",
                    "()V");

                if (_sdkLevel >= ApiVibrationEffect)
                {
                    if (_sdkLevel >= ApiVibrationAttributes)
                    {
                        _vibrateAttributedEffectMethod =
                            AndroidJNI.GetMethodID(
                                vibratorClass,
                                "vibrate",
                                "(Landroid/os/VibrationEffect;" +
                                "Landroid/os/VibrationAttributes;)V");
                        _attributedEffectArguments = new jvalue[2];
                    }
                    else
                    {
                        _vibrateEffectMethod = AndroidJNI.GetMethodID(
                            vibratorClass,
                            "vibrate",
                            "(Landroid/os/VibrationEffect;)V");
                        _effectArguments = new jvalue[1];
                    }
                }
                else
                {
                    _vibrateDurationMethod = AndroidJNI.GetMethodID(
                        vibratorClass,
                        "vibrate",
                        "(J)V");
                    _durationArguments = new jvalue[1];
                }
            }
            finally
            {
                AndroidJNI.DeleteLocalRef(vibratorClass);
            }
        }

        private void VibrateEffect(AndroidJavaObject effect)
        {
            if (effect == null)
            {
                throw new InvalidOperationException(
                    "VibrationEffect was unavailable.");
            }

            if (_sdkLevel >= ApiVibrationAttributes)
            {
                VibrateAttributedEffect(effect);
                return;
            }

            if (_vibrateEffectMethod == IntPtr.Zero ||
                _effectArguments == null)
            {
                throw new InvalidOperationException(
                    "VibrationEffect JNI method was not initialized.");
            }

            _effectArguments[0].l = effect.GetRawObject();
            AndroidJNI.CallVoidMethod(
                _vibrator.GetRawObject(),
                _vibrateEffectMethod,
                _effectArguments);
            ThrowIfJniException();
        }

        private void VibrateAttributedEffect(AndroidJavaObject effect)
        {
            if (_vibrateAttributedEffectMethod == IntPtr.Zero ||
                _attributedEffectArguments == null ||
                _touchVibrationAttributes == null)
            {
                throw new InvalidOperationException(
                    "Attributed VibrationEffect JNI method was not " +
                    "initialized.");
            }

            _attributedEffectArguments[0].l = effect.GetRawObject();
            _attributedEffectArguments[1].l =
                _touchVibrationAttributes.GetRawObject();
            AndroidJNI.CallVoidMethod(
                _vibrator.GetRawObject(),
                _vibrateAttributedEffectMethod,
                _attributedEffectArguments);
            ThrowIfJniException();
        }

        private void VibrateDuration(long durationMilliseconds)
        {
            if (_vibrateDurationMethod == IntPtr.Zero ||
                _durationArguments == null)
            {
                throw new InvalidOperationException(
                    "Legacy vibrator JNI method was not initialized.");
            }

            _durationArguments[0].j = durationMilliseconds;
            AndroidJNI.CallVoidMethod(
                _vibrator.GetRawObject(),
                _vibrateDurationMethod,
                _durationArguments);
            ThrowIfJniException();
        }

        private static void ThrowIfJniException()
        {
            var exception = AndroidJNI.ExceptionOccurred();
            if (exception == IntPtr.Zero)
            {
                return;
            }

            AndroidJNI.ExceptionDescribe();
            AndroidJNI.ExceptionClear();
            AndroidJNI.DeleteLocalRef(exception);
            throw new InvalidOperationException(
                "Android vibrator JNI call failed.");
        }

        private void VibrateSnapWithUnityFallbackAfterBridgeFailure()
        {
            if (_bridgeFailed)
            {
                Handheld.Vibrate();
            }
        }

        private void MarkBridgeFailed()
        {
            _bridgeReady = false;
            _bridgeFailed = true;
        }

        private void DisposeBridgeObjects()
        {
            if (_dragEffects != null)
            {
                for (var index = 0; index < _dragEffects.Length; index++)
                {
                    _dragEffects[index]?.Dispose();
                    _dragEffects[index] = null;
                }

                _dragEffects = null;
            }

            _defaultDragEffect?.Dispose();
            _defaultDragEffect = null;
            _snapEffect?.Dispose();
            _snapEffect = null;
            _touchVibrationAttributes?.Dispose();
            _touchVibrationAttributes = null;
            _vibrationAttributesClass?.Dispose();
            _vibrationAttributesClass = null;
            _vibrationEffectClass?.Dispose();
            _vibrationEffectClass = null;
            _vibrator?.Dispose();
            _vibrator = null;
            _vibratorManager?.Dispose();
            _vibratorManager = null;
            _applicationContext?.Dispose();
            _applicationContext = null;
            _activity?.Dispose();
            _activity = null;
            _hasVibrator = false;
            _bridgeReady = false;
            _vibrateEffectMethod = IntPtr.Zero;
            _vibrateAttributedEffectMethod = IntPtr.Zero;
            _vibrateDurationMethod = IntPtr.Zero;
            _cancelMethod = IntPtr.Zero;
            _effectArguments = null;
            _attributedEffectArguments = null;
            _durationArguments = null;
        }
#endif
    }
}
