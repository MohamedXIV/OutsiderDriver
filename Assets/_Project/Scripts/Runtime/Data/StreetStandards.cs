namespace OutsiderDriver.Data
{
    /// <summary>
    /// Physical conversion constants for the authored world.
    /// One planning cell is one metre; vehicles use the nominal baseline dimensions
    /// from the owner's street-standard decision (2026-10-25).
    /// </summary>
    public static class StreetStandards
    {
        public const float MetersPerCell = 1f;
        public const float NominalCarWidthMeters = 2f;
        public const float NominalCarLengthMeters = 4.5f;
        public const float NominalTravelLaneWidthMeters = 3f;
        public const float NominalSidewalkWidthMeters = 2f;
        public const float NominalLocalTwoWayWidthMeters = 10f;
        public const float NominalTwoLanesEachDirectionWidthMeters = 16f;
        public const float NominalLaneCenterOffsetMeters = NominalTravelLaneWidthMeters * 0.5f;
    }
}
