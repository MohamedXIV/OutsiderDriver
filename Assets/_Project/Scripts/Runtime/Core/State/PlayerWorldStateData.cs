using System;
using UnityEngine;

namespace OutsiderDriver.Core.State
{
    /// <summary>
    /// Pure serializable C# DTO capturing the player's world location, driver mode, and facing direction.
    /// </summary>
    [Serializable]
    public class PlayerWorldStateData
    {
        public float PositionX;
        public float PositionY;
        public string DriverMode = "InTaxi";
        public string Facing = "SouthEast";

        public PlayerWorldStateData()
        {
        }

        public PlayerWorldStateData(float x, float y, string driverMode, string facing = "SouthEast")
        {
            PositionX = x;
            PositionY = y;
            DriverMode = driverMode ?? "InTaxi";
            Facing = facing ?? "SouthEast";
        }
    }
}
