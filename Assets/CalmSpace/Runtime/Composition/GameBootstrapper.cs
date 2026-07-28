using System;
using System.Threading;
using CalmSpace.Monetization;
using CalmSpace.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace CalmSpace.Core
{
    /// <summary>
    /// Initializes policy services and loads the first Addressable prefab into
    /// the persistent gameplay scene.
    /// </summary>
    public sealed class GameBootstrapper : IStartable, IDisposable
    {
        private readonly IMonetizationManager _monetization;
        private readonly IDemoExperienceController _experience;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        public GameBootstrapper(
            IMonetizationManager monetization,
            IDemoExperienceController experience)
        {
            _monetization = monetization ??
                throw new ArgumentNullException(nameof(monetization));
            _experience = experience ??
                throw new ArgumentNullException(nameof(experience));
        }

        public void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            StartAsync(_lifetimeCancellation.Token).Forget();
        }

        public void Dispose()
        {
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        private async UniTaskVoid StartAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                await _monetization.InitializeAsync(
                    cancellationToken);
                await _experience.InitializeAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected while the scene/application is shutting down.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
