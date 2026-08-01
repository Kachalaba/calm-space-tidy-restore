using System;
using UnityEngine;
using VContainer;

namespace CalmSpace.Haptics
{
    public enum HapticPlatformKind
    {
        Unsupported = 0,
        Android = 1,
        Ios = 2
    }

    public interface IHapticServiceFactory
    {
        IHapticService Create(
            RuntimePlatform platform,
            Core.IMonotonicClock clock);
    }

    /// <summary>
    /// Keeps platform selection out of gameplay code and makes the choice
    /// deterministic in tests. Each concrete service shares the same
    /// HapticRateLimiter policy.
    /// </summary>
    public sealed class PlatformHapticServiceFactory :
        IHapticServiceFactory
    {
        public IHapticService Create(
            RuntimePlatform platform,
            Core.IMonotonicClock clock)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            switch (ResolvePlatformKind(platform))
            {
                case HapticPlatformKind.Android:
                    return new AndroidHapticManager(clock);
                case HapticPlatformKind.Ios:
                    return new IosHapticManager(clock);
                default:
                    return NullHapticService.Instance;
            }
        }

        public static HapticPlatformKind ResolvePlatformKind(
            RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                    return HapticPlatformKind.Android;
                case RuntimePlatform.IPhonePlayer:
                    return HapticPlatformKind.Ios;
                default:
                    return HapticPlatformKind.Unsupported;
            }
        }
    }

    /// <summary>
    /// Explicit null object used in the editor and on platforms without
    /// tactile feedback. Gameplay never needs platform conditionals.
    /// </summary>
    public sealed class NullHapticService : IHapticService
    {
        public static readonly NullHapticService Instance =
            new NullHapticService();

        private NullHapticService()
        {
        }

        public bool IsSupported => false;

        public void PlayDragTick(float intensity)
        {
        }

        public void PlaySnap()
        {
        }

        public void Cancel()
        {
        }
    }

    /// <summary>
    /// Stops the selected native haptic engine whenever Unity loses
    /// foreground ownership.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HapticLifecycleRelay : MonoBehaviour
    {
        private IHapticService _haptics;

        [Inject]
        public void Construct(IHapticService haptics)
        {
            _haptics = haptics ??
                throw new ArgumentNullException(nameof(haptics));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _haptics?.Cancel();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                _haptics?.Cancel();
            }
        }

        private void OnDisable()
        {
            _haptics?.Cancel();
        }
    }
}
