using System.Diagnostics;
using UnityEngine;

namespace CalmSpace.Core
{
    public interface IMonotonicClock
    {
        double NowSeconds { get; }
    }

    /// <summary>
    /// Unity's unscaled, monotonic process clock. It is unaffected by time scale
    /// and is suitable for presentation cooldowns within the current run.
    /// </summary>
    public sealed class RealtimeClock : IMonotonicClock
    {
        public double NowSeconds => Time.realtimeSinceStartupAsDouble;
    }

    /// <summary>
    /// Explicitly named adapter for composition roots that prefer the clock's
    /// implementation detail in the registered type name.
    /// </summary>
    public sealed class UnityMonotonicClock : IMonotonicClock
    {
        private static readonly double TimestampToSeconds =
            1d / Stopwatch.Frequency;

        public double NowSeconds =>
            Stopwatch.GetTimestamp() * TimestampToSeconds;
    }
}
