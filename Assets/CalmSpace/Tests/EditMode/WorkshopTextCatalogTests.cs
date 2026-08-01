using System;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Demo;
using CalmSpace.Editor;
using CalmSpace.Workshop;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class WorkshopTextCatalogTests
    {
        private const string CatalogPath =
            "Assets/CalmSpace/Config/WorkshopTextCatalog.asset";

        private static readonly string[] ExpectedKeys =
        {
            "chapter.cozy-workshop.title",
            "memory.summer-trail.title",
            "memory.summer-trail.body",
            "memory.fix-everything.title",
            "memory.fix-everything.body",
            "memory.open-windows.title",
            "memory.open-windows.body",
            "postcard.family-tea.title",
            "postcard.family-tea.body",
            "finale.mom.line-1",
            "finale.mom.line-2",
            "teaser.kitchen",
            "beat.clear-passage.title",
            "beat.clear-passage.result",
            "beat.pebble-shelf.title",
            "beat.pebble-shelf.result",
            "beat.tea-drawer.title",
            "beat.tea-drawer.result",
            "beat.paint-shelf.title",
            "beat.paint-shelf.result",
            "beat.fastener-tray.title",
            "beat.fastener-tray.result",
            "beat.warm-workbench.title",
            "beat.warm-workbench.result",
            "beat.cabinet-hinge.title",
            "beat.cabinet-hinge.result",
            "beat.open-window.title",
            "beat.open-window.result",
            "home.start",
            "home.catalog",
            "home.album",
            "home.decor",
            "home.settings",
            "home.daily-care",
            "home.view-workshop",
            "home.chapter-complete",
            "save.retry",
            "save.return-without",
            "album.title",
            "album.locked",
            "common.skip",
            "common.close",
            "rewarded.unlock",
            "rewarded.unavailable",
            "rewarded.closed",
            "decor.slot.workbench",
            "decor.slot.light",
            "decor.soft-fern",
            "decor.river-stones",
            "decor.clay-vase",
            "decor.moon-glaze-vase",
            "decor.linen-shade",
            "decor.warm-lantern",
            "decor.paper-light",
            "decor.amber-lamp",
            "daily.title",
            "daily.instruction",
            "daily.complete",
            "relax-pass.title"
        };

        [Test]
        public void GeneratedCatalogContainsEveryWorkshopKeyInEveryLocale()
        {
            WorkshopTextCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorkshopTextCatalog>(
                    CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.Entries.Count,
                Is.EqualTo(ExpectedKeys.Length));

            var actualKeys = new HashSet<string>();
            foreach (WorkshopTextEntry entry in catalog.Entries)
            {
                Assert.That(entry.Key, Is.Not.Empty);
                Assert.That(actualKeys.Add(entry.Key), Is.True);
                Assert.That(entry.English, Is.Not.Empty);
                Assert.That(entry.Ukrainian, Is.Not.Empty);
                Assert.That(entry.Russian, Is.Not.Empty);
            }

            CollectionAssert.AreEquivalent(ExpectedKeys, actualKeys);
            Assert.That(
                catalog.Get("chapter.cozy-workshop.title", DemoLocale.Russian),
                Is.EqualTo("Старая семейная мастерская"));
            Assert.That(
                catalog.Get("memory.fix-everything.body", DemoLocale.Russian),
                Is.EqualTo(
                    "Дважды отмерь. Не спеши. Оставь вещь лучше, чем она была."));
            Assert.That(
                catalog.Get("daily.instruction", DemoLocale.Ukrainian),
                Is.EqualTo(
                    "Додай чай, обережно налий воду й зроби три спокійні кола ложкою."));
        }

        [Test]
        public void ServiceUpdatesWhenTheSharedLocaleChangesAndFallsBackToEnglish()
        {
            var playerPrefsKey = "calmspace.tests.locale." + Guid.NewGuid();
            var localization = new DemoLocalizationService(
                playerPrefsKey,
                SystemLanguage.English);
            WorkshopTextCatalog catalog = CreateCatalog(
                new WorkshopTextEntry(
                    "present",
                    "English value",
                    "Українське значення",
                    string.Empty));
            try
            {
                localization.Initialize();
                var service = new WorkshopTextService(catalog, localization);
                var notifications = 0;
                service.LocaleChanged += () => notifications++;

                Assert.That(service.Get("present"), Is.EqualTo("English value"));
                localization.SelectLocale(DemoLocale.Ukrainian);
                Assert.That(
                    service.Get("present"),
                    Is.EqualTo("Українське значення"));
                localization.SelectLocale(DemoLocale.Russian);
                Assert.That(service.Get("present"), Is.EqualTo("English value"));
                Assert.That(notifications, Is.EqualTo(2));
                Assert.That(service.Get("missing"), Is.EqualTo("missing"));
                service.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                PlayerPrefs.DeleteKey(playerPrefsKey);
            }
        }

        [Test]
        public void BuildValidatorRejectsDuplicateAndBlankTranslations()
        {
            WorkshopTextCatalog duplicate = CreateCatalog(
                new WorkshopTextEntry("duplicate", "One", "Один", "Один"),
                new WorkshopTextEntry("duplicate", "Two", "Два", "Два"));
            WorkshopTextCatalog blank = CreateCatalog(
                new WorkshopTextEntry("blank", "English", "", "Русский"));
            try
            {
                Assert.Throws<BuildFailedException>(
                    () => WorkshopTextBuildValidator.ValidateOrThrow(duplicate));
                Assert.Throws<BuildFailedException>(
                    () => WorkshopTextBuildValidator.ValidateOrThrow(blank));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
                UnityEngine.Object.DestroyImmediate(blank);
            }
        }

        private static WorkshopTextCatalog CreateCatalog(
            params WorkshopTextEntry[] entries)
        {
            WorkshopTextCatalog catalog =
                ScriptableObject.CreateInstance<WorkshopTextCatalog>();
            FieldInfo field = typeof(WorkshopTextCatalog).GetField(
                "_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(catalog, entries);
            return catalog;
        }
    }
}
