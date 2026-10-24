using System;
using UnityEngine;
using OutsiderDriver.Core.Time;

namespace OutsiderDriver.Core.State
{
    /// <summary>
    /// Root serializable save container capturing simulation clock and player profile state.
    /// </summary>
    [Serializable]
    public class SaveStateData
    {
        public int Version = 1;
        public int Day = 1;
        public int Hour = 18;
        public int Minute = 0;
        public PlayerProfileData Profile = new PlayerProfileData();
        public PlayerWorldStateData World = new PlayerWorldStateData();

        public WorldTime GetWorldTime()
        {
            return new WorldTime(Day, Hour, Minute);
        }

        public void SetWorldTime(WorldTime time)
        {
            Day = time.Day;
            Hour = time.Hour;
            Minute = time.Minute;
        }

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static SaveStateData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new SaveStateData();
            }

            try
            {
                var loaded = JsonUtility.FromJson<SaveStateData>(json);
                if (loaded == null)
                {
                    return new SaveStateData();
                }

                loaded.Profile ??= new PlayerProfileData();
                loaded.Profile.InstalledLanguageIds ??= new System.Collections.Generic.List<string>();
                loaded.Profile.KnownResidents ??= new System.Collections.Generic.List<ResidentRelationshipData>();
                loaded.World ??= new PlayerWorldStateData();
                return loaded;
            }
            catch
            {
                return null;
            }
        }
    }
}
