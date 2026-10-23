using UnityEngine;
using VContainer;
using VContainer.Unity;
using OutsiderDriver.Navigation;
using OutsiderDriver.Player;
using OutsiderDriver.Rides;
using OutsiderDriver.UI.Dialogue;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Core
{
    /// <summary>
    /// Per-scene VContainer dependency injection scope for city navigation, traffic, ride lifecycle,
    /// walkable home/neighborhood layout, on-foot player controller, UI presenters, and local actors.
    /// </summary>
    [DisallowMultipleComponent]
    public class CitySceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<RoadNetwork>();
            builder.RegisterComponentInHierarchy<VehicleController>();
            builder.RegisterComponentInHierarchy<TrafficDirector>();
            builder.Register<RideLifecycleService>(Lifetime.Scoped).As<IRideLifecycleService>();

            var neighborhoodLayout = FindAnyObjectByType<HomeNeighborhoodLayout>();
            if (neighborhoodLayout != null)
            {
                builder.RegisterComponent(neighborhoodLayout);
            }

            var playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                builder.RegisterComponent(playerController);
            }

            var dialogueView = FindAnyObjectByType<DialogueView>();
            if (dialogueView != null)
            {
                builder.RegisterComponent(dialogueView).As<IDialogueView>();
                builder.RegisterEntryPoint<DialoguePresenter>(Lifetime.Scoped);
            }
        }
    }
}
