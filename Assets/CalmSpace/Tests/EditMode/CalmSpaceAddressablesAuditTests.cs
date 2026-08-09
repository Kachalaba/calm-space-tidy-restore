using System;
using System.Collections.Generic;
using System.IO;
using CalmSpace.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;

namespace CalmSpace.Tests.EditMode
{
    public sealed class CalmSpaceAddressablesAuditTests
    {
        [Test]
        public void NoIssuesResultClassifiesAsClean()
        {
            CalmSpaceAddressablesAudit.AnalyzeClassification result =
                CalmSpaceAddressablesAudit.ClassifyAnalyzeResults(
                    new List<AnalyzeRule.AnalyzeResult>
                    {
                        Result("No issues found", MessageType.None)
                    });

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Duplicates, Is.Empty);
        }

        [Test]
        public void DuplicateResultsAreParsedDeduplicatedAndSorted()
        {
            CalmSpaceAddressablesAudit.AnalyzeClassification result =
                CalmSpaceAddressablesAudit.ClassifyAnalyzeResults(
                    new List<AnalyzeRule.AnalyzeResult>
                    {
                        Result(
                            "Workshop Local:workshop_bundle:" +
                            "Assets/CalmSpace/UI/Workshop/Art/Room/z.png",
                            MessageType.Warning),
                        Result(
                            "Default Local Group:levels_bundle:" +
                            "Assets/CalmSpace/Shared/a.mat",
                            MessageType.Warning),
                        Result(
                            "Workshop Local:workshop_bundle:" +
                            "Assets/CalmSpace/UI/Workshop/Art/Room/z.png",
                            MessageType.Warning)
                    });

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Duplicates.Count, Is.EqualTo(2));
            Assert.That(
                result.Duplicates[0].Group,
                Is.EqualTo("Default Local Group"));
            Assert.That(
                result.Duplicates[0].AssetPath,
                Is.EqualTo("Assets/CalmSpace/Shared/a.mat"));
            Assert.That(
                result.Duplicates[1].Group,
                Is.EqualTo("Workshop Local"));
        }

        [Test]
        public void DuplicateDisplayOrientationsNormalizeToOneFinding()
        {
            const string assetPath =
                "Assets/CalmSpace/UI/Workshop/Art/Room/shared.png";
            CalmSpaceAddressablesAudit.AnalyzeClassification result =
                CalmSpaceAddressablesAudit.ClassifyAnalyzeResults(
                    new List<AnalyzeRule.AnalyzeResult>
                    {
                        Result(
                            "Workshop Local:workshop_bundle:" + assetPath,
                            MessageType.Warning),
                        Result(
                            assetPath +
                            ":Workshop Local:workshop_bundle",
                            MessageType.Warning)
                    });

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Duplicates.Count, Is.EqualTo(1));
            Assert.That(result.Duplicates[0].Group, Is.EqualTo("Workshop Local"));
            Assert.That(result.Duplicates[0].Bundle, Is.EqualTo("workshop_bundle"));
            Assert.That(result.Duplicates[0].AssetPath, Is.EqualTo(assetPath));
        }

        [TestCase(
            "Assets/CalmSpace/first.asset:Group:" +
            "Assets/CalmSpace/second.asset")]
        [TestCase("/Users/private.asset:Group:bundle")]
        [TestCase("Group:bundle:/Users/private.asset")]
        [TestCase(
            "/Users/private-group:bundle:" +
            "Assets/CalmSpace/valid.asset")]
        [TestCase(
            "Assets/CalmSpace/valid.asset:Group:" +
            "/Users/private-bundle")]
        public void AmbiguousAndAbsoluteDisplayShapesFailClosed(
            string resultName)
        {
            CalmSpaceAddressablesAudit.AnalyzeClassification result =
                CalmSpaceAddressablesAudit.ClassifyAnalyzeResults(
                    new List<AnalyzeRule.AnalyzeResult>
                    {
                        Result(resultName, MessageType.Warning)
                    });

            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Duplicates, Is.Empty);
        }

        [TestCase(
            "Check Duplicate Bundle DependenciesAnalyze build failed. Error")]
        [TestCase(
            "Check Duplicate Bundle DependenciesCannot run Analyze with " +
            "unsaved scenes")]
        public void AnalyzeExecutionFailuresFailClosed(string resultName)
        {
            CalmSpaceAddressablesAudit.AnalyzeClassification result =
                CalmSpaceAddressablesAudit.ClassifyAnalyzeResults(
                    new List<AnalyzeRule.AnalyzeResult>
                    {
                        Result(resultName, MessageType.Error)
                    });

            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Duplicates, Is.Empty);
        }

        [TestCase("Malformed duplicate result")]
        [TestCase("Group:bundle:/Users/someone/private.asset")]
        public void UnsafeOrMalformedResultsFailClosed(string resultName)
        {
            CalmSpaceAddressablesAudit.AnalyzeClassification result =
                CalmSpaceAddressablesAudit.ClassifyAnalyzeResults(
                    new List<AnalyzeRule.AnalyzeResult>
                    {
                        Result(resultName, MessageType.Warning)
                    });

            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Duplicates, Is.Empty);
        }

        [Test]
        public void ErrorSeverityFailsClosedEvenForDuplicateShapedResult()
        {
            CalmSpaceAddressablesAudit.AnalyzeClassification result =
                CalmSpaceAddressablesAudit.ClassifyAnalyzeResults(
                    new List<AnalyzeRule.AnalyzeResult>
                    {
                        Result(
                            "Group:bundle:Assets/CalmSpace/Shared/a.mat",
                            MessageType.Error)
                    });

            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Duplicates, Is.Empty);
        }

        [Test]
        public void FailedAnalyzeCannotClaimDuplicationIsClear()
        {
            CalmSpaceAddressablesAudit.AnalyzeClassification classification =
                CalmSpaceAddressablesAudit.ClassifyAnalyzeResults(
                    new List<AnalyzeRule.AnalyzeResult>
                    {
                        Result("Analyze build failed", MessageType.Error)
                    });

            Assert.That(classification.Errors, Is.Not.Empty);
            Assert.That(
                CalmSpaceAddressablesAudit.GetAddressablesDuplicationStatus(
                    classification),
                Is.EqualTo("notEvaluated"));
        }

        [Test]
        public void FailedBuildCannotFallBackToAStaleBuildLayout()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "CalmSpaceAddressablesAuditTests-" +
                Guid.NewGuid().ToString("N"));
            string reportDirectory = Path.Combine(
                root,
                "Library",
                "com.unity.addressables");
            string staleLayout = Path.Combine(
                reportDirectory,
                "buildlayout.json");
            string sibling = Path.Combine(reportDirectory, "keep.json");
            try
            {
                Directory.CreateDirectory(reportDirectory);
                File.WriteAllText(staleLayout, "stale");
                File.WriteAllText(sibling, "keep");

                CalmSpaceAddressablesAudit.RemovePriorBuildLayout(
                    staleLayout);
                // Simulate the current build returning without an artifact.

                Assert.That(File.Exists(staleLayout), Is.False);
                Assert.That(
                    File.Exists(sibling),
                    Is.True,
                    "Cleanup must delete only the exact legacy layout file.");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static AnalyzeRule.AnalyzeResult Result(
            string name,
            MessageType severity)
        {
            return new AnalyzeRule.AnalyzeResult
            {
                resultName = name,
                severity = severity
            };
        }
    }
}
