using CalmSpace.Core;
using UnityEngine;

namespace CalmSpace.Levels
{
    public static class SnapMath
    {
        public static bool IsPositionWithinThreshold(
            Vector3 position,
            Vector3 targetPosition,
            float positionThreshold)
        {
            if (!IsFiniteNonNegative(positionThreshold))
            {
                return false;
            }

            var delta = position - targetPosition;
            var distanceSquared = delta.sqrMagnitude;
            var thresholdSquared =
                positionThreshold * positionThreshold;

            // Authored thresholds are inclusive. Account for the single-
            // precision rounding introduced when the world-space vector is
            // squared, without widening the actual gameplay tolerance.
            return distanceSquared <= thresholdSquared ||
                   Mathf.Approximately(
                       distanceSquared,
                       thresholdSquared);
        }

        public static bool IsPoseWithinThreshold(
            Vector3 position,
            Quaternion rotation,
            SnapPose target,
            float positionThreshold,
            float rotationThresholdDegrees)
        {
            if (!IsFiniteNonNegative(rotationThresholdDegrees))
            {
                return false;
            }

            return IsPositionWithinThreshold(
                       position,
                       target.Position,
                       positionThreshold) &&
                   Quaternion.Angle(rotation, target.Rotation) <=
                   rotationThresholdDegrees;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f &&
                   !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
