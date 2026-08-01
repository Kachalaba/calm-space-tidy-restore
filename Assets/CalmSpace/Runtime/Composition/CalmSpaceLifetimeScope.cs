using System;
using System.IO;
using System.Text;
using CalmSpace.Analytics;
using CalmSpace.Audio;
using CalmSpace.Core;
using CalmSpace.Demo;
using CalmSpace.Haptics;
using CalmSpace.Levels;
using CalmSpace.Monetization;
using CalmSpace.UI;
using CalmSpace.Workshop;
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
        private const string DevelopmentKeyFileName =
            "development-auth-key-v1.bin";
        private const string PlayerProfileFileName =
            "player-profile-v1.bin";

        private static readonly byte[] StateAssociatedData =
            Encoding.UTF8.GetBytes(
                "CalmSpace.MonetizationState.v1");

        private static readonly byte[] ProfileAssociatedData =
            Encoding.UTF8.GetBytes(
                "CalmSpace.PlayerProfile.v1");

        [SerializeField]
        private LevelCatalog _levelCatalog;

        [SerializeField]
        private Transform _levelRoot;

        [SerializeField]
        private DemoThemeCatalog _demoThemeCatalog;

        [SerializeField]
        private DemoDecorationCatalog _demoDecorationCatalog;

        [SerializeField]
        private LivingWorkshopCatalog _livingWorkshopCatalog;

        [SerializeField]
        private WorkshopTextCatalog _workshopTextCatalog;

        protected override void Configure(IContainerBuilder builder)
        {
            if (builder.ApplicationOrigin == null)
            {
                builder.ApplicationOrigin = this;
            }

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

            if (_demoDecorationCatalog == null)
            {
                throw new InvalidOperationException(
                    "Assign a DemoDecorationCatalog to " +
                    "CalmSpaceLifetimeScope.");
            }

            LivingWorkshopCatalog runtimeWorkshop =
                _livingWorkshopCatalog != null
                    ? _livingWorkshopCatalog
                    : ScriptableObject.CreateInstance<
                        LivingWorkshopCatalog>();
            WorkshopTextCatalog runtimeWorkshopText =
                _workshopTextCatalog != null
                    ? _workshopTextCatalog
                    : ScriptableObject.CreateInstance<WorkshopTextCatalog>();
            var workshopAvailability = new WorkshopRuntimeAvailability(
                _levelCatalog,
                runtimeWorkshop,
                runtimeWorkshopText);

            builder.RegisterInstance(_levelCatalog);
            builder.RegisterInstance(_demoThemeCatalog);
            builder.RegisterInstance(_demoDecorationCatalog);
            builder.RegisterInstance(runtimeWorkshop);
            builder.RegisterInstance(runtimeWorkshopText);
            builder.RegisterInstance(workshopAvailability);
            builder.Register<WorkshopAnalyticsSessionState>(
                _ => new WorkshopAnalyticsSessionState(),
                Lifetime.Singleton);

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
                .Register<DevelopmentProductAnalyticsSink>(
                    _ => new DevelopmentProductAnalyticsSink(),
                    Lifetime.Singleton)
                .As<IProductAnalyticsSink>();
            builder
                .Register<SafeProductAnalyticsService>(
                    resolver =>
                        new SafeProductAnalyticsService(
                            resolver.Resolve<IProductAnalyticsSink>()),
                    Lifetime.Singleton)
                .As<IProductAnalytics>();
            builder
                .Register<LevelSessionUndoHistory>(
                    _ => new LevelSessionUndoHistory(),
                    Lifetime.Singleton)
                .As<IUndoHistory>();
            builder
                .Register<PlatformHapticServiceFactory>(
                    _ => new PlatformHapticServiceFactory(),
                    Lifetime.Singleton)
                .As<IHapticServiceFactory>();
            builder.Register<IHapticService>(
                resolver =>
                    resolver.Resolve<IHapticServiceFactory>().Create(
                        Application.platform,
                        resolver.Resolve<IMonotonicClock>()),
                Lifetime.Singleton);

            builder
                .RegisterComponentInHierarchy<AsmrAudioService>()
                .As<IAsmrAudioService>();
            builder
                .RegisterComponentInHierarchy<
                    BackgroundMusicController>()
                .As<IBackgroundMusicService>();
            builder.RegisterComponentInHierarchy<HapticLifecycleRelay>();

            RegisterMonetization(builder);

            var profilePath = Path.Combine(
                Application.persistentDataPath,
                PlayerProfileFileName);
            builder.Register<IDemoProgressStore>(
                resolver =>
                    new EncryptedFileDemoProgressStore(
                        profilePath,
                        resolver.Resolve<
                            IAuthenticatedDataProtector>(),
                        ProfileAssociatedData),
                Lifetime.Singleton);
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

            builder.Register<WorkshopTextService>(
                    resolver => new WorkshopTextService(
                        runtimeWorkshopText,
                        resolver.Resolve<IDemoLocalizationService>()),
                    Lifetime.Singleton)
                .As<IWorkshopTextService>();

            builder.Register<AddressableWorkshopRoomLoader>(
                    _ => new AddressableWorkshopRoomLoader(),
                    Lifetime.Singleton)
                .As<IWorkshopRoomLoader>();

            builder.Register<WorkshopProgressProjector>(
                    resolver => new WorkshopProgressProjector(
                        _levelCatalog,
                        runtimeWorkshop),
                    Lifetime.Singleton)
                .As<IWorkshopProgressProjector>();

            builder.Register<WorkshopFlowCoordinator>(
                    resolver => new WorkshopFlowCoordinator(
                        _levelCatalog,
                        runtimeWorkshop,
                        resolver.Resolve<IDemoProgressStore>(),
                        resolver.Resolve<IWorkshopProgressProjector>(),
                        resolver.Resolve<WorkshopRuntimeAvailability>()),
                    Lifetime.Singleton)
                .As<IWorkshopFlowCoordinator>();

            builder.Register<ILevelFlowController>(
                resolver =>
                    new AddressableLevelFlowController(
                        _levelCatalog,
                        resolver,
                        resolver.Resolve<IUndoHistory>(),
                        _levelRoot),
                Lifetime.Singleton);

            builder
                .RegisterComponentInHierarchy<WorkshopHomeController>()
                .As<IWorkshopHomeController>()
                .As<IWorkshopHomeRecovery>();
            builder
                .RegisterComponentInHierarchy<
                    DemoExperienceController>()
                .As<IDemoExperienceController>();
            builder.RegisterEntryPoint<GameBootstrapper>(
                resolver =>
                    new GameBootstrapper(
                        resolver.Resolve<IMonetizationManager>(),
                        resolver.Resolve<
                            IDemoExperienceController>(),
                        resolver.Resolve<IWorkshopHomeController>()),
                Lifetime.Singleton);
        }

        private static void RegisterMonetization(
            IContainerBuilder builder)
        {
            builder
                .Register<NoOpRewardedAdProvider>(
                    _ => new NoOpRewardedAdProvider(),
                    Lifetime.Singleton)
                .As<IRewardedAdProvider>();
            builder
                .Register<NoOpRelaxPassEntitlementProvider>(
                    _ => new NoOpRelaxPassEntitlementProvider(),
                    Lifetime.Singleton)
                .As<IRelaxPassEntitlementProvider>();

            var developmentKeyPath = Path.Combine(
                Application.persistentDataPath,
                DevelopmentKeyFileName);
            builder.Register<IAuthenticatedDataProtector>(
                _ =>
                    PlatformAuthenticatedDataProtectorFactory.Create(
                        Application.platform,
                        KeystoreAlias,
                        developmentKeyPath),
                Lifetime.Singleton);

            var statePath = Path.Combine(
                Application.persistentDataPath,
                StateFileName);
            builder.Register<IMonetizationStateStore>(
                resolver =>
                    new EncryptedFileMonetizationStateStore(
                        statePath,
                        resolver.Resolve<
                            IAuthenticatedDataProtector>(),
                        StateAssociatedData),
                Lifetime.Singleton);

            builder.RegisterInstance(
                new MonetizationOptions(
                    allowRewardedForRelaxPassOwners: false));
            builder
                .Register<MonetizationManager>(
                    resolver =>
                        new MonetizationManager(
                            resolver.Resolve<IRewardedAdProvider>(),
                            resolver.Resolve<
                                IRelaxPassEntitlementProvider>(),
                            resolver.Resolve<IMonetizationStateStore>(),
                            resolver.Resolve<
                                IPresentationActivityCoordinator>(),
                            resolver.Resolve<MonetizationOptions>()),
                    Lifetime.Singleton)
                .As<IMonetizationManager>();
        }
    }
}
