using System.Collections.Generic;
using CalmSpace.Demo;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class DemoDecorationCatalogTests
    {
        [Test]
        public void BuiltInCatalogHasFourAffordableUniqueDecorations()
        {
            DemoDecorationDefinition[] definitions =
                DemoDecorationCatalog.CreateBuiltInDefinitions();

            Assert.That(definitions, Has.Length.EqualTo(4));
            Assert.That(
                definitions[0].Cost,
                Is.Zero,
                "The starter room must always have one owned decoration.");

            var ids = new HashSet<string>();
            var previousCost = -1;
            for (var index = 0; index < definitions.Length; index++)
            {
                DemoDecorationDefinition definition = definitions[index];
                Assert.That(definition.Id, Is.Not.Empty);
                Assert.That(definition.DisplayName, Is.Not.Empty);
                Assert.That(ids.Add(definition.Id), Is.True);
                Assert.That(
                    definition.Cost,
                    Is.GreaterThanOrEqualTo(previousCost));
                previousCost = definition.Cost;
            }

            var catalog =
                UnityEngine.ScriptableObject.CreateInstance<
                    DemoDecorationCatalog>();
            try
            {
                Assert.That(catalog.CompletionReward, Is.GreaterThan(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }
}
