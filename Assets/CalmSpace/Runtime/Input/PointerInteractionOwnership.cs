using UnityEngine;

namespace CalmSpace.Input
{
    /// <summary>
    /// One physical pointer gesture may drive exactly one gameplay mechanic.
    /// DragInputRouter runs first, so items placed over a cleaning mesh win the
    /// hit; otherwise the cleaning controller may claim the stroke.
    /// </summary>
    internal static class PointerInteractionOwnership
    {
        private static object _owner;

        public static bool TryAcquire(object owner)
        {
            if (owner == null)
            {
                return false;
            }

            if (_owner != null && !ReferenceEquals(_owner, owner))
            {
                return false;
            }

            _owner = owner;
            return true;
        }

        public static void Release(object owner)
        {
            if (ReferenceEquals(_owner, owner))
            {
                _owner = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _owner = null;
        }
    }
}
