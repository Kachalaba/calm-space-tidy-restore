using System;

namespace CalmSpace.Fasteners
{
    public readonly struct ScrewRotationSnapshot :
        IEquatable<ScrewRotationSnapshot>
    {
        public ScrewRotationSnapshot(
            float elapsedSeconds,
            int completedTurns)
        {
            ElapsedSeconds = elapsedSeconds;
            CompletedTurns = completedTurns;
        }

        public float ElapsedSeconds { get; }

        public int CompletedTurns { get; }

        public bool Equals(ScrewRotationSnapshot other)
        {
            return
                ElapsedSeconds.Equals(other.ElapsedSeconds) &&
                CompletedTurns == other.CompletedTurns;
        }

        public override bool Equals(object obj)
        {
            return
                obj is ScrewRotationSnapshot other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return
                    (ElapsedSeconds.GetHashCode() * 397) ^
                    CompletedTurns;
            }
        }

        public static bool operator ==(
            ScrewRotationSnapshot left,
            ScrewRotationSnapshot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            ScrewRotationSnapshot left,
            ScrewRotationSnapshot right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Outcome of advancing one screw by a slice of hold time.
    /// </summary>
    public readonly struct ScrewAdvanceResult
    {
        public ScrewAdvanceResult(
            float progress,
            int completedTurns,
            int newlyCompletedTurns,
            bool isComplete)
        {
            Progress = progress;
            CompletedTurns = completedTurns;
            NewlyCompletedTurns = Math.Max(0, newlyCompletedTurns);
            IsComplete = isComplete;
        }

        public float Progress { get; }

        public int CompletedTurns { get; }

        /// <summary>
        /// Number of whole turns crossed by this advance. Frame hitches can
        /// legitimately cross more than one boundary, and each still owes the
        /// player one tactile tick.
        /// </summary>
        public int NewlyCompletedTurns { get; }

        public bool CrossedTurnBoundary => NewlyCompletedTurns > 0;

        public bool IsComplete { get; }
    }

    /// <summary>
    /// Pure hold-to-unscrew accumulator. Lifting the finger stops the
    /// accumulation but never rewinds it: the game has no fail state, so a
    /// half-loosened screw stays loosened and the player may spread one screw
    /// across as many separate holds as they like.
    /// </summary>
    public sealed class ScrewRotationModel
    {
        private readonly int _turnsRequired;
        private readonly float _secondsPerTurn;
        private readonly float _totalSeconds;

        private float _elapsedSeconds;
        private int _completedTurns;

        public ScrewRotationModel(
            int turnsRequired,
            float secondsPerTurn)
        {
            if (turnsRequired < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(turnsRequired),
                    turnsRequired,
                    "A screw must require at least one turn.");
            }

            if (float.IsNaN(secondsPerTurn) ||
                float.IsInfinity(secondsPerTurn) ||
                secondsPerTurn <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(secondsPerTurn),
                    secondsPerTurn,
                    "Seconds per turn must be positive and finite.");
            }

            _turnsRequired = turnsRequired;
            _secondsPerTurn = secondsPerTurn;
            _totalSeconds = turnsRequired * secondsPerTurn;
        }

        public int TurnsRequired => _turnsRequired;

        public int CompletedTurns => _completedTurns;

        public float Progress => _elapsedSeconds / _totalSeconds;

        /// <summary>
        /// Accumulated rotation for the visual, unbounded by whole turns.
        /// </summary>
        public float RotationDegrees =>
            _elapsedSeconds / _secondsPerTurn * 360f;

        public bool IsComplete => _elapsedSeconds >= _totalSeconds;

        public ScrewAdvanceResult Advance(float deltaSeconds)
        {
            if (IsComplete ||
                float.IsNaN(deltaSeconds) ||
                deltaSeconds <= 0f)
            {
                return CreateResult(newlyCompletedTurns: 0);
            }

            _elapsedSeconds = float.IsInfinity(deltaSeconds)
                ? _totalSeconds
                : Math.Min(
                    _totalSeconds,
                    _elapsedSeconds + deltaSeconds);

            int turns = IsComplete
                ? _turnsRequired
                : (int)(_elapsedSeconds / _secondsPerTurn);
            int newlyCompletedTurns =
                Math.Max(0, turns - _completedTurns);
            _completedTurns = turns;
            return CreateResult(newlyCompletedTurns);
        }

        public void Reset()
        {
            _elapsedSeconds = 0f;
            _completedTurns = 0;
        }

        public ScrewRotationSnapshot CaptureSnapshot()
        {
            return new ScrewRotationSnapshot(
                _elapsedSeconds,
                _completedTurns);
        }

        public void Restore(ScrewRotationSnapshot snapshot)
        {
            if (float.IsNaN(snapshot.ElapsedSeconds) ||
                float.IsInfinity(snapshot.ElapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(snapshot));
            }

            _elapsedSeconds = Math.Max(
                0f,
                Math.Min(_totalSeconds, snapshot.ElapsedSeconds));
            int derivedTurns = IsComplete
                ? _turnsRequired
                : (int)(_elapsedSeconds / _secondsPerTurn);
            _completedTurns = Math.Max(
                0,
                Math.Min(
                    derivedTurns,
                    snapshot.CompletedTurns));
        }

        private ScrewAdvanceResult CreateResult(
            int newlyCompletedTurns)
        {
            return new ScrewAdvanceResult(
                Progress,
                _completedTurns,
                newlyCompletedTurns,
                IsComplete);
        }
    }
}
