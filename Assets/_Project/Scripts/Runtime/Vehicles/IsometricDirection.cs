namespace OutsiderDriver.Vehicles
{
    /// <summary>
    /// Cardinal and intercardinal isometric facing directions.
    /// In standard 2:1 dimetric/isometric 2D projections:
    /// NorthEast = +X, +Y
    /// SouthEast = +X, -Y
    /// SouthWest = -X, -Y
    /// NorthWest = -X, +Y
    /// </summary>
    public enum IsometricDirection
    {
        NorthEast,
        SouthEast,
        SouthWest,
        NorthWest,
        North,
        East,
        South,
        West
    }
}
