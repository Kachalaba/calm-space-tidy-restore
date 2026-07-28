using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CalmSpace.Input
{
    /// <summary>
    /// Performs an explicit UI raycast on press frames. This is reliable even
    /// when the low-latency world input router executes before the EventSystem
    /// has updated its cached pointer-over state.
    /// </summary>
    internal static class UiPointerBlocker
    {
        private static readonly List<RaycastResult> Results =
            new List<RaycastResult>(16);

        private static EventSystem _eventSystem;
        private static PointerEventData _pointerEvent;

        public static bool IsBlocked(Vector2 screenPosition)
        {
            EventSystem current = EventSystem.current;
            if (current == null)
            {
                return false;
            }

            if (_eventSystem != current || _pointerEvent == null)
            {
                _eventSystem = current;
                _pointerEvent = new PointerEventData(current);
            }

            _pointerEvent.Reset();
            _pointerEvent.position = screenPosition;
            Results.Clear();
            current.RaycastAll(_pointerEvent, Results);
            bool blocked = Results.Count > 0;
            Results.Clear();
            return blocked;
        }
    }
}
