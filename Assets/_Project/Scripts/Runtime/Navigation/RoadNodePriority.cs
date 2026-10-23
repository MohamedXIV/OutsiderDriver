namespace OutsiderDriver.Navigation
{
    /// <summary>
    /// Defines traffic control and right-of-way rules at a road node (Decision P-019).
    /// </summary>
    public enum RoadNodePriority
    {
        /// <summary>
        /// Standard waypoint or main-artery crossing with right-of-way.
        /// </summary>
        MainArtery = 0,

        /// <summary>
        /// Vehicles approaching this node must yield to any vehicle already occupying or crossing the intersection.
        /// </summary>
        YieldIntersection = 1
    }
}
