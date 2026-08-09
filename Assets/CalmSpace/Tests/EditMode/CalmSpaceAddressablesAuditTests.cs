using System.Collections.Generic;
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
