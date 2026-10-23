using System;
using VContainer;
using VContainer.Unity;

namespace OutsiderDriver.Core.Time
{
    /// <summary>
    /// Concrete implementation of the continuous real-time simulation clock.
    /// Driven by VContainer's ITickable loop in runtime, or manually in unit tests.
    /// </summary>
    public sealed class TimeService : ITimeService, ITickable
    {
        private readonly float _realSecondsPerInGameMinute;
        private WorldTime _currentTime;
        private float _accumulatedSeconds;
        private float _timeScale = 1.0f;
        private bool _isPaused;

        public WorldTime CurrentTime => _currentTime;
        public bool IsPaused => _isPaused;
        public float RealSecondsPerInGameMinute => _realSecondsPerInGameMinute;

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Math.Max(0f, value);
        }

        public event Action<WorldTime> OnMinuteTicked;
        public event Action<WorldTime> OnHourTicked;
        public event Action<WorldTime> OnDayChanged;
        public event Action<bool> OnPauseStateChanged;

        [Inject]
        public TimeService(TimeConfigSO config = null)
        {
            if (config != null)
            {
                _realSecondsPerInGameMinute = Math.Max(0.05f, config.RealSecondsPerInGameMinute);
                _currentTime = config.InitialWorldTime;
            }
            else
            {
                _realSecondsPerInGameMinute = 1.0f;
                _currentTime = new WorldTime(day: 1, hour: 18, minute: 0);
            }
        }

        public void Tick()
        {
            AdvanceTime(UnityEngine.Time.deltaTime);
        }

        public void AdvanceTime(float realDeltaSeconds)
        {
            if (_isPaused || realDeltaSeconds <= 0f || _timeScale <= 0f)
            {
                return;
            }

            _accumulatedSeconds += realDeltaSeconds * _timeScale;

            while (_accumulatedSeconds >= _realSecondsPerInGameMinute)
            {
                _accumulatedSeconds -= _realSecondsPerInGameMinute;
                StepOneMinute();
            }
        }

        public void Pause()
        {
            if (_isPaused) return;
            _isPaused = true;
            OnPauseStateChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;
            OnPauseStateChanged?.Invoke(false);
        }

        public void SetTime(WorldTime newTime)
        {
            WorldTime previous = _currentTime;
            _currentTime = newTime;
            _accumulatedSeconds = 0f;

            if (_currentTime != previous)
            {
                OnMinuteTicked?.Invoke(_currentTime);
                if (_currentTime.Hour != previous.Hour || _currentTime.Day != previous.Day)
                {
                    OnHourTicked?.Invoke(_currentTime);
                }
                if (_currentTime.Day != previous.Day)
                {
                    OnDayChanged?.Invoke(_currentTime);
                }
            }
        }

        private void StepOneMinute()
        {
            WorldTime previous = _currentTime;
            _currentTime = _currentTime.AddMinutes(1);

            OnMinuteTicked?.Invoke(_currentTime);

            if (_currentTime.Hour != previous.Hour)
            {
                OnHourTicked?.Invoke(_currentTime);
            }

            if (_currentTime.Day != previous.Day)
            {
                OnDayChanged?.Invoke(_currentTime);
            }
        }
    }
}
