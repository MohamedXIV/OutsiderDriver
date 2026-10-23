using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutsiderDriver.Core.State
{
    /// <summary>
    /// Pure serializable C# DTO representing the player's current resources,
    /// equipped vehicle/translator modules, cover integrity, and known contacts.
    /// </summary>
    [Serializable]
    public class PlayerProfileData
    {
        public const string DefaultCommonLanguageId = "Common";

        public int Credits = 100;
        [Range(0, 100)]
        public int CoverIntegrity = 100;
        public bool HasGpsModule = true;
        public bool HasRadioModule = true;
        public List<string> InstalledLanguageIds = new List<string> { DefaultCommonLanguageId };
        public List<ResidentRelationshipData> KnownResidents = new List<ResidentRelationshipData>();
    }
}
