using System;
using UnityEngine;
using VContainer;

namespace CalmSpace.Haptics
{
    /// <summary>
    /// Stops native vibration whenever Unity loses foreground ownership.
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
