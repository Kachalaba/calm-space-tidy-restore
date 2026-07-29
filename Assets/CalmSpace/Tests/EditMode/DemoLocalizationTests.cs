using System;
using CalmSpace.Cleaning;
using CalmSpace.Demo;
using NUnit.Framework;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class DemoLocalizationTests
    {
        [Test]
        public void EveryPlayerFacingKeyExistsInBothLocales()
        {
            foreach (DemoTextKey key in
                     Enum.GetValues(typeof(DemoTextKey)))
            {
                Assert.That(
                    DemoLocalizationTable.Get(
                        DemoLocale.English,
                        key),
                    Is.Not.Empty,
                    "Missing English copy for " + key);
                Assert.That(
                    DemoLocalizationTable.Get(
                        DemoLocale.Ukrainian,
                        key),
                    Is.Not.Empty,
                    "Missing Ukrainian copy for " + key);
            }
        }

        [Test]
        public void UkrainianCatalogLocalizesStableContentIds()
        {
            Assert.That(
                DemoLocalizationTable.GetLevelName(
                    DemoLocale.Ukrainian,
                    "05-fastener-tray",
                    "Fastener Tray"),
                Is.EqualTo("Таця з кріпленнями"));
            Assert.That(
                DemoLocalizationTable.GetThemeName(
                    DemoLocale.Ukrainian,
                    "ocean",
                    "Blue Hour"),
                Is.EqualTo("Синя година"));
            Assert.That(
                DemoLocalizationTable.GetLevelName(
                    DemoLocale.Ukrainian,
                    "future-level",
                    "Future Level"),
                Is.EqualTo("Future Level"));
        }

        [Test]
        public void UkrainianCatalogLocalizesRoomDecor()
        {
            Assert.That(
                DemoLocalizationTable.GetDecorationName(
                    DemoLocale.Ukrainian,
                    "warm-lantern",
                    "Warm Lantern"),
                Is.EqualTo("Теплий ліхтар"));
            Assert.That(
                DemoLocalizationTable.GetDecorationName(
                    DemoLocale.Ukrainian,
                    "future-decoration",
                    "Future Decoration"),
                Is.EqualTo("Future Decoration"));
        }

        [Test]
        public void UkrainianStageProgressUsesWaterSqueegeeTerm()
        {
            var key =
                "calmspace.tests.locale." + Guid.NewGuid();
            try
            {
                var service = new DemoLocalizationService(
                    key,
                    SystemLanguage.Ukrainian);
                service.Initialize();

                Assert.That(
                    service.FormatStageProgress(
                        CleaningToolKind.Squeegee,
                        2,
                        3,
                        40),
                    Is.EqualTo(
                        "Водозгін · крок 2 з 3 · 40%"));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void NewProfileUsesUkrainianDeviceLanguage()
        {
            var key =
                "calmspace.tests.locale." + Guid.NewGuid();
            try
            {
                var service = new DemoLocalizationService(
                    key,
                    SystemLanguage.Ukrainian);

                service.Initialize();

                Assert.That(
                    service.CurrentLocale,
                    Is.EqualTo(DemoLocale.Ukrainian));
                Assert.That(
                    service.FormatHomeProgress(2, 6),
                    Is.EqualTo("Відновлено: 2 із 6"));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void ExplicitLocaleSelectionPersistsAcrossServices()
        {
            var key =
                "calmspace.tests.locale." + Guid.NewGuid();
            try
            {
                var first = new DemoLocalizationService(
                    key,
                    SystemLanguage.English);
                first.Initialize();
                Assert.That(
                    first.SelectLocale(DemoLocale.Ukrainian),
                    Is.True);

                var restored = new DemoLocalizationService(
                    key,
                    SystemLanguage.English);
                restored.Initialize();

                Assert.That(
                    restored.CurrentLocale,
                    Is.EqualTo(DemoLocale.Ukrainian));
                Assert.That(
                    restored.Get(DemoTextKey.PlayButton),
                    Is.EqualTo("Почати відновлення"));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
