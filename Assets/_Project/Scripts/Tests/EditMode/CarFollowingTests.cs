using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.EditMode
{
    public class CarFollowingTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private RoadNetwork _network;
        private RoadNode _start, _end;
        private const float Dt = 0.02f;

        [SetUp]
        public void SetUp()
        {
            VehicleController.ClearActiveVehicles();
            _network = ObjectAt("network", 0, 0).AddComponent<RoadNetwork>();
            _start = Node("start", 0, 0);
            _end = Node("end", 1000, 0);
            _start.AddConnection(_end);
            _network.RebuildNetwork();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) Object.DestroyImmediate(go);
            _objects.Clear();
            VehicleController.ClearActiveVehicles();
        }

        [Test]
        public void FasterFollower_SettlesBehindSlowLeaderWithoutContact()
        {
            var lead = Car("lead", 25); lead.MaxSpeed = 3;
            var follower = Car("follower", 0);
            for (int i = 0; i < 1500; i++)
            {
                lead.TickMovement(Dt); follower.TickMovement(Dt);
                Assert.GreaterOrEqual(X(lead) - X(follower), 6.4f);
            }
            Assert.That(follower.CurrentSpeed, Is.EqualTo(3).Within(0.15f));
            Assert.That(X(lead) - X(follower), Is.InRange(9f, 11f));
        }

        [TestCase(0.02f)]
        [TestCase(0.1f)]
        public void StationaryLeader_ProgressiveBrakingAndSmoothRestart(float dt)
        {
            var lead = Car("lead", 50); lead.Stop();
            var follower = Car("follower", 0);
            bool brakedWhileMoving = false;
            float previous = 0;
            for (int i = 0; i < Mathf.CeilToInt(30 / dt); i++)
            {
                follower.TickMovement(dt);
                if (follower.CurrentSpeed < previous && follower.CurrentSpeed > 1) brakedWhileMoving = true;
                Assert.GreaterOrEqual(X(lead) - X(follower), 6.4f);
                previous = follower.CurrentSpeed;
            }
            Assert.IsTrue(brakedWhileMoving);
            Assert.Less(follower.CurrentSpeed, 0.05f);
            lead.Resume();
            for (int i = 0; i < Mathf.CeilToInt(10 / dt); i++)
            {
                lead.TickMovement(dt); follower.TickMovement(dt);
                Assert.LessOrEqual(follower.CurrentSpeed - previous, follower.Acceleration * dt + 0.001f);
                previous = follower.CurrentSpeed;
            }
            Assert.Greater(follower.CurrentSpeed, 5);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThreeCarQueue_StopsAndDrainsInEitherTickOrder(bool reverse)
        {
            var cars = new[] { Car("front", 50), Car("middle", 25), Car("rear", 0) };
            cars[0].Stop();
            for (int i = 0; i < 2000; i++)
            {
                if (i == 1000) cars[0].Resume();
                for (int j = 0; j < cars.Length; j++) cars[reverse ? 2 - j : j].TickMovement(Dt);
                Assert.GreaterOrEqual(X(cars[0]) - X(cars[1]), 6.4f);
                Assert.GreaterOrEqual(X(cars[1]) - X(cars[2]), 6.4f);
                if (i == 999) Assert.Less(cars[2].CurrentSpeed, 0.05f);
            }
            Assert.Greater(cars[2].CurrentSpeed, 5);
        }

        [TestCase(3, 0)]
        [TestCase(0, 1)]
        public void AdjacentLaneOrOtherElevation_DoesNotBlock(float lateral, int elevation)
        {
            var lead = Car("other", 8, lateral); lead.Stop();
            var otherStart = Node("otherStart", 0, lateral); otherStart.ElevationLevel = elevation;
            var otherEnd = Node("otherEnd", 1000, lateral); otherEnd.ElevationLevel = elevation;
            lead.NavigateAlongPath(new[] { otherEnd }); lead.Stop();
            var follower = Car("follower", 0);
            for (int i = 0; i < 200; i++) follower.TickMovement(Dt);
            Assert.That(follower.CurrentSpeed, Is.EqualTo(follower.MaxSpeed).Within(0.01f));
        }

        [Test]
        public void ReservedApproach_BlockedQueueReleasesAndRecovers()
        {
            var junction = Node("junction", 10, 0);
            junction.Priority = RoadNodePriority.YieldIntersection;
            junction.IntersectionClearanceRadius = 9;
            var follower = Car("follower", 3);
            follower.NavigateAlongPath(new[] { junction, _end });
            follower.TickMovement(Dt);
            Assert.AreSame(follower, junction.OccupyingVehicle);
            var lead = Car("parked", 10); lead.CancelRoute();
            follower.TickMovement(Dt);
            Assert.IsNull(junction.OccupyingVehicle);
            lead.gameObject.SetActive(false);
            for (int i = 0; i < 500; i++) follower.TickMovement(Dt);
            Assert.Greater(X(follower), 20);
            Assert.IsNull(junction.OccupyingVehicle);
        }

        [Test]
        public void LeaderAroundCorner_IsSeenAlongRouteRatherThanForwardCone()
        {
            var corner = Node("corner", 20, 0);
            var end = Node("turnEnd", 20, 100);
            var follower = Car("follower", 15);
            follower.ForwardSensorDistance = 30;
            follower.NavigateAlongPath(new[] { corner, end });
            var lead = Car("parkedAroundCorner", 20, 5); lead.CancelRoute();
            follower.TickMovement(Dt);
            Assert.IsTrue(follower.IsYielding);
        }

        [Test]
        public void SpeedPolicy_UsesLeaderBrakingCapabilityAndPositiveGap()
        {
            float hardBrakingLeader = CarFollowing.TargetSpeed(20, 12, 8, 10, 5, 2, 1.2f);
            float gentleBrakingLeader = CarFollowing.TargetSpeed(20, 12, 8, 2, 5, 2, 1.2f);
            Assert.Less(hardBrakingLeader, gentleBrakingLeader);
            Assert.AreEqual(0, CarFollowing.TargetSpeed(9, 1, 0, 5, 5, 2, 1.2f));
        }

        [Test]
        public void ParkedCarBeyondDestination_PreventsTerminalOverlap()
        {
            var terminal = Node("terminal", 10, 0);
            var parked = Car("parked", 12); parked.CancelRoute();
            var follower = Car("follower", 0); follower.NavigateAlongPath(new[] { terminal });
            for (int i = 0; i < 1000; i++) follower.TickMovement(Dt);
            Assert.GreaterOrEqual(X(parked) - X(follower), 6.4f);
            Assert.IsTrue(follower.HasActiveRoute);
        }

        [Test]
        public void CancelledElevatedLeader_RemainsAnObstacleOnItsLevel()
        {
            var elevatedEnd = Node("upperEnd", 1000, 0); elevatedEnd.ElevationLevel = 1;
            var parked = Car("parked", 8); parked.NavigateAlongPath(new[] { elevatedEnd }); parked.CancelRoute();
            var follower = Car("follower", 0); follower.NavigateAlongPath(new[] { elevatedEnd });
            for (int i = 0; i < 500; i++) follower.TickMovement(Dt);
            Assert.GreaterOrEqual(X(parked) - X(follower), 6.4f);
        }

        [Test]
        public void MovingQueue_CannotAdvanceThroughAnUnreservedJunction()
        {
            var junction = Node("junction", 10, 0); junction.Priority = RoadNodePriority.YieldIntersection;
            var parked = Car("queueHead", 20); parked.Stop();
            var follower = Car("follower", 0); follower.NavigateAlongPath(new[] { junction, _end });
            for (int i = 0; i < 500; i++) follower.TickMovement(Dt);
            Assert.AreSame(junction, follower.CurrentTargetNode);
            Assert.IsNull(junction.OccupyingVehicle);
            parked.gameObject.SetActive(false);
            for (int i = 0; i < 500; i++) follower.TickMovement(Dt);
            Assert.Greater(X(follower), 20);
        }

        [TestCase(-1.5f, false)]
        [TestCase(1.5f, true)]
        public void OffsetCorner_UsesOutgoingLaneRatherThanCentrelineChord(float laneX, bool blocked)
        {
            var corner = Node("corner", 0, 0); corner.LaneOffsetMeters = 1.5f;
            var end = Node("north", 0, 100); end.LaneOffsetMeters = 1.5f;
            var follower = Car("follower", -10, -1.5f);
            follower.NavigateAlongPath(new[] { corner, end }); follower.ForwardSensorDistance = 30;
            var parked = Car("parked", laneX, 3); parked.CancelRoute();
            follower.TickMovement(Dt);
            Assert.AreEqual(blocked, follower.IsYielding);
        }

        private float X(VehicleController car) => _network.GroundProjection.WorldToGround(car.transform.position).x;
        private GameObject ObjectAt(string name, float x, float y)
        {
            var go = new GameObject(name); _objects.Add(go);
            go.transform.position = (Vector3)IsometricGroundProjection.Default.GroundToWorld(new Vector2(x, y));
            return go;
        }
        private RoadNode Node(string name, float x, float y)
        {
            var node = ObjectAt(name, x, y).AddComponent<RoadNode>();
            node.transform.SetParent(_network.transform); return node;
        }
        private VehicleController Car(string name, float x, float y = 0)
        {
            var car = ObjectAt(name, x, y).AddComponent<VehicleController>();
            car.Construct(_network); car.NavigateAlongPath(new[] { _end }); return car;
        }
    }
}
