using UnityEngine;
using VContainer;
using VContainer.Unity;
using OutsiderDriver.Audio;
using OutsiderDriver.Navigation;
using OutsiderDriver.Player;
using OutsiderDriver.Rides;
using OutsiderDriver.UI.Dialogue;
using OutsiderDriver.Vehicles;
using OutsiderDriver.Visuals.Portraits;
using OutsiderDriver.World;

namespace OutsiderDriver.Core
{
    /// <summary>
    /// Per-scene VContainer dependency injection scope for city navigation, traffic, ride lifecycle,
    /// walkable home/neighborhood layout, on-foot player controller, radio/ambient audio, UI presenters, and local actors.
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

            var worldRegistry = FindAnyObjectByType<CityWorldRegistry>();
            if (worldRegistry != null)
            {
                builder.RegisterComponent(worldRegistry);
            }

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

            var radioController = FindAnyObjectByType<RadioAudioController>();
            if (radioController != null)
            {
                builder.RegisterComponent(radioController).AsSelf().As<IRadioAudioService>();
            }

            var portraitRig = FindAnyObjectByType<CharacterPortraitRig>();
            if (portraitRig != null)
            {
                builder.RegisterComponent(portraitRig);
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
