using UnityEngine;
using VContainer;
using VContainer.Unity;
using OutsiderDriver.Audio;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Player;
using OutsiderDriver.Rides;
using OutsiderDriver.UI.Dialogue;
using OutsiderDriver.UI.TaxiShift;
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
        [Header("Systemic Ride Demand")]
        [SerializeField] private RideDemandConfigSO _rideDemandConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<RoadNetwork>();
            builder.RegisterComponentInHierarchy<VehicleController>();
            builder.RegisterComponentInHierarchy<TrafficDirector>();
            builder.Register<RideLifecycleService>(Lifetime.Scoped).As<IRideLifecycleService>();

            if (_rideDemandConfig != null)
            {
                builder.Register<RideDemandService>(Lifetime.Scoped)
                    .As<IRideDemandService>()
                    .WithParameter(_rideDemandConfig);
            }

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

            var taxiShiftView = FindAnyObjectByType<TaxiShiftView>();
            if (taxiShiftView != null)
            {
                builder.RegisterComponent(taxiShiftView).As<ITaxiShiftView>();
                builder.RegisterEntryPoint<TaxiShiftPresenter>(Lifetime.Scoped);
            }
        }
    }
}
