using System;
using UnityEngine;

namespace OutsiderDriver.Core.State
{
    /// <summary>
    /// Serializable record of the driver's relationship with a specific resident.
    /// Keeps liking/affinity, personal trust, and human-cover suspicion distinct (Decision X-011).
    /// </summary>
    [Serializable]
    public class ResidentRelationshipData
    {
        public string ResidentId;
        public int EncounterCount;
        public int Affinity;
        public int PersonalTrust;
        [Range(0, 100)]
        public int SuspicionOfHumanity;
        public int LastEncounterTotalMinutes;

        public ResidentRelationshipData()
        {
        }

        public ResidentRelationshipData(string residentId)
        {
            ResidentId = residentId;
            EncounterCount = 0;
            Affinity = 0;
            PersonalTrust = 0;
            SuspicionOfHumanity = 0;
            LastEncounterTotalMinutes = 0;
        }
    }
}
