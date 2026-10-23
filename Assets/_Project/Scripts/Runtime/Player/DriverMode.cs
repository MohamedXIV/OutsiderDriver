namespace OutsiderDriver.Player
{
    /// <summary>
    /// Represents whether the protagonist is currently driving the taxi or walking on foot
    /// within the bounded home and starter neighborhood scope (Decision P-009).
    /// </summary>
    public enum DriverMode
    {
        /// <summary>
        /// Inside the taxi; semi-autonomous road navigation and taxi camera active.
        /// </summary>
        InTaxi = 0,

        /// <summary>
        /// Walking on foot inside the apartment, garage, or starter neighborhood street;
        /// closer on-foot camera and direct 2D isometric movement active.
        /// </summary>
        OnFoot = 1
    }
}
