using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using OutsiderDriver.Core;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Navigation;
using OutsiderDriver.Rides;
using OutsiderDriver.UI.TaxiShift;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.PlayMode
{
    public class TaxiShiftAcceptancePlayModeTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/OutsiderCity.unity";

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            var persistentScopes = Object.FindObjectsByType<GameLifetimeScope>();
            for (int i = 0; i < persistentScopes.Length; i++)
            {
                if (persistentScopes[i] != null)
                {
                    Object.DestroyImmediate(persistentScopes[i].gameObject);
                }
            }

            var trafficDirectors = Object.FindObjectsByType<TrafficDirector>();
            for (int i = 0; i < trafficDirectors.Length; i++)
            {
                if (trafficDirectors[i] != null)
                {
                    trafficDirectors[i].DespawnAll();
                    Object.DestroyImmediate(trafficDirectors[i].gameObject);
                }
            }

            var vehicles = Object.FindObjectsByType<VehicleController>();
            for (int i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i] != null)
                {
                    Object.DestroyImmediate(vehicles[i].gameObject);
                }
            }

            VehicleController.ClearActiveVehicles();
        }

        [UnityTest]
        public IEnumerator ProductionScene_TwoConsecutiveSystemicRides_DeclineAndNoWorkRecovery_SucceedViaLiveHud()
        {
            Time.timeScale = 1f;

            var loadOp = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.IsNotNull(loadOp, "OutsiderCity scene must load in PlayMode.");
            while (!loadOp.isDone)
            {
                yield return null;
            }

            // Let VContainer scopes and IStartable entry points initialize
            yield return null;
            yield return null;

            var sceneScope = Object.FindAnyObjectByType<CitySceneLifetimeScope>();
            Assert.IsNotNull(sceneScope, "CitySceneLifetimeScope must be active in OutsiderCity.");

            var demandService = sceneScope.Container.Resolve<IRideDemandService>();
            var rideService = sceneScope.Container.Resolve<IRideLifecycleService>();
            var playerState = sceneScope.Container.Resolve<IPlayerStateService>();
            var timeService = sceneScope.Container.Resolve<ITimeService>();
            var roadNetwork = sceneScope.Container.Resolve<RoadNetwork>();
            var playerTaxi = sceneScope.Container.Resolve<VehicleController>();
            var taxiView = Object.FindAnyObjectByType<TaxiShiftView>();
            var trafficDirector = Object.FindAnyObjectByType<TrafficDirector>();

            Assert.IsNotNull(demandService);
            Assert.IsNotNull(rideService);
            Assert.IsNotNull(playerState);
            Assert.IsNotNull(timeService);
            Assert.IsNotNull(roadNetwork);
            Assert.IsNotNull(playerTaxi);
            Assert.IsNotNull(taxiView);
            Assert.IsNotNull(trafficDirector);

            var uiDocument = taxiView.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument);
            var root = uiDocument.rootVisualElement;
            Assert.IsNotNull(root);

            // Set world time to 12:00 (Day 1) where three distinct residents (resident_001, resident_002, resident_004) are active
            timeService.SetTime(new WorldTime(day: 1, hour: 12, minute: 0));
            yield return null;

            var initialOffers = demandService.GetEligibleOffers();
            Assert.AreEqual(3, initialOffers.Count, "At 12:00, three systemic ride offers must be on the board.");
            Assert.AreEqual(RidePhase.Idle, rideService.CurrentPhase);

            var offerIds = new HashSet<string>();
            for (int i = 0; i < initialOffers.Count; i++)
            {
                Assert.IsTrue(initialOffers[i].OfferId.StartsWith("offer_"), "Offer IDs must come from the systemic demand generator.");
                Assert.IsTrue(offerIds.Add(initialOffers[i].OfferId), "Each displayed offer must have a unique ID.");
                Assert.AreNotEqual(initialOffers[i].PickupNode, initialOffers[i].DestinationNode);
                Assert.GreaterOrEqual(roadNetwork.FindPath(initialOffers[i].PickupNode, initialOffers[i].DestinationNode).Count, 2);
            }

            // 1. Test Decline via real UI Toolkit button click
            string declinedOfferId = initialOffers[0].OfferId;
            string declinedPassengerId = initialOffers[0].Passenger.PassengerId;
            ClickButton(root, "taxi-offer-0-decline");
            yield return null;

            var afterDeclineOffers = demandService.GetEligibleOffers();
            Assert.AreEqual(2, afterDeclineOffers.Count, "Declining one offer must remove only that offer and not immediately regenerate.");
            for (int i = 0; i < afterDeclineOffers.Count; i++)
            {
                Assert.AreNotEqual(declinedOfferId, afterDeclineOffers[i].OfferId);
                Assert.AreNotEqual(declinedPassengerId, afterDeclineOffers[i].Passenger.PassengerId);
            }

            //Speed up taxi slightly for fast PlayMode test execution while keeping real frame-by-frame Update() navigation
            float originalMaxSpeed = playerTaxi.MaxSpeed;
            float originalAccel = playerTaxi.Acceleration;
            playerTaxi.MaxSpeed = 28f;
            playerTaxi.Acceleration = 40f;

            try
            {
                // 2. First Full Ride: Accept -> EnRoute -> Pickup -> Board -> InTransit -> Arrival -> Collect
                var firstOffer = afterDeclineOffers[0];
                int creditsBeforeFirst = playerState.Credits;

                ClickButton(root, "taxi-offer-0-accept");
                yield return null;

                Assert.IsTrue(
                    rideService.CurrentPhase == RidePhase.EnRouteToPickup || rideService.CurrentPhase == RidePhase.AwaitingBoarding,
                    $"Accepting offer must transition to EnRouteToPickup or AwaitingBoarding, was {rideService.CurrentPhase}.");

                if (rideService.CurrentPhase == RidePhase.EnRouteToPickup)
                {
                    yield return WaitForPhase(rideService, RidePhase.AwaitingBoarding, timeoutSeconds: 15f);
                }

                Assert.AreEqual(RidePhase.AwaitingBoarding, rideService.CurrentPhase, "Taxi must reach pickup and await player boarding action.");
                var boardButton = root.Q<Button>("taxi-board");
                Assert.IsNotNull(boardButton);
                Assert.AreEqual(DisplayStyle.Flex, boardButton.resolvedStyle.display, "Board button must be visible at pickup.");

                ClickButton(root, "taxi-board");
                yield return null;

                Assert.AreEqual(RidePhase.InTransit, rideService.CurrentPhase, "Clicking Board must start transit to destination.");
                Assert.IsTrue(playerTaxi.HasActiveRoute, "Player taxi must have an active route to destination.");

                yield return WaitForPhase(rideService, RidePhase.ArrivedAtDestination, timeoutSeconds: 15f);
                Assert.AreEqual(RidePhase.ArrivedAtDestination, rideService.CurrentPhase, "Taxi must reach destination and wait for fare collection.");
                Assert.AreEqual(creditsBeforeFirst, playerState.Credits, "Credits must not update before clicking Collect.");

                var collectButton = root.Q<Button>("taxi-collect");
                Assert.IsNotNull(collectButton);
                Assert.AreEqual(DisplayStyle.Flex, collectButton.resolvedStyle.display, "Collect button must be visible upon arrival.");

                ClickButton(root, "taxi-collect");
                yield return null;

                Assert.AreEqual(RidePhase.Idle, rideService.CurrentPhase, "Collecting fare must return ride lifecycle to Idle.");
                int expectedFirstTip = Mathf.RoundToInt(firstOffer.QuotedFare * firstOffer.Passenger.TipGenerosity);
                int expectedFirstTotal = firstOffer.QuotedFare + expectedFirstTip;
                Assert.AreEqual(creditsBeforeFirst + expectedFirstTotal, playerState.Credits, "Player credits must increase by quoted base fare + tip.");

                var feedbackLabel = root.Q<Label>("taxi-feedback");
                Assert.IsNotNull(feedbackLabel);
                StringAssert.Contains(firstOffer.QuotedFare.ToString(), feedbackLabel.text, "Settlement feedback must show base fare.");
                if (expectedFirstTip > 0)
                {
                    StringAssert.Contains(expectedFirstTip.ToString(), feedbackLabel.text, "Settlement feedback must show tip.");
                }

                // 3. Second Consecutive Ride without restarting scene
                var secondBoardOffers = demandService.GetEligibleOffers();
                Assert.GreaterOrEqual(secondBoardOffers.Count, 1, "New work must be available immediately after completing the first ride.");

                // Pick a distinct systemic job
                int secondCardIndex = 0;
                for (int i = 0; i < secondBoardOffers.Count; i++)
                {
                    if (secondBoardOffers[i].OfferId != firstOffer.OfferId)
                    {
                        secondCardIndex = i;
                        break;
                    }
                }

                var secondOffer = secondBoardOffers[secondCardIndex];
                Assert.AreNotEqual(firstOffer.OfferId, secondOffer.OfferId, "Second job must have a new systemic offer identity.");
                int creditsBeforeSecond = playerState.Credits;

                ClickButton(root, $"taxi-offer-{secondCardIndex}-accept");
                yield return null;

                if (rideService.CurrentPhase == RidePhase.EnRouteToPickup)
                {
                    yield return WaitForPhase(rideService, RidePhase.AwaitingBoarding, timeoutSeconds: 15f);
                }

                Assert.AreEqual(RidePhase.AwaitingBoarding, rideService.CurrentPhase);
                ClickButton(root, "taxi-board");
                yield return null;

                Assert.AreEqual(RidePhase.InTransit, rideService.CurrentPhase);
                yield return WaitForPhase(rideService, RidePhase.ArrivedAtDestination, timeoutSeconds: 15f);

                Assert.AreEqual(RidePhase.ArrivedAtDestination, rideService.CurrentPhase);
                ClickButton(root, "taxi-collect");
                yield return null;

                Assert.AreEqual(RidePhase.Idle, rideService.CurrentPhase);
                int expectedSecondTip = Mathf.RoundToInt(secondOffer.QuotedFare * secondOffer.Passenger.TipGenerosity);
                int expectedSecondTotal = secondOffer.QuotedFare + expectedSecondTip;
                Assert.AreEqual(creditsBeforeSecond + expectedSecondTotal, playerState.Credits, "Second ride settlement must accumulate credits accurately.");

                // 4. Verify No-Work condition and recovery when time advances
                timeService.SetTime(new WorldTime(day: 2, hour: 3, minute: 0));
                yield return null;

                Assert.IsEmpty(demandService.GetEligibleOffers(), "At 03:00 no passengers are scheduled.");
                var noWorkCard = root.Q<VisualElement>("taxi-no-work");
                Assert.IsNotNull(noWorkCard);
                Assert.AreEqual(DisplayStyle.Flex, noWorkCard.resolvedStyle.display, "No-work card must be visible when no offers exist.");

                timeService.SetTime(new WorldTime(day: 2, hour: 10, minute: 0));
                yield return null;

                Assert.GreaterOrEqual(demandService.GetEligibleOffers().Count, 2, "Systemic demand must recover automatically when world time enters active hours.");
                Assert.AreEqual(DisplayStyle.None, noWorkCard.resolvedStyle.display, "No-work card must hide when offers return.");

                // 5. Verify ambient traffic is healthy and not deadlocked at intersections
                trafficDirector.EvaluateBubble();
                yield return null;
                var allVehicles = VehicleController.AllActiveVehicles;
                for (int i = 0; i < allVehicles.Count; i++)
                {
                    var v = allVehicles[i];
                    if (v == null || v == playerTaxi) continue;
                    Assert.IsTrue(v.HasActiveRoute, $"Ambient vehicle {v.name} should have an active route.");
                }
            }
            finally
            {
                playerTaxi.MaxSpeed = originalMaxSpeed;
                playerTaxi.Acceleration = originalAccel;
            }
        }

        private static void ClickButton(VisualElement root, string buttonName)
        {
            var button = root.Q<Button>(buttonName);
            Assert.IsNotNull(button, $"Expected UI Toolkit Button '{buttonName}' to exist.");
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

        private static IEnumerator WaitForPhase(IRideLifecycleService rideService, RidePhase targetPhase, float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            while (rideService.CurrentPhase != targetPhase && Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                yield return null;
            }
        }
    }
}
