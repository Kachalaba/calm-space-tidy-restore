using System;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Demo;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class WorkshopAnalyticsTests
    {
        [Test]
        public void DailyCareAvailabilityAcceptsOnlyForwardValidUtcDayKeys()
        {
            object state = CreateState();

            int[] invalidKeys =
            {
                0,
                -1,
                20260001,
                20260100,
                20261301,
                20260230,
                20250229
            };
            for (var index = 0; index < invalidKeys.Length; index++)
            {
                Assert.That(
                    InvokeBool(
                        state,
                        "TryAdmitDailyCareAvailable",
                        invalidKeys[index]),
                    Is.False);
            }

            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitDailyCareAvailable",
                    20260801),
                Is.True);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitDailyCareAvailable",
                    20260801),
                Is.False);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitDailyCareAvailable",
                    20260731),
                Is.False);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitDailyCareAvailable",
                    20260802),
                Is.True);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitDailyCareAvailable",
                    20260801),
                Is.False);
            Assert.That(
                HasCallerControlledDailyKeyCollection(state),
                Is.False);
        }

        [Test]
        public void RewardedOfferAdmitsExactlyOneOutcomeForItsActiveSession()
        {
            object state = CreateState();

            int offerSessionId = BeginRewardedOffer(state);

            Assert.That(offerSessionId, Is.GreaterThan(0));
            Assert.That(BeginRewardedOffer(state), Is.EqualTo(0));
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitRewardedOfferOutcome",
                    offerSessionId),
                Is.True);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitRewardedOfferOutcome",
                    offerSessionId),
                Is.False);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitRewardedOfferOutcome",
                    offerSessionId + 1),
                Is.False);

            int nextOfferSessionId = BeginRewardedOffer(state);
            Assert.That(nextOfferSessionId, Is.GreaterThan(offerSessionId));
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitRewardedOfferOutcome",
                    offerSessionId),
                Is.False);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitRewardedOfferOutcome",
                    nextOfferSessionId),
                Is.True);
        }

        [Test]
        public void FirstTimeProfileEventsRequireAppliedMutationAndDoNotRepeat()
        {
            object state = CreateState();
            var rejectedStatuses = new[]
            {
                ProfileMutationStatus.AlreadyApplied,
                ProfileMutationStatus.PersistFailed,
                ProfileMutationStatus.Invalid
            };

            for (var index = 0; index < rejectedStatuses.Length; index++)
            {
                Assert.That(
                    InvokeBool(
                        state,
                        "TryAdmitMemoryUnlocked",
                        "tea-postcard-" + index,
                        rejectedStatuses[index]),
                    Is.False);
                Assert.That(
                    InvokeBool(
                        state,
                        "TryAdmitRestorationRevealCompleted",
                        "tea-drawer-" + index,
                        rejectedStatuses[index]),
                    Is.False);
                Assert.That(
                    InvokeBool(
                        state,
                        "TryAdmitChapterCompleted",
                        "cozy-workshop-" + index,
                        rejectedStatuses[index]),
                    Is.False);
            }

            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitMemoryUnlocked",
                    "tea-postcard",
                    ProfileMutationStatus.Applied),
                Is.True);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitMemoryUnlocked",
                    "tea-postcard",
                    ProfileMutationStatus.Applied),
                Is.False);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitRestorationRevealCompleted",
                    "tea-drawer",
                    ProfileMutationStatus.Applied),
                Is.True);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitRestorationRevealCompleted",
                    "tea-drawer",
                    ProfileMutationStatus.Applied),
                Is.False);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitChapterCompleted",
                    "cozy-workshop",
                    ProfileMutationStatus.Applied),
                Is.True);
            Assert.That(
                InvokeBool(
                    state,
                    "TryAdmitChapterCompleted",
                    "cozy-workshop",
                    ProfileMutationStatus.Applied),
                Is.False);
        }

        [Test]
        public void RepeatablePresentationActionsRemainAdmitted()
        {
            object state = CreateState();

            Assert.That(
                InvokeBool(state, "TryAdmitRestorationRevealStarted"),
                Is.True);
            Assert.That(
                InvokeBool(state, "TryAdmitRestorationRevealStarted"),
                Is.True);
            Assert.That(
                InvokeBool(state, "TryAdmitMemoryViewed"),
                Is.True);
            Assert.That(
                InvokeBool(state, "TryAdmitMemoryViewed"),
                Is.True);
        }

        private static object CreateState()
        {
            Type type = Type.GetType(
                "CalmSpace.Analytics.WorkshopAnalyticsSessionState, " +
                "CalmSpace.Runtime");
            Assert.That(type, Is.Not.Null, "Workshop analytics state is required.");
            return Activator.CreateInstance(type);
        }

        private static int BeginRewardedOffer(object state)
        {
            MethodInfo method = state.GetType().GetMethod(
                "TryBeginRewardedOffer",
                new[] { typeof(int).MakeByRefType() });
            Assert.That(method, Is.Not.Null, "Rewarded offer gate is required.");
            var arguments = new object[] { 0 };

            bool admitted = (bool)method.Invoke(state, arguments);
            return admitted ? (int)arguments[0] : 0;
        }

        private static bool InvokeBool(
            object state,
            string methodName,
            params object[] arguments)
        {
            Type[] parameterTypes = new Type[arguments.Length];
            for (var index = 0; index < arguments.Length; index++)
            {
                parameterTypes[index] = arguments[index].GetType();
            }

            MethodInfo method = state.GetType().GetMethod(
                methodName,
                parameterTypes);
            Assert.That(method, Is.Not.Null, methodName + " gate is required.");
            return (bool)method.Invoke(state, arguments);
        }

        private static bool HasCallerControlledDailyKeyCollection(object state)
        {
            FieldInfo[] fields = state.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (var index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType == typeof(HashSet<int>))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
