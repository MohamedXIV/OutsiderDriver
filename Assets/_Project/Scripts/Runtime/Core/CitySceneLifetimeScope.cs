using UnityEngine;
using VContainer;
using VContainer.Unity;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Core
{
    /// <summary>
    /// Per-scene VContainer dependency injection scope for city navigation, traffic, and local actors.
    /// </summary>
    [DisallowMultipleComponent]
    public class CitySceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<RoadNetwork>();
            builder.RegisterComponentInHierarchy<VehicleController>();
            builder.RegisterComponentInHierarchy<TrafficDirector>();
        }
    }
}
