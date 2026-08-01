using System;
using CalmSpace.Core;
using CalmSpace.Haptics;
using NUnit.Framework;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class HapticServiceFactoryTests
    {
        [TestCase(
            RuntimePlatform.Android,
            HapticPlatformKind.Android)]
        [TestCase(
            RuntimePlatform.IPhonePlayer,
            HapticPlatformKind.Ios)]
        [TestCase(
            RuntimePlatform.OSXEditor,
            HapticPlatformKind.Unsupported)]
        [TestCase(
            RuntimePlatform.WindowsEditor,
            HapticPlatformKind.Unsupported)]
        [TestCase(
            RuntimePlatform.LinuxEditor,
            HapticPlatformKind.Unsupported)]
        [TestCase(
            RuntimePlatform.WebGLPlayer,
            HapticPlatformKind.Unsupported)]
        public void ResolvePlatformKindUsesExplicitPlatform(
            RuntimePlatform platform,
            HapticPlatformKind expected)
        {
            Assert.That(
                PlatformHapticServiceFactory.ResolvePlatformKind(platform),
                Is.EqualTo(expected));
        }

        [Test]
        public void CurrentEditorPlatformResolvesToUnsupported()
        {
            Assert.That(Application.isEditor, Is.True);
            Assert.That(
                PlatformHapticServiceFactory.ResolvePlatformKind(
                    Application.platform),
                Is.EqualTo(HapticPlatformKind.Unsupported));
        }

        [Test]
        public void FactoryCreatesServiceForExplicitPlayerPlatform()
        {
            var factory = new PlatformHapticServiceFactory();
            var clock = new FixedClock();
            IHapticService android =
                factory.Create(RuntimePlatform.Android, clock);
            IHapticService ios =
                factory.Create(RuntimePlatform.IPhonePlayer, clock);

            try
            {
                Assert.That(android, Is.TypeOf<AndroidHapticManager>());
                Assert.That(ios, Is.TypeOf<IosHapticManager>());
            }
            finally
            {
                (android as IDisposable)?.Dispose();
                (ios as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void FactoryUsesSharedNullServiceForEditor()
        {
            var factory = new PlatformHapticServiceFactory();

            IHapticService service =
                factory.Create(Application.platform, new FixedClock());

            Assert.That(service, Is.SameAs(NullHapticService.Instance));
            Assert.That(service.IsSupported, Is.False);
        }

        [Test]
        public void FactoryRejectsMissingClock()
        {
            var factory = new PlatformHapticServiceFactory();

            Assert.Throws<ArgumentNullException>(
                () => factory.Create(RuntimePlatform.Android, null));
        }

        private sealed class FixedClock : IMonotonicClock
        {
            public double NowSeconds => 1d;
        }
    }
}
