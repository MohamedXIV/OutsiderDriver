using System;
using UnityEngine;

namespace OutsiderDriver.Data
{
    /// <summary>
    /// Represents a coarse time-of-day schedule window for a named resident (Skyrim Radiant AI model).
    /// Off-screen residents resolve their current landmark/node virtually on the TimeService coarse tick.
    /// </summary>
    [Serializable]
    public struct PassengerScheduleEntry
    {
        [Tooltip("Start hour (0-23) when this schedule block begins.")]
        [Range(0, 23)]
        public int StartHour;

        [Tooltip("End hour (0-23) when this schedule block ends. Supports wrapping past midnight (e.g., 22 to 4).")]
        [Range(0, 23)]
        public int EndHour;

        [Tooltip("District ID where the resident typically stays or hails from during this window.")]
        public string DistrictId;

        [Tooltip("Target RoadNode ID where the resident can be found or prefers to travel.")]
        public string PreferredNodeId;

        public PassengerScheduleEntry(int startHour, int endHour, string districtId, string preferredNodeId)
        {
            StartHour = Mathf.Clamp(startHour, 0, 23);
            EndHour = Mathf.Clamp(endHour, 0, 23);
            DistrictId = districtId ?? string.Empty;
            PreferredNodeId = preferredNodeId ?? string.Empty;
        }

        /// <summary>
        /// Checks whether the specified hour (0-23) falls within [StartHour, EndHour).
        /// Handles both standard ranges (e.g., 8 to 17) and overnight ranges (e.g., 22 to 4).
        /// If StartHour == EndHour, the schedule is treated as active all 24 hours.
        /// </summary>
        public bool IsActiveAtHour(int hour)
        {
            int normalizedHour = ((hour % 24) + 24) % 24;
            if (StartHour == EndHour)
            {
                return true;
            }

            if (StartHour < EndHour)
            {
                return normalizedHour >= StartHour && normalizedHour < EndHour;
            }

            // Overnight wrap (e.g., 22:00 to 04:00)
            return normalizedHour >= StartHour || normalizedHour < EndHour;
        }
    }
}
