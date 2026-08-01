using System;
using System.Collections.Generic;

namespace CalmSpace.Core
{
    public interface IUndoCommand
    {
        bool CanUndo { get; }

        bool TryUndo();
    }

    public interface IUndoHistory
    {
        event Action<bool> AvailabilityChanged;

        bool CanUndo { get; }

        int Count { get; }

        void Push(IUndoCommand command);

        bool Undo();

        void Clear();
    }

    /// <summary>
    /// Global LIFO history for the currently loaded level. There is no
    /// player-visible cap; the level loader clears the list on transition.
    /// Commands capture only compact action state, never textures or frames.
    /// </summary>
    public sealed class LevelSessionUndoHistory : IUndoHistory
    {
        private readonly List<IUndoCommand> _commands =
            new List<IUndoCommand>(16);

        public event Action<bool> AvailabilityChanged;

        public bool CanUndo => _commands.Count > 0;

        public int Count => _commands.Count;

        public void Push(IUndoCommand command)
        {
            if (command == null || !command.CanUndo)
            {
                return;
            }

            bool wasAvailable = CanUndo;
            _commands.Add(command);
            if (!wasAvailable)
            {
                AvailabilityChanged?.Invoke(true);
            }
        }

        public bool Undo()
        {
            while (_commands.Count > 0)
            {
                int index = _commands.Count - 1;
                IUndoCommand command = _commands[index];
                _commands.RemoveAt(index);
                if (command != null &&
                    command.CanUndo &&
                    command.TryUndo())
                {
                    AvailabilityChanged?.Invoke(CanUndo);
                    return true;
                }
            }

            AvailabilityChanged?.Invoke(false);
            return false;
        }

        public void Clear()
        {
            if (_commands.Count == 0)
            {
                return;
            }

            _commands.Clear();
            AvailabilityChanged?.Invoke(false);
        }
    }
}
