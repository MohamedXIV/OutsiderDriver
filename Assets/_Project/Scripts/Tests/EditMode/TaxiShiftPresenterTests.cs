using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Data;
using OutsiderDriver.Localization;
using OutsiderDriver.Navigation;
using OutsiderDriver.Rides;
using OutsiderDriver.UI.TaxiShift;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Tests.EditMode
{
    public class TaxiShiftPresenterTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/OutsiderCity.unity";
        private const string DemandConfigPath = "Assets/_Project/Data/Rides/RideDemandConfig_Default.asset";

        [TearDown]
        public void TearDown()
        {
            VehicleController.ClearActiveVehicles();
        }

        [Test]
        public void Start_RendersGeneratedOffersCreditsAndNoActiveRide()
        {
            var world = OpenProductionWorld();
            var view = new FakeTaxiShiftView();
            using var presenter = CreatePresenter(world, view);

            presenter.Start();

            Assert.GreaterOrEqual(view.Offers.Count, 2, "The production city must expose real generated fares.");
            Assert.IsFalse(view.ActiveVisible);
            Assert.IsFalse(view.NoWorkVisible);
            Assert.IsFalse(string.IsNullOrEmpty(view.Title));
            Assert.IsFalse(string.IsNullOrEmpty(view.Credits));
            Assert.IsTrue(view.Credits.Contains("100"), "Default player credits must be displayed.");

            var offerIds = new HashSet<string>();
            for (int i = 0; i < view.Offers.Count; i++)
            {
                var card = view.Offers[i];
                Assert.IsNotNull(card.Offer);
                Assert.IsTrue(offerIds.Add(card.Offer.OfferId), "Every rendered fare must have a stable unique identity.");
                Assert.IsFalse(string.IsNullOrEmpty(card.Passenger));
                Assert.IsFalse(string.IsNullOrEmpty(card.Fare));
                Assert.IsFalse(string.IsNullOrEmpty(card.Pickup));
                Assert.IsFalse(string.IsNullOrEmpty(card.Destination));
            }
        }

        [Test]
        public void WholeRide_AcceptBoardCollect_RequiresPlayerActionsAndContinuesWork()
        {
            var world = OpenProductionWorld();
            var view = new FakeTaxiShiftView();
            using var presenter = CreatePresenter(world, view);
            presenter.Start();

            Assert.GreaterOrEqual(view.Offers.Count, 2);
            var chosen = view.Offers[0].Offer;
            var other = view.Offers[1].Offer;

            // Wait for the passenger at the real curb so accepting starts the boarding phase.
            world.Taxi.transform.position = chosen.PickupNode.Position;
            int creditsBefore = world.Player.Credits;

            view.RaiseAccept(chosen);

            Assert.AreEqual(RidePhase.AwaitingBoarding, world.Ride.CurrentPhase);
            Assert.IsTrue(view.ActiveVisible);
            Assert.IsTrue(view.Active.Value.ShowBoardButton, "Boarding must wait for the player, never auto-board.");
            Assert.IsFalse(view.Active.Value.ShowCollectButton);
            Assert.IsFalse(ContainsOffer(world.Demand.GetEligibleOffers(), chosen.OfferId));
            Assert.IsTrue(ContainsOffer(world.Demand.GetEligibleOffers(), other.OfferId));

            view.RaiseAccept(chosen);
            Assert.AreEqual(RidePhase.AwaitingBoarding, world.Ride.CurrentPhase, "A second accept must not start another ride.");

            view.RaiseDecline(other);
            Assert.IsTrue(ContainsOffer(world.Demand.GetEligibleOffers(), other.OfferId), "Declining while on a fare must not consume other work.");

            view.RaiseBoard();
            Assert.AreEqual(RidePhase.InTransit, world.Ride.CurrentPhase);
            Assert.IsFalse(view.Active.Value.ShowBoardButton);

            SimulateUntilStopped(world.Taxi);
            Assert.AreEqual(RidePhase.ArrivedAtDestination, world.Ride.CurrentPhase);
            Assert.AreEqual(creditsBefore, world.Player.Credits, "Arrival must not settle the fare automatically.");
            Assert.IsTrue(view.Active.Value.ShowCollectButton);

            view.RaiseCollect();

            Assert.AreEqual(RidePhase.Idle, world.Ride.CurrentPhase);
            Assert.Greater(world.Player.Credits, creditsBefore, "Collecting must credit the settled fare.");
            Assert.IsFalse(view.ActiveVisible);
            Assert.IsFalse(string.IsNullOrEmpty(view.Feedback));
            StringAssert.Contains("Fare collected", view.Feedback);
            Assert.IsFalse(view.NoWorkVisible);
            Assert.GreaterOrEqual(view.Offers.Count, 1, "Fresh work must appear after settlement without reloading.");
        }

        [Test]
        public void DeclineOffer_ConsumesOnlyThatFareWithoutStartingARide()
        {
            var world = OpenProductionWorld();
            var view = new FakeTaxiShiftView();
            using var presenter = CreatePresenter(world, view);
            presenter.Start();

            int initialCount = view.Offers.Count;
            Assert.GreaterOrEqual(initialCount, 2);
            var declined = view.Offers[0].Offer;

            view.RaiseDecline(declined);

            Assert.AreEqual(RidePhase.Idle, world.Ride.CurrentPhase);
            Assert.IsFalse(ContainsOffer(world.Demand.GetEligibleOffers(), declined.OfferId));
            Assert.AreEqual(initialCount - 1, view.Offers.Count);
            Assert.IsFalse(world.Demand.TryReserveOffer(declined), "A declined displayed fare must not be selectable again.");
        }

        [Test]
        public void FailedRideActivation_ReleasesTheExactFareBackToTheBoard()
        {
            var world = OpenProductionWorld();
            var view = new FakeTaxiShiftView();
            var failingRide = new RideLifecycleService(world.Network, null, world.Player, world.Time);
            using var presenter = new TaxiShiftPresenter(
                world.Demand,
                failingRide,
                world.Player,
                world.Localization,
                view,
                world.Registry,
                world.Time);

            presenter.Start();
            var chosen = view.Offers[0].Offer;

            view.RaiseAccept(chosen);

            Assert.AreEqual(RidePhase.Idle, failingRide.CurrentPhase);
            Assert.IsTrue(ContainsOffer(world.Demand.GetEligibleOffers(), chosen.OfferId), "A failed ride start must not lose the presented fare.");
            Assert.IsTrue(world.Demand.TryReserveOffer(chosen), "The released fare must be reservable again.");
            world.Demand.ReleaseReservedOffer(chosen);
            Assert.IsFalse(string.IsNullOrEmpty(view.Feedback));
        }

        [Test]
        public void ExpiredDisplayedFare_IsRejected_ThenWorkReturnsAfterCooldown()
        {
            var world = OpenProductionWorld();
            var view = new FakeTaxiShiftView();
            using var presenter = CreatePresenter(world, view);
            presenter.Start();

            var expired = view.Offers[0].Offer;
            Assert.GreaterOrEqual(view.Offers.Count, 2);

            world.Time.SetTime(new WorldTime(day: 1, hour: 12, minute: 31));

            Assert.AreEqual(0, view.Offers.Count, "Expired fares must leave the HUD.");
            Assert.IsTrue(view.NoWorkVisible);

            view.RaiseAccept(expired);
            Assert.AreEqual(RidePhase.Idle, world.Ride.CurrentPhase);
            Assert.IsFalse(string.IsNullOrEmpty(view.Feedback));

            world.Time.SetTime(new WorldTime(day: 1, hour: 12, minute: 52));

            Assert.GreaterOrEqual(view.Offers.Count, 1, "Ordinary work must return through the day without reloading.");
            Assert.IsFalse(view.NoWorkVisible);
        }

        [Test]
        public void BoardOrCollectOutsideTheirPhase_IsIgnored()
        {
            var world = OpenProductionWorld();
            var view = new FakeTaxiShiftView();
            using var presenter = CreatePresenter(world, view);
            presenter.Start();

            var chosen = view.Offers[0].Offer;
            view.RaiseAccept(chosen);
            Assert.AreEqual(RidePhase.EnRouteToPickup, world.Ride.CurrentPhase);
            int creditsBefore = world.Player.Credits;

            view.RaiseBoard();
            Assert.AreEqual(RidePhase.EnRouteToPickup, world.Ride.CurrentPhase, "Boarding must not skip the pickup.");

            view.RaiseCollect();
            Assert.AreEqual(RidePhase.EnRouteToPickup, world.Ride.CurrentPhase, "Fare collection must not skip the ride.");
            Assert.AreEqual(creditsBefore, world.Player.Credits);
        }

        [Test]
        public void ExternalCancellation_ReturnsToIdleAndRefreshesWork()
        {
            var world = OpenProductionWorld();
            var view = new FakeTaxiShiftView();
            using var presenter = CreatePresenter(world, view);
            presenter.Start();

            var chosen = view.Offers[0].Offer;
            world.Taxi.transform.position = chosen.PickupNode.Position;
            view.RaiseAccept(chosen);
            Assert.AreEqual(RidePhase.AwaitingBoarding, world.Ride.CurrentPhase);
            Assert.IsTrue(view.ActiveVisible);

            world.Ride.CancelActiveRide();

            Assert.AreEqual(RidePhase.Idle, world.Ride.CurrentPhase);
            Assert.IsFalse(view.ActiveVisible);
            Assert.IsFalse(string.IsNullOrEmpty(view.Feedback));
            Assert.GreaterOrEqual(view.Offers.Count, 1, "Cancellation must leave the player with usable work.");
        }

        [Test]
        public void LocaleChange_RelocalizesHudAndAppliesTextDirection()
        {
            var world = OpenProductionWorld();
            var view = new FakeTaxiShiftView();
            using var presenter = CreatePresenter(world, view);
            presenter.Start();

            string originalTitle = view.Title;
            Assert.IsFalse(view.Rtl);

            world.Localization.SetLocale("ar");

            Assert.IsTrue(view.Rtl);
            Assert.AreNotEqual(originalTitle, view.Title);
            StringAssert.Contains("100", view.Credits);
        }

        private static void SimulateUntilStopped(VehicleController taxi, int maxSteps = 5000)
        {
            for (int i = 0; i < maxSteps && taxi.HasActiveRoute; i++)
            {
                taxi.TickMovement(0.05f);
            }
        }

        private static bool ContainsOffer(IReadOnlyList<RideOffer> offers, string offerId)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                if (string.Equals(offers[i].OfferId, offerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private TaxiShiftPresenter CreatePresenter(ProductionWorld world, FakeTaxiShiftView view)
        {
            return new TaxiShiftPresenter(
                world.Demand,
                world.Ride,
                world.Player,
                world.Localization,
                view,
                world.Registry,
                world.Time);
        }

        private ProductionWorld OpenProductionWorld()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid());

            var network = UnityEngine.Object.FindAnyObjectByType<RoadNetwork>();
            var registry = UnityEngine.Object.FindAnyObjectByType<CityWorldRegistry>();
            var taxi = UnityEngine.Object.FindAnyObjectByType<VehicleController>();
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<RideDemandConfigSO>(DemandConfigPath);
            Assert.IsNotNull(network);
            Assert.IsNotNull(registry);
            Assert.IsNotNull(taxi);
            Assert.IsNotNull(config);

            var time = new TimeService(null);
            time.SetTime(new WorldTime(day: 1, hour: 12, minute: 0));
            var player = new PlayerStateService(time);
            var demand = new RideDemandService(network, registry, time, config, seedOverride: 7);
            var ride = new RideLifecycleService(network, taxi, player, time);
            var localization = new LocalizationService();

            return new ProductionWorld(network, registry, taxi, demand, ride, time, player, localization);
        }

        private sealed class ProductionWorld
        {
            public readonly RoadNetwork Network;
            public readonly CityWorldRegistry Registry;
            public readonly VehicleController Taxi;
            public readonly RideDemandService Demand;
            public readonly RideLifecycleService Ride;
            public readonly TimeService Time;
            public readonly PlayerStateService Player;
            public readonly LocalizationService Localization;

            public ProductionWorld(
                RoadNetwork network,
                CityWorldRegistry registry,
                VehicleController taxi,
                RideDemandService demand,
                RideLifecycleService ride,
                TimeService time,
                PlayerStateService player,
                LocalizationService localization)
            {
                Network = network;
                Registry = registry;
                Taxi = taxi;
                Demand = demand;
                Ride = ride;
                Time = time;
                Player = player;
                Localization = localization;
            }
        }

        private sealed class FakeTaxiShiftView : ITaxiShiftView
        {
            public readonly List<TaxiOfferCard> Offers = new List<TaxiOfferCard>();
            public TaxiActiveRideCard? Active { get; private set; }
            public bool ActiveVisible { get; private set; }
            public bool NoWorkVisible { get; private set; }
            public bool Rtl { get; private set; }
            public string Title { get; private set; } = string.Empty;
            public string Credits { get; private set; } = string.Empty;
            public string Status { get; private set; } = string.Empty;
            public string Feedback { get; private set; } = string.Empty;
            public string AcceptLabel { get; private set; } = string.Empty;
            public string DeclineLabel { get; private set; } = string.Empty;
            public string BoardLabel { get; private set; } = string.Empty;
            public string CollectLabel { get; private set; } = string.Empty;

            public event Action<RideOffer> OnAcceptClicked;
            public event Action<RideOffer> OnDeclineClicked;
            public event Action OnBoardClicked;
            public event Action OnCollectClicked;

            public void EnsureBuilt()
            {
            }

            public void SetTitle(string title) => Title = title;

            public void SetCredits(string formattedCredits) => Credits = formattedCredits;

            public void SetStatus(string status, bool attention) => Status = status;

            public void SetButtonLabels(string accept, string decline, string board, string collect)
            {
                AcceptLabel = accept;
                DeclineLabel = decline;
                BoardLabel = board;
                CollectLabel = collect;
            }

            public void SetActiveTitle(string title)
            {
            }

            public void SetNoWorkText(string title, string hint)
            {
            }

            public void SetOffers(IReadOnlyList<TaxiOfferCard> offers)
            {
                Offers.Clear();
                if (offers != null)
                {
                    for (int i = 0; i < offers.Count; i++)
                    {
                        Offers.Add(offers[i]);
                    }
                }
            }

            public void SetNoWorkVisible(bool visible) => NoWorkVisible = visible;

            public void SetActiveRide(TaxiActiveRideCard active)
            {
                Active = active;
                ActiveVisible = true;
            }

            public void ClearActiveRide()
            {
                Active = null;
                ActiveVisible = false;
            }

            public void SetFeedback(string message) => Feedback = message ?? string.Empty;

            public void ApplyTextDirection(bool isRtl) => Rtl = isRtl;

            public void RaiseAccept(RideOffer offer) => OnAcceptClicked?.Invoke(offer);

            public void RaiseDecline(RideOffer offer) => OnDeclineClicked?.Invoke(offer);

            public void RaiseBoard() => OnBoardClicked?.Invoke();

            public void RaiseCollect() => OnCollectClicked?.Invoke();
        }
    }
}
