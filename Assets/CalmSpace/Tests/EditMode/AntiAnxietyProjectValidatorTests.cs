using System;
using System.Collections.Generic;
using CalmSpace.Core;
using CalmSpace.Editor;
using CalmSpace.Levels;
using CalmSpace.Monetization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class AntiAnxietyProjectValidatorTests
    {
        [Test]
        public void CurrentProjectSatisfiesAntiAnxietyContracts()
        {
            IReadOnlyList<AntiAnxietyValidationIssue> issues =
                AntiAnxietyProjectValidator.ValidateProject();

            Assert.That(
                issues,
                Is.Empty,
                AntiAnxietyProjectValidator.FormatIssues(issues));
        }

        [Test]
        public void MonetizationPolicyRejectsForcedAndGenericShowApis()
        {
            IReadOnlyList<AntiAnxietyValidationIssue> issues =
                AntiAnxietyProjectValidator.ValidateMonetizationApi(
                    new[]
                    {
                        typeof(UnsafeInterstitialApi),
                        typeof(UnsafeGenericAdApi)
                    });

            Assert.That(
                HasCode(
                    issues,
                    AntiAnxietyProjectValidator.ForcedAdApiCode),
                Is.True);
            Assert.That(
                HasCode(
                    issues,
                    AntiAnxietyProjectValidator
                        .NonRewardedShowApiCode),
                Is.True);
        }

        [Test]
        public void MonetizationPolicyAcceptsExplicitRewardedPresentation()
        {
            IReadOnlyList<AntiAnxietyValidationIssue> issues =
                AntiAnxietyProjectValidator.ValidateMonetizationApi(
                    new[] { typeof(SafeRewardedApi) });

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void LevelPolicyRejectsSerializedCountdownContract()
        {
            IReadOnlyList<AntiAnxietyValidationIssue> issues =
                AntiAnxietyProjectValidator.ValidateLevelContracts(
                    new[] { typeof(UnsafeAuthoredLevel) });

            Assert.That(
                HasCode(
                    issues,
                    AntiAnxietyProjectValidator
                        .NegativeLevelContractCode),
                Is.True);
        }

        [Test]
        public void LevelPolicyRejectsPublicFailContract()
        {
            IReadOnlyList<AntiAnxietyValidationIssue> issues =
                AntiAnxietyProjectValidator.ValidateLevelContracts(
                    new[] { typeof(UnsafeLifecycleLevel) });

            Assert.That(
                HasCode(
                    issues,
                    AntiAnxietyProjectValidator
                        .NegativeLevelContractCode),
                Is.True);
        }

        [Test]
        public void UndoPolicyRejectsControllerWithoutUndoIntegration()
        {
            IReadOnlyList<AntiAnxietyValidationIssue> issues =
                AntiAnxietyProjectValidator.ValidateUndoContracts(
                    typeof(ControllerWithoutUndo));

            Assert.That(
                HasCode(
                    issues,
                    AntiAnxietyProjectValidator
                        .MissingUndoContractCode),
                Is.True);
        }

        [Test]
        public void UndoPolicyAcceptsSharedHistoryInjection()
        {
            IReadOnlyList<AntiAnxietyValidationIssue> issues =
                AntiAnxietyProjectValidator.ValidateUndoContracts(
                    typeof(ControllerWithUndoHistory));

            Assert.That(issues, Is.Empty);
        }

        private static bool HasCode(
            IReadOnlyList<AntiAnxietyValidationIssue> issues,
            string code)
        {
            for (var index = 0; index < issues.Count; index++)
            {
                if (string.Equals(
                        issues[index].Code,
                        code,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class UnsafeInterstitialApi
        {
            public void ShowInterstitial()
            {
            }
        }

        private sealed class UnsafeGenericAdApi
        {
            public UniTask ShowAdAsync()
            {
                return UniTask.CompletedTask;
            }
        }

        private sealed class SafeRewardedApi
        {
            public UniTask ShowAsync(RewardedAdRequest request)
            {
                return UniTask.CompletedTask;
            }
        }

        private sealed class UnsafeAuthoredLevel : LevelBase
        {
            [SerializeField]
            private float _countdownSeconds = 30f;

            public override LevelType SupportedType =>
                LevelType.Sorting;
        }

        private sealed class UnsafeLifecycleLevel : LevelBase
        {
            public override LevelType SupportedType =>
                LevelType.Sorting;

            public void FailLevel()
            {
            }
        }

        private sealed class ControllerWithoutUndo
        {
        }

        private sealed class ControllerWithUndoHistory
        {
            public void Construct(IUndoHistory history)
            {
            }
        }
    }
}
