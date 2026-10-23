using NUnit.Framework;
using UnityEngine;
using Unity.Cinemachine;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Navigation;
using OutsiderDriver.Player;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Tests.EditMode
{
    [TestFixture]
    public class PlayerControllerTests
    {
        private GameObject _rootGo;
        private HomeNeighborhoodLayout _layout;
        private VehicleController _taxi;
        private TrafficDirector _trafficDirector;
        private PlayerController _player;
        private GameObject _driverVisuals;
        private CinemachineCamera _taxiCamera;
        private CinemachineCamera _onFootCamera;
        private TimeService _timeService;

        [SetUp]
        public void SetUp()
        {
            _rootGo = new GameObject("PlayerControllerTestRoot");

            // 1. Setup HomeNeighborhoodLayout with HomeInterior and NeighborhoodSidewalk zones
            var layoutGo = new GameObject("HomeNeighborhoodLayout");
            layoutGo.transform.SetParent(_rootGo.transform, false);
            _layout = layoutGo.AddComponent<HomeNeighborhoodLayout>();
            _layout.EnsureIsometricGridConfigured();

            var homeZoneGo = new GameObject("Zone_HomeInterior");
            homeZoneGo.transform.SetParent(layoutGo.transform, false);
            homeZoneGo.transform.position = new Vector3(-5f, 3f, 0f);
            var homeZone = homeZoneGo.AddComponent<WalkableZone>();
            homeZone.Configure("zone_home", "Apartment Interior", WalkableZoneType.HomeInterior, new Rect(-2f, -2f, 4f, 4f));

            var sidewalkZoneGo = new GameObject("Zone_Sidewalk");
            sidewalkZoneGo.transform.SetParent(layoutGo.transform, false);
            sidewalkZoneGo.transform.position = new Vector3(0f, 0f, 0f);
            var sidewalkZone = sidewalkZoneGo.AddComponent<WalkableZone>();
            sidewalkZone.Configure("zone_sidewalk", "Starter Sidewalk", WalkableZoneType.NeighborhoodSidewalk, new Rect(-3f, -2f, 6f, 4f));

            // Anchors & Interactables
            var bedGo = new GameObject("HomeBed");
            bedGo.transform.SetParent(layoutGo.transform, false);
            bedGo.transform.position = new Vector3(-5.5f, 3.2f, 0f);
            var bedInteractable = bedGo.AddComponent<NeighborhoodInteractable>();
            bedInteractable.Configure("interact_bed", "Rest", InteractableKind.HomeBed, 1.25f);

            var doorGo = new GameObject("ApartmentDoor");
            doorGo.transform.SetParent(layoutGo.transform, false);
            doorGo.transform.position = new Vector3(-2f, 1f, 0f);
            var doorInteractable = doorGo.AddComponent<NeighborhoodInteractable>();
            doorInteractable.Configure("interact_door", "Doorway", InteractableKind.DoorwayThreshold, 1.25f);

            var bayGo = new GameObject("ParkedTaxiBay");
            bayGo.transform.SetParent(layoutGo.transform, false);
            bayGo.transform.position = new Vector3(0f, 0f, 0f);
            var bayInteractable = bayGo.AddComponent<NeighborhoodInteractable>();
            bayInteractable.Configure("interact_taxi_bay", "Board Taxi", InteractableKind.ParkedTaxiBay, 1.25f);

            _layout.AssignAnchors(bedGo.transform, doorGo.transform, bayGo.transform);
            _layout.RebuildLayoutRegistry();

            // 2. Setup Taxi, TrafficDirector, and TimeService
            var taxiGo = new GameObject("Taxi");
            taxiGo.transform.SetParent(_rootGo.transform, false);
            taxiGo.transform.position = new Vector3(0f, 0f, 0f);
            _taxi = taxiGo.AddComponent<VehicleController>();

            var trafficGo = new GameObject("TrafficDirector");
            trafficGo.transform.SetParent(_rootGo.transform, false);
            _trafficDirector = trafficGo.AddComponent<TrafficDirector>();

            _timeService = new TimeService();
            _timeService.SetTime(new WorldTime(1, 8, 0));

            // 3. Setup Cinemachine 3 Virtual Cameras
            var taxiCamGo = new GameObject("TaxiVCam");
            taxiCamGo.transform.SetParent(_rootGo.transform, false);
            _taxiCamera = taxiCamGo.AddComponent<CinemachineCamera>();

            var onFootCamGo = new GameObject("OnFootVCam");
            onFootCamGo.transform.SetParent(_rootGo.transform, false);
            _onFootCamera = onFootCamGo.AddComponent<CinemachineCamera>();

            // 4. Setup Driver PlayerController
            var driverGo = new GameObject("Driver_OnFoot");
            driverGo.transform.SetParent(_rootGo.transform, false);
            _player = driverGo.AddComponent<PlayerController>();

            _driverVisuals = new GameObject("Visuals");
            _driverVisuals.transform.SetParent(driverGo.transform, false);

            _player.Construct(_layout, _taxi, _trafficDirector, _timeService);
            _player.AssignVisualsRoot(_driverVisuals);
            _player.AssignCameras(_taxiCamera, _onFootCamera);
            _player.SetModeImmediate(DriverMode.InTaxi);
        }

        [TearDown]
        public void TearDown()
        {
            VehicleController.ClearActiveVehicles();
            if (_rootGo != null)
            {
                Object.DestroyImmediate(_rootGo);
            }
        }

        [Test]
        public void TryExitAndBoardTaxi_SwitchesModeCamerasVisualsAndTrafficBubble()
        {
            Assert.AreEqual(DriverMode.InTaxi, _player.CurrentMode);
            Assert.IsFalse(_driverVisuals.activeSelf);
            Assert.AreEqual(PlayerController.ActiveCameraPriority, _taxiCamera.Priority.Value);
            Assert.AreEqual(PlayerController.InactiveCameraPriority, _onFootCamera.Priority.Value);
            Assert.AreEqual(_taxi.transform, _trafficDirector.BubbleCenter);

            // Exit the parked taxi at (0, 0)
            bool exited = _player.TryExitTaxi();
            Assert.IsTrue(exited);
            Assert.AreEqual(DriverMode.OnFoot, _player.CurrentMode);
            Assert.IsTrue(_driverVisuals.activeSelf);
            Assert.AreEqual(PlayerController.InactiveCameraPriority, _taxiCamera.Priority.Value);
            Assert.AreEqual(PlayerController.ActiveCameraPriority, _onFootCamera.Priority.Value);
            Assert.AreEqual(_player.transform, _trafficDirector.BubbleCenter);
            Assert.IsNotNull(_player.CurrentWalkableZone);
            Assert.AreEqual("zone_sidewalk", _player.CurrentWalkableZone.ZoneId);

            // Board the taxi again
            bool boarded = _player.TryBoardTaxi();
            Assert.IsTrue(boarded);
            Assert.AreEqual(DriverMode.InTaxi, _player.CurrentMode);
            Assert.IsFalse(_driverVisuals.activeSelf);
            Assert.AreEqual(PlayerController.ActiveCameraPriority, _taxiCamera.Priority.Value);
            Assert.AreEqual(PlayerController.InactiveCameraPriority, _onFootCamera.Priority.Value);
            Assert.AreEqual(_taxi.transform, _trafficDirector.BubbleCenter);
        }

        [Test]
        public void TryExitTaxi_WhenTaxiOutsideWalkableNeighborhood_RejectsExit()
        {
            // Move taxi far out into the city road network away from any WalkableZone (Decision P-009)
            _taxi.transform.position = new Vector3(45f, 25f, 0f);

            bool exited = _player.TryExitTaxi();
            Assert.IsFalse(exited, "Driver must not exit on foot outside authorized WalkableZones (Decision P-009).");
            Assert.AreEqual(DriverMode.InTaxi, _player.CurrentMode);
        }

        [Test]
        public void TickMovement_ClampsToAuthorizedWalkableZoneAndUpdatesIsometricFacing()
        {
            _player.TryExitTaxi();
            Assert.AreEqual(DriverMode.OnFoot, _player.CurrentMode);

            // Move NorthWest (-X, +Y) inside the sidewalk zone (-3..3, -2..2)
            _player.TickMovement(new Vector2(-1f, 0.5f), 0.5f);
            Assert.AreEqual(IsometricDirection.NorthWest, _player.CurrentFacing);
            Assert.Less(_player.Position2D.x, 0f);
            Assert.Greater(_player.Position2D.y, 0f);

            // Attempt to walk far East past the sidewalk boundary (x > 3)
            for (int i = 0; i < 30; i++)
            {
                _player.TickMovement(new Vector2(1f, 0f), 0.2f);
            }

            Assert.AreEqual(IsometricDirection.NorthEast, _player.CurrentFacing);
            Assert.LessOrEqual(_player.Position2D.x, 3f, "Player position must remain clamped within the WalkableZone.");
            Assert.IsTrue(_layout.CanWalkAt(_player.Position2D, out _));
        }

        [Test]
        public void DoorwayAndHomeBedInteractions_TransitionBetweenZonesAndAdvanceWorldClock()
        {
            // Place driver on foot right at the apartment doorway on the sidewalk
            _player.SetModeImmediate(DriverMode.OnFoot, new Vector2(-2f, 1f));
            Assert.AreEqual(WalkableZoneType.NeighborhoodSidewalk, _player.CurrentWalkableZone.ZoneType);
            Assert.IsNotNull(_player.NearestInteractable);
            Assert.AreEqual(InteractableKind.DoorwayThreshold, _player.NearestInteractable.Kind);

            // Interact with doorway -> transitions inside HomeInterior at HomeBed spawn anchor (-5.5, 3.2)
            bool enteredHome = _player.TryInteractWithNearest();
            Assert.IsTrue(enteredHome);
            Assert.IsNotNull(_player.CurrentWalkableZone);
            Assert.AreEqual(WalkableZoneType.HomeInterior, _player.CurrentWalkableZone.ZoneType);
            Assert.AreEqual(new Vector2(-5.5f, 3.2f), _player.Position2D);

            // Now standing next to HomeBed -> interact to rest for 6 hours (360 minutes)
            WorldTime beforeRest = _timeService.CurrentTime;
            Assert.AreEqual(8, beforeRest.Hour);

            bool rested = _player.TryInteractWithNearest();
            Assert.IsTrue(rested);
            Assert.AreEqual(14, _timeService.CurrentTime.Hour);
        }
    }
}
