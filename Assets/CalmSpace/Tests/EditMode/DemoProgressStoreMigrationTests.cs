using System;
using CalmSpace.Demo;
using NUnit.Framework;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class DemoProgressStoreMigrationTests
    {
        [Test]
        public void VersionOneSaveReceivesRetroactiveRewardsExactlyOnce()
        {
            string key = "calmspace.tests.progress." + Guid.NewGuid();
            try
            {
                PlayerPrefs.SetString(
                    key,
                    "{\"version\":1," +
                    "\"highestUnlockedLevelIndex\":4," +
                    "\"completedLevelMask\":13," +
                    "\"selectedThemeId\":\"ocean\"," +
                    "\"musicEnabled\":true}");

                var first = new PlayerPrefsDemoProgressStore(key);
                first.Initialize(8, "sage", 10);

                Assert.That(first.Current.CalmPoints, Is.EqualTo(30));
                Assert.That(
                    first.Current.RewardedLevelMask,
                    Is.EqualTo(13));
                Assert.That(
                    first.Current.OwnedDecorationMask,
                    Is.EqualTo(1));
                Assert.That(
                    first.Current.SelectedDecorationIndex,
                    Is.Zero);

                var reloaded = new PlayerPrefsDemoProgressStore(key);
                reloaded.Initialize(8, "sage", 10);

                Assert.That(reloaded.Current, Is.EqualTo(first.Current));
                Assert.That(reloaded.Current.CalmPoints, Is.EqualTo(30));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
