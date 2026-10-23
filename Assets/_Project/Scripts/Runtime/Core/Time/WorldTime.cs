using System;

namespace OutsiderDriver.Core.Time
{
    public enum TimeOfDayPeriod
    {
        Morning,
        Afternoon,
        Dusk,
        Night
    }

    /// <summary>
    /// Immutable value type representing in-game calendar day, hour, and minute.
    /// </summary>
    [Serializable]
    public readonly struct WorldTime : IEquatable<WorldTime>, IComparable<WorldTime>
    {
        public const int MinutesPerHour = 60;
        public const int HoursPerDay = 24;
        public const int MinutesPerDay = HoursPerDay * MinutesPerHour;

        public readonly int Day;
        public readonly int Hour;
        public readonly int Minute;

        public int TotalMinutes => ((Day - 1) * MinutesPerDay) + (Hour * MinutesPerHour) + Minute;

        public TimeOfDayPeriod Period
        {
            get
            {
                if (Hour >= 6 && Hour < 12) return TimeOfDayPeriod.Morning;
                if (Hour >= 12 && Hour < 17) return TimeOfDayPeriod.Afternoon;
                if (Hour >= 17 && Hour < 21) return TimeOfDayPeriod.Dusk;
                return TimeOfDayPeriod.Night;
            }
        }

        public WorldTime(int day, int hour, int minute)
        {
            int normalizedDay = Math.Max(1, day);
            int totalMins = ((normalizedDay - 1) * MinutesPerDay) + (hour * MinutesPerHour) + minute;
            if (totalMins < 0) totalMins = 0;

            Day = (totalMins / MinutesPerDay) + 1;
            int remainder = totalMins % MinutesPerDay;
            Hour = remainder / MinutesPerHour;
            Minute = remainder % MinutesPerHour;
        }

        public WorldTime AddMinutes(int minutesToAdd)
        {
            return new WorldTime(Day, Hour, Minute + minutesToAdd);
        }

        public string ToFormatted24H()
        {
            return $"{Hour:D2}:{Minute:D2}";
        }

        public override string ToString()
        {
            return $"Day {Day} - {Hour:D2}:{Minute:D2} ({Period})";
        }

        public bool Equals(WorldTime other)
        {
            return Day == other.Day && Hour == other.Hour && Minute == other.Minute;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldTime other && Equals(other);
        }

        public override int GetHashCode()
        {
            return TotalMinutes;
        }

        public int CompareTo(WorldTime other)
        {
            return TotalMinutes.CompareTo(other.TotalMinutes);
        }

        public static bool operator ==(WorldTime left, WorldTime right) => left.Equals(right);
        public static bool operator !=(WorldTime left, WorldTime right) => !left.Equals(right);
        public static bool operator <(WorldTime left, WorldTime right) => left.CompareTo(right) < 0;
        public static bool operator >(WorldTime left, WorldTime right) => left.CompareTo(right) > 0;
        public static bool operator <=(WorldTime left, WorldTime right) => left.CompareTo(right) <= 0;
        public static bool operator >=(WorldTime left, WorldTime right) => left.CompareTo(right) >= 0;
    }
}
