using System.Collections.Generic;
using CalmSpace.Core;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class UndoHistoryTests
    {
        [Test]
        public void UndoExecutesCommandsInLastInFirstOutOrder()
        {
            var undoneIds = new List<int>();
            var history = new LevelSessionUndoHistory();
            history.Push(new RecordingCommand(1, undoneIds));
            history.Push(new RecordingCommand(2, undoneIds));
            history.Push(new RecordingCommand(3, undoneIds));

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.Undo(), Is.True);
            Assert.That(history.Undo(), Is.True);
            Assert.That(history.Undo(), Is.True);

            Assert.That(undoneIds, Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(history.Count, Is.Zero);
            Assert.That(history.CanUndo, Is.False);
        }

        [Test]
        public void HistoryDoesNotApplyAnArtificialCommandLimit()
        {
            const int commandCount = 4096;
            var undoneIds = new List<int>(commandCount);
            var history = new LevelSessionUndoHistory();

            for (var index = 0; index < commandCount; index++)
            {
                history.Push(new RecordingCommand(index, undoneIds));
            }

            Assert.That(history.Count, Is.EqualTo(commandCount));

            for (var index = 0; index < commandCount; index++)
            {
                Assert.That(history.Undo(), Is.True);
            }

            Assert.That(undoneIds.Count, Is.EqualTo(commandCount));
            for (var index = 0; index < commandCount; index++)
            {
                Assert.That(
                    undoneIds[index],
                    Is.EqualTo(commandCount - index - 1));
            }
        }

        [Test]
        public void ClearDropsAllCommandsAndReportsUnavailable()
        {
            var availability = new List<bool>();
            var undoneIds = new List<int>();
            var history = new LevelSessionUndoHistory();
            history.AvailabilityChanged += availability.Add;

            history.Push(new RecordingCommand(1, undoneIds));
            history.Push(new RecordingCommand(2, undoneIds));
            history.Clear();
            history.Clear();

            Assert.That(history.Count, Is.Zero);
            Assert.That(history.CanUndo, Is.False);
            Assert.That(availability, Is.EqualTo(new[] { true, false }));
            Assert.That(history.Undo(), Is.False);
            Assert.That(undoneIds, Is.Empty);
        }

        [Test]
        public void InvalidCommandsAreIgnoredOrSkippedWithoutBlockingOlderUndo()
        {
            var undoneIds = new List<int>();
            var history = new LevelSessionUndoHistory();
            var initiallyInvalid = new RecordingCommand(
                1,
                undoneIds,
                canUndo: false);
            var valid = new RecordingCommand(2, undoneIds);
            var failed = new RecordingCommand(
                3,
                undoneIds,
                result: false);
            var becameInvalid = new RecordingCommand(4, undoneIds);

            history.Push(null);
            history.Push(initiallyInvalid);
            history.Push(valid);
            history.Push(failed);
            history.Push(becameInvalid);
            becameInvalid.CanUndo = false;

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.Undo(), Is.True);

            Assert.That(becameInvalid.TryCount, Is.Zero);
            Assert.That(failed.TryCount, Is.EqualTo(1));
            Assert.That(valid.TryCount, Is.EqualTo(1));
            Assert.That(initiallyInvalid.TryCount, Is.Zero);
            Assert.That(undoneIds, Is.EqualTo(new[] { 2 }));
            Assert.That(history.Count, Is.Zero);
            Assert.That(history.Undo(), Is.False);
        }

        private sealed class RecordingCommand : IUndoCommand
        {
            private readonly int _id;
            private readonly List<int> _undoneIds;
            private readonly bool _result;

            public RecordingCommand(
                int id,
                List<int> undoneIds,
                bool canUndo = true,
                bool result = true)
            {
                _id = id;
                _undoneIds = undoneIds;
                CanUndo = canUndo;
                _result = result;
            }

            public bool CanUndo { get; set; }

            public int TryCount { get; private set; }

            public bool TryUndo()
            {
                TryCount++;
                if (!_result)
                {
                    return false;
                }

                _undoneIds.Add(_id);
                return true;
            }
        }
    }
}
