using System;
using System.IO;
using System.Text;
using CalmSpace.Audio;
using CalmSpace.Core;
using CalmSpace.Demo;
using CalmSpace.Haptics;
using CalmSpace.Levels;
using CalmSpace.Monetization;
using CalmSpace.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CalmSpace.Core
{
    /// <summary>
    /// Single-scene composition root. Provider-specific ad/IAP adapters can
    /// replace the conservative no-op registrations without changing policy.
    /// </summary>
    public sealed class CalmSpaceLifetimeScope : LifetimeScope
    {
        private const string KeystoreAlias =
            "com.calmspace.tidyrestore.noads.v1";
        private const string StateFileName =
            "monetization-state-v1.bin";

        private static readonly byte[] StateAssociatedData =
            Encoding.UTF8.GetBytes(
                "CalmSpace.MonetizationState.v1");

        [SerializeField]
        private LevelCatalog _levelCatalog;

        [SerializeField]
        private Transform _levelRoot;

        [SerializeField]
        private DemoThemeCatalog _demoThemeCatalog;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_levelCatalog == null)
            {
                throw new InvalidOperationException(
                    "Assign a LevelCatalog to CalmSpaceLifetimeScope.");
            }

            if (_levelRoot == null)
            {
                throw new InvalidOperationException(
                    "Assign a level root to CalmSpaceLifetimeScope.");
            }

            if (_demoThemeCatalog == null)
            {
                throw new InvalidOperationException(
                    "Assign a DemoThemeCatalog to CalmSpaceLifetimeScope.");
            }

            builder.RegisterInstance(_levelCatalog);
            builder.RegisterInstance(_demoThemeCatalog);

            builder
                .Register<PresentationActivityCoordinator>(
                    _ => new PresentationActivityCoordinator(),
                    Lifetime.Singleton)
                .As<IPresentationActivityCoordinator>();
            builder
                .Register<UnityMonotonicClock>(
                    _ => new UnityMonotonicClock(),
                    Lifetime.Singleton)
                .As<IMonotonicClock>();
            builder
                .Register<AndroidHapticManager>(
                    resolver =>
                        new AndroidHapticManager(
                            resolver.Resolve<IMonotonicClock>()),
                    Lifetime.Singleton)
                .As<IHapticService>();

            builder
                .RegisterComponentInHierarchy<AsmrAudioService>()
                .As<IAsmrAudioService>();
            builder
                .RegisterComponentInHierarchy<
                    BackgroundMusicController>()
                .As<IBackgroundMusicService>();
            builder.RegisterComponentInHierarchy<HapticLifecycleRelay>();

            RegisterMonetization(builder);

            builder
                .Register<PlayerPrefsDemoProgressStore>(
                    _ => new PlayerPrefsDemoProgressStore(),
                    Lifetime.Singleton)
                .As<IDemoProgressStore>();
            builder
                .Register<DemoThemeService>(
                    resolver =>
                        new DemoThemeService(
                            _demoThemeCatalog,
                            resolver.Resolve<IDemoProgressStore>()),
                    Lifetime.Singleton)
                .As<IDemoThemeService>();
            builder
                .Register<DemoLocalizationService>(
                    _ => new DemoLocalizationService(),
                    Lifetime.Singleton)
                .As<IDemoLocalizationService>();

            builder.Register<ILevelFlowController>(
                resolver =>
                    new AddressableLevelFlowController(
                        _levelCatalog,
                        resolver,
                        resolver.Resolve<IMonetizationManager>(),
                        _levelRoot),
                Lifetime.Singleton);

            builder
                .RegisterComponentInHierarchy<
                    DemoExperienceController>()
                .As<IDemoExperienceController>();
            builder.RegisterEntryPoint<GameBootstrapper>(
                resolver =>
                    new GameBootstrapper(
                        resolver.Resolve<IMonetizationManager>(),
                        resolver.Resolve<
                            IDemoExperienceController>()),
                Lifetime.Singleton);
        }

        private static void RegisterMonetization(
            IContainerBuilder builder)
        {
            builder
                .Register<NoOpAdProvider>(
                    _ => new NoOpAdProvider(),
                    Lifetime.Singleton)
                .As<IAdProvider>();
            builder
                .Register<NoOpNoAdsEntitlementProvider>(
                    _ => new NoOpNoAdsEntitlementProvider(),
                    Lifetime.Singleton)
                .As<INoAdsEntitlementProvider>();

            var protector =
                new AndroidKeystoreAuthenticatedDataProtector(
                    KeystoreAlias);
            builder.RegisterInstance<IAuthenticatedDataProtector>(
                protector);

            var statePath = Path.Combine(
                Application.persistentDataPath,
                StateFileName);
            var stateStore =
                new EncryptedFileMonetizationStateStore(
                    statePath,
                    protector,
                    StateAssociatedData);
            builder.RegisterInstance<IMonetizationStateStore>(
                stateStore);

            builder.RegisterInstance(
                new MonetizationOptions(
                    TimeSpan.FromSeconds(180d),
                    allowRewardedForNoAdsOwners: true));
            builder
                .Register<MonetizationManager>(
                    resolver =>
                        new MonetizationManager(
                            resolver.Resolve<IAdProvider>(),
                            resolver.Resolve<
                                INoAdsEntitlementProvider>(),
                            resolver.Resolve<IMonetizationStateStore>(),
                            resolver.Resolve<
                                IPresentationActivityCoordinator>(),
                            resolver.Resolve<IMonotonicClock>(),
                            resolver.Resolve<MonetizationOptions>()),
                    Lifetime.Singleton)
                .As<IMonetizationManager>();
        }
    }
}
