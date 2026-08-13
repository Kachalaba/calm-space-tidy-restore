using System;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Demo;
using CalmSpace.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CalmSpace.Tests.EditMode
{
    public sealed class RestorationProgressRulesTests
    {
        private readonly List<LevelDefinition> _created =
            new List<LevelDefinition>();

        private readonly List<LevelCatalog> _createdCatalogs =
            new List<LevelCatalog>();

        [TearDown]
        public void TearDown()
        {
            for (var index = 0;
                 index < _createdCatalogs.Count;
                 index++)
            {
                if (_createdCatalogs[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        _createdCatalogs[index]);
                }
            }

            _createdCatalogs.Clear();

            for (var index = 0; index < _created.Count; index++)
            {
                if (_created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        _created[index]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AuthoredStagesExposeChapterAndContinuation()
        {
            LevelDefinition first = CreateDefinition(
                "first",
                "cozy-workshop",
                0,
                2);
            LevelDefinition final = CreateDefinition(
                "final",
                "cozy-workshop",
                1,
                2);

            Assert.That(
                first.TryGetRestorationStage(out var firstStage),
                Is.True);
            Assert.That(firstStage.StageNumber, Is.EqualTo(1));
            Assert.That(firstStage.IsFirstStage, Is.True);
            Assert.That(firstStage.IsFinalStage, Is.False);
            Assert.That(
                final.TryGetRestorationStage(out var finalStage),
                Is.True);
            Assert.That(finalStage.IsFinalStage, Is.True);
            Assert.That(
                RestorationProgressRules.IsContinuation(
                    first,
                    final),
                Is.True);

            LogAssert.Expect(
                LogType.Error,
                "Level 'malformed' has invalid restoration metadata. " +
                "Assign a chapter id and a zero-based stage index within " +
                "the authored stage count.");
            LevelDefinition malformedStandalone =
                CreateDefinition(
                    "malformed",
                    string.Empty,
                    -1,
                    0);
            Assert.That(
                RestorationProgressRules.IsCatalogSequenceValid(
                    new[] { malformedStandalone },
                    out var invalidIndex),
                Is.False);
            Assert.That(invalidIndex, Is.EqualTo(0));
        }

        [Test]
        public void CatalogValidationAcceptsCompleteContiguousChapters()
        {
            var definitions = new[]
            {
                CreateDefinition(
                    "a-1",
                    "chapter-a",
                    0,
                    2),
                CreateDefinition(
                    "a-2",
                    "chapter-a",
                    1,
                    2),
                CreateDefinition(
                    "standalone",
                    string.Empty,
                    0,
                    0),
                CreateDefinition(
                    "b-1",
                    "chapter-b",
                    0,
                    2),
                CreateDefinition(
                    "b-2",
                    "chapter-b",
                    1,
                    2)
            };

            Assert.That(
                RestorationProgressRules.IsCatalogSequenceValid(
                    definitions,
                    out var invalidIndex),
                Is.True);
            Assert.That(invalidIndex, Is.EqualTo(-1));
        }

        [Test]
        public void CatalogValidationRejectsMissingOrDriftingStage()
        {
            var definitions = new[]
            {
                CreateDefinition(
                    "a-1",
                    "chapter-a",
                    0,
                    3),
                CreateDefinition(
                    "a-2",
                    "chapter-a",
                    1,
                    2),
                CreateDefinition(
                    "a-3",
                    "chapter-a",
                    2,
                    3)
            };

            Assert.That(
                RestorationProgressRules.IsCatalogSequenceValid(
                    definitions,
                    out var invalidIndex),
                Is.False);
            Assert.That(invalidIndex, Is.EqualTo(1));
        }

        [Test]
        public void CatalogValidationRejectsRepeatedChapterId()
        {
            var definitions = new[]
            {
                CreateDefinition(
                    "a-1",
                    "chapter-a",
                    0,
                    1),
                CreateDefinition(
                    "standalone",
                    string.Empty,
                    0,
                    0),
                CreateDefinition(
                    "a-again",
                    "chapter-a",
                    0,
                    1)
            };

            Assert.That(
                RestorationProgressRules.IsCatalogSequenceValid(
                    definitions,
                    out var invalidIndex),
                Is.False);
            Assert.That(invalidIndex, Is.EqualTo(2));
        }

        [Test]
        public void ChapterValidationKeepsValidChapterWhenAnotherHasGap()
        {
            var definitions = new[]
            {
                CreateDefinition(
                    "a-1",
                    "chapter-a",
                    0,
                    2),
                CreateDefinition(
                    "a-2",
                    "chapter-a",
                    1,
                    2),
                CreateDefinition(
                    "b-1",
                    "chapter-b",
                    0,
                    3),
                CreateDefinition(
                    "b-3",
                    "chapter-b",
                    2,
                    3)
            };

            Assert.That(
                RestorationProgressRules.TryValidateChapter(
                    definitions,
                    "chapter-a",
                    out var chapter,
                    out var invalidIndex),
                Is.True);
            Assert.That(chapter.ChapterId, Is.EqualTo("chapter-a"));
            Assert.That(chapter.FirstCatalogIndex, Is.Zero);
            Assert.That(chapter.StageCount, Is.EqualTo(2));
            Assert.That(invalidIndex, Is.EqualTo(-1));

            Assert.That(
                RestorationProgressRules.TryValidateChapter(
                    definitions,
                    "chapter-b",
                    out _,
                    out invalidIndex),
                Is.False);
            Assert.That(invalidIndex, Is.EqualTo(3));
        }

        [Test]
        public void CatalogFailsClosedOnlyForInvalidChapter()
        {
            LevelCatalog catalog = CreateCatalog(
                CreateDefinition(
                    "a-1",
                    "chapter-a",
                    0,
                    2),
                CreateDefinition(
                    "a-2",
                    "chapter-a",
                    1,
                    2),
                CreateDefinition(
                    "b-1",
                    "chapter-b",
                    0,
                    3),
                CreateDefinition(
                    "b-3",
                    "chapter-b",
                    2,
                    3));

            Assert.That(
                catalog.IsRestorationChapterValid("chapter-a"),
                Is.True);
            Assert.That(
                catalog.TryGetRestorationChapter(
                    "chapter-a",
                    out var chapter),
                Is.True);
            Assert.That(chapter.FirstCatalogIndex, Is.Zero);
            Assert.That(chapter.StageCount, Is.EqualTo(2));

            Assert.That(
                catalog.IsRestorationChapterValid("chapter-b"),
                Is.False);
            Assert.That(
                catalog.TryGetRestorationChapter(
                    "chapter-b",
                    out _),
                Is.False);
        }

        [Test]
        public void ChapterValidationRejectsDuplicateStage()
        {
            var definitions = new[]
            {
                CreateDefinition(
                    "a-1",
                    "chapter-a",
                    0,
                    3),
                CreateDefinition(
                    "a-2",
                    "chapter-a",
                    1,
                    3),
                CreateDefinition(
                    "a-duplicate",
                    "chapter-a",
                    1,
                    3)
            };

            AssertChapterIsInvalid(
                definitions,
                expectedInvalidIndex: 2);
        }

        [Test]
        public void ChapterValidationRejectsMissingStage()
        {
            var definitions = new[]
            {
                CreateDefinition(
                    "a-1",
                    "chapter-a",
                    0,
                    3),
                CreateDefinition(
                    "a-3",
                    "chapter-a",
                    2,
                    3)
            };

            AssertChapterIsInvalid(
                definitions,
                expectedInvalidIndex: 1);
        }

        [Test]
        public void ChapterValidationRejectsTruncatedFinalStage()
        {
            var definitions = new[]
            {
                CreateDefinition(
                    "a-1",
                    "chapter-a",
                    0,
                    2)
            };

            AssertChapterIsInvalid(
                definitions,
                expectedInvalidIndex: 0);
        }

        [Test]
        public void ChapterValidationRejectsMismatchedStageCount()
        {
            var definitions = new[]
            {
                CreateDefinition(
                    "a-1",
                    "chapter-a",
                    0,
                    2),
                CreateDefinition(
                    "a-2",
                    "chapter-a",
                    1,
                    3)
            };

            AssertChapterIsInvalid(
                definitions,
                expectedInvalidIndex: 1);
        }

        [Test]
        public void ChapterValidationRejectsDuplicateLevelId()
        {
            var definitions = new[]
            {
                CreateDefinition(
                    "duplicate",
                    "chapter-a",
                    0,
                    2),
                CreateDefinition(
                    "duplicate",
                    "chapter-a",
                    1,
                    2)
            };

            AssertChapterIsInvalid(
                definitions,
                expectedInvalidIndex: 1);
        }

        [Test]
        public void UkrainianCopyPresentsChapterStageAndCompletion()
        {
            string key =
                "calmspace.tests.restoration.locale." +
                Guid.NewGuid();
            LevelDefinition definition = CreateDefinition(
                "tea-stage",
                "cozy-workshop",
                1,
                3);
            try
            {
                var service = new DemoLocalizationService(
                    key,
                    SystemLanguage.Ukrainian);
                service.Initialize();

                Assert.That(
                    service.FormatRestorationStageTitle(
                        definition),
                    Is.EqualTo(
                        "Затишна майстерня\n" +
                        "2/3 · tea-stage"));
                Assert.That(
                    service.FormatCompletionBody(definition),
                    Is.EqualTo(
                        "Затишна майстерня · " +
                        "етап 2 з 3 " +
                        "завершено."));
                Assert.That(
                    service.FormatRestorationStageTitle(
                        definition,
                        includeRestorationContext: false),
                    Is.EqualTo("tea-stage"));
                Assert.That(
                    service.FormatCompletionBody(
                        definition,
                        includeRestorationContext: false),
                    Is.EqualTo(
                        "«tea-stage» — знову в гармонії."));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        private static void AssertChapterIsInvalid(
            IReadOnlyList<LevelDefinition> definitions,
            int expectedInvalidIndex)
        {
            Assert.That(
                RestorationProgressRules.TryValidateChapter(
                    definitions,
                    "chapter-a",
                    out _,
                    out var invalidIndex),
                Is.False);
            Assert.That(
                invalidIndex,
                Is.EqualTo(expectedInvalidIndex));
        }

        private LevelCatalog CreateCatalog(
            params LevelDefinition[] definitions)
        {
            const BindingFlags Flags =
                BindingFlags.Instance |
                BindingFlags.NonPublic;

            FieldInfo definitionField =
                typeof(LevelCatalogEntry).GetField(
                    "_definition",
                    Flags);
            FieldInfo levelsField =
                typeof(LevelCatalog).GetField(
                    "_levels",
                    Flags);
            Assert.That(definitionField, Is.Not.Null);
            Assert.That(levelsField, Is.Not.Null);

            var entries =
                new LevelCatalogEntry[definitions.Length];
            for (var index = 0;
                 index < definitions.Length;
                 index++)
            {
                entries[index] = new LevelCatalogEntry();
                definitionField.SetValue(
                    entries[index],
                    definitions[index]);
            }

            LevelCatalog catalog =
                ScriptableObject.CreateInstance<LevelCatalog>();
            levelsField.SetValue(catalog, entries);
            _createdCatalogs.Add(catalog);
            return catalog;
        }

        private LevelDefinition CreateDefinition(
            string levelId,
            string chapterId,
            int stageIndex,
            int stageCount)
        {
            LevelDefinition definition =
                ScriptableObject.CreateInstance<LevelDefinition>();
            definition.name = levelId;
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_levelId").stringValue =
                levelId;
            serialized.FindProperty("_displayName").stringValue =
                levelId;
            serialized.FindProperty("_levelType").enumValueIndex =
                (int)LevelType.Fitting;
            serialized.FindProperty(
                "_restorationChapterId").stringValue =
                chapterId;
            serialized.FindProperty(
                "_restorationStageIndex").intValue =
                stageIndex;
            serialized.FindProperty(
                "_restorationStageCount").intValue =
                stageCount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _created.Add(definition);
            return definition;
        }
    }
}
