using System;

namespace OutsiderDriver.Core.Time
{
    /// <summary>
    /// Authoritative simulation clock providing continuous time progression
    /// and discrete coarse simulation ticks for schedules and game systems.
    /// </summary>
    public interface ITimeService
    {
        WorldTime CurrentTime { get; }
        bool IsPaused { get; }
        float TimeScale { get; set; }
        float RealSecondsPerInGameMinute { get; }

        /// <summary>
        /// Fired once per in-game minute (the primary coarse simulation tick).
        /// </summary>
        event Action<WorldTime> OnMinuteTicked;

        /// <summary>
        /// Fired whenever the in-game hour rolls over.
        /// </summary>
        event Action<WorldTime> OnHourTicked;

        /// <summary>
        /// Fired whenever the in-game day increments.
        /// </summary>
        event Action<WorldTime> OnDayChanged;

        /// <summary>
        /// Fired when the simulation clock is paused or resumed.
        /// </summary>
        event Action<bool> OnPauseStateChanged;

        void Pause();
        void Resume();
        void SetTime(WorldTime newTime);
        void AdvanceTime(float realDeltaSeconds);
    }
}
