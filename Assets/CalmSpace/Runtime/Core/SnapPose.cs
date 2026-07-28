using UnityEngine;

namespace CalmSpace.Core
{
    /// <summary>
    /// Immutable world-space pose used by draggable items and snap targets.
    /// </summary>
    public readonly struct SnapPose
    {
        public SnapPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }
    }
}
