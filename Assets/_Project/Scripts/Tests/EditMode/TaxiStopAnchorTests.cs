using System.Collections.Generic;
using NUnit.Framework;
using OutsiderDriver.Navigation;
using OutsiderDriver.World;
using UnityEngine;

namespace OutsiderDriver.Tests.EditMode
{
    public class TaxiStopAnchorTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private static IsometricGroundProjection Projection => IsometricGroundProjection.Default;
        private RoadNetwork _network;
        private CityWorldRegistry _registry;
        private TaxiStopAnchor _stop;
        private Transform _entry, _inFirst, _inSecond, _pose, _outFirst, _outSecond, _exit, _heading;

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void Prepare_CachesBothAuthoredGroundCurvesAndExactStopPose()
        {
            CreateBay();
            Assert.That(_stop.Prepare(_network), Is.True);
            AssertVector(_stop.PositionAtDistance(false, 0f), new Vector2(0f, -1.5f));
            AssertVector(_stop.PositionAtDistance(false, _stop.PullInLength), new Vector2(10f, -5f));
            AssertVector(_stop.PositionAtDistance(true, 0f), _stop.StopGroundPosition);
            AssertVector(_stop.PositionAtDistance(true, _stop.PullOutLength), new Vector2(20f, -1.5f));
            AssertVector(_stop.StopGroundDirection, Vector2.right);
            AssertVector(_stop.TangentAtDistance(false, _stop.PullInLength), Vector2.right);
            AssertVector(_stop.TangentAtDistance(true, 0f), Vector2.right);
            Assert.That(_stop.PullInLength, Is.GreaterThan(10f));
            Assert.That(_stop.PullOutLength, Is.EqualTo(_stop.PullInLength).Within(0.0001f));
            Assert.That(_stop.TryProject(false, _stop.StopGroundPosition, out float distance, out float lateral), Is.True);
            Assert.That(distance, Is.EqualTo(_stop.PullInLength).Within(0.0001f));
            Assert.That(lateral, Is.Zero.Within(0.0001f));
            _pose.position += Vector3.up * 10f;
            AssertVector(_stop.StopGroundPosition, new Vector2(10f, -5f));
        }

        [Test]
        public void Prepare_MissingControlsFailsAndClearsOldCache()
        {
            CreateBay();
            Assert.That(_stop.Prepare(_network), Is.True);
            Configure(entry: null);
            Assert.That(_stop.Validate(_network, 4.5f, 2f, out var diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("Missing"));
            Assert.That(_stop.IsPrepared, Is.False);
            Assert.That(_stop.PullInLength, Is.Zero);
            Assert.That(_stop.TryReserve(new object()), Is.False);
        }

        [Test]
        public void Prepare_RejectsWrongDirectedEdgeForeignNodeAndLocationAccess()
        {
            CreateBay();
            _stop.ApproachFromNode.RemoveConnection(_stop.ApproachNode);
            Assert.That(_stop.Prepare(_network), Is.False);
            _stop.ApproachFromNode.AddConnection(_stop.ApproachNode);
            _stop.Location.AccessRoadNode = _stop.DepartureNode;
            Assert.That(_stop.Prepare(_network), Is.False);
            _stop.Location.AccessRoadNode = _stop.ApproachNode;
            RoadNode foreign = Node("foreign", new Vector2(-20f, 0f), null);
            foreign.AddConnection(_stop.ApproachNode);
            _stop.Configure("stop_a", _stop.Location, foreign, _stop.ApproachNode, _stop.DepartureNode,
                _stop.DepartureNextNode, _entry, _inFirst, _inSecond, _pose, _outFirst, _outSecond, _exit, _heading);
            Assert.That(_stop.Prepare(_network), Is.False);
        }

        [Test]
        public void Prepare_RejectsJunctionEndpointAndElevationMismatch()
        {
            CreateBay();
            _stop.ApproachNode.PieceType = RoadPieceType.TJunction;
            Assert.That(_stop.Prepare(_network), Is.False);
            _stop.ApproachNode.PieceType = RoadPieceType.Straight;
            _stop.DepartureNextNode.ElevationLevel = 1;
            Assert.That(_stop.Prepare(_network), Is.False);
        }

        [Test]
        public void Prepare_RejectsCurveBodyInsideAnyJunctionConflictArea()
        {
            CreateBay();
            RoadNode conflict = Node("nearby_junction", new Vector2(10f, 3f), _network.transform);
            conflict.PieceType = RoadPieceType.TJunction;
            conflict.IntersectionClearanceRadius = 6f;
            _network.RebuildNetwork();
            Assert.That(_stop.Validate(_network, 4.5f, 2f, out var diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("junction"));
            conflict.ElevationLevel = 1;
            Assert.That(_stop.Prepare(_network), Is.True, "A visual crossing on a different level is not a conflict.");
        }

        [Test]
        public void Prepare_RejectsStoppedBodyWithinThroughLaneAndAcceptsOffLaneBay()
        {
            CreateBay();
            Assert.That(_stop.Prepare(_network), Is.True);
            _pose.position = Projection.GroundToWorld(new Vector2(10f, -3f));
            _inSecond.position = Projection.GroundToWorld(new Vector2(7f, -3f));
            _outFirst.position = Projection.GroundToWorld(new Vector2(13f, -3f));
            _heading.position = Projection.GroundToWorld(new Vector2(11f, -3f));
            Assert.That(_stop.Validate(_network, 4.5f, 2f, out var diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("through lane"));
        }

        [TestCase(6f, 2f)]
        [TestCase(4.5f, 2.2f)]
        [TestCase(0f, 2f)]
        [TestCase(float.NaN, 2f)]
        public void Prepare_RejectsVehicleExceedingCapacityOrInvalidDimensions(float length, float width)
        {
            CreateBay();
            Assert.That(_stop.Prepare(_network, length, width), Is.False);
        }

        [Test]
        public void RejectedOversizedVisitor_PreservesPreparedGeometryAndExistingLease()
        {
            CreateBay();
            object owner = new object(), visitor = new object();
            Assert.That(_stop.Prepare(_network, 4.5f, 2f), Is.True);
            Assert.That(_stop.TryReserve(owner), Is.True);
            float inLength = _stop.PullInLength, outLength = _stop.PullOutLength;
            Vector2 midpoint = _stop.PositionAtDistance(false, inLength / 2f);
            Assert.That(_stop.Validate(_network, 100f, 2f, out _), Is.False);
            Assert.That(_stop.Prepare(_network, 4.5f, 100f), Is.False);
            Assert.That(_stop.IsPrepared, Is.True);
            Assert.That(_stop.PullInLength, Is.EqualTo(inLength));
            Assert.That(_stop.PullOutLength, Is.EqualTo(outLength));
            AssertVector(_stop.PositionAtDistance(false, inLength / 2f), midpoint);
            AssertVector(_stop.StopGroundPosition, new Vector2(10f, -5f));
            Assert.That(_stop.IsAvailableFor(owner), Is.True);
            Assert.That(_stop.TryReserve(visitor), Is.False);
        }

        [Test]
        public void LargerVisitorWithinCapacity_ButInsideConflictMargin_PreservesSmallerOwnerSnapshot()
        {
            CreateBay();
            RoadNode conflict = Node("nearby_junction", new Vector2(10f, -14f), _network.transform);
            conflict.PieceType = RoadPieceType.TJunction;
            conflict.IntersectionClearanceRadius = 6f;
            _network.RebuildNetwork();
            object owner = new object();
            Assert.That(_stop.Prepare(_network, 4.5f, 2f), Is.True);
            Assert.That(_stop.TryReserve(owner), Is.True);
            float length = _stop.PullOutLength;
            Vector2 midpoint = _stop.PositionAtDistance(true, length / 2f);
            Assert.That(_stop.Validate(_network, 5.4f, 2f, out var diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("junction"));
            Assert.That(_stop.IsPrepared, Is.True);
            Assert.That(_stop.PullOutLength, Is.EqualTo(length));
            AssertVector(_stop.PositionAtDistance(true, length / 2f), midpoint);
            Assert.That(_stop.IsAvailableFor(owner), Is.True);
            Assert.That(_stop.TryReserve(new object()), Is.False);
        }

        [Test]
        public void WrongNetworkVisitor_PreservesPreparedOwnerSnapshot()
        {
            CreateBay();
            Assert.That(_stop.Prepare(_network), Is.True);
            object owner = new object();
            Assert.That(_stop.TryReserve(owner), Is.True);
            RoadNetwork foreignNetwork = NewObject("foreign_network").AddComponent<RoadNetwork>();
            Assert.That(_stop.Prepare(foreignNetwork), Is.False);
            Assert.That(_stop.Prepare(null), Is.False);
            Assert.That(_stop.IsPrepared, Is.True);
            Assert.That(_stop.IsAvailableFor(owner), Is.True);
            AssertVector(_stop.StopGroundPosition, new Vector2(10f, -5f));
        }

        [Test]
        public void DestroyedAuthoredControl_FailsClosedEvenForRejectedVisitor()
        {
            CreateBay();
            Assert.That(_stop.Prepare(_network), Is.True);
            Object.DestroyImmediate(_inFirst.gameObject);
            Assert.That(_stop.Validate(_network, 100f, 2f, out _), Is.False);
            Assert.That(_stop.IsPrepared, Is.False);
            Assert.That(_stop.PullInLength, Is.Zero);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SignalControlledStraightEndpoint_IsNeverEligible(bool departure, bool disabled)
        {
            CreateBay();
            var signal = NewObject("signal", _registry.transform).AddComponent<TrafficSignalController>();
            signal.Initialize(departure ? _stop.DepartureNode : _stop.ApproachNode, null);
            signal.enabled = !disabled;
            Assert.That(_stop.Prepare(_network), Is.False);
            Assert.That(_stop.IsPrepared, Is.False);
        }

        [Test]
        public void SignalControlledStraightNode_ExcludesEntireManoeuvreEvenWhenDisabled()
        {
            CreateBay();
            RoadNode conflict = Node("controlled_straight", new Vector2(10f, 3f), _network.transform);
            _network.RebuildNetwork();
            var signal = NewObject("signal", _registry.transform).AddComponent<TrafficSignalController>();
            signal.Initialize(conflict, null);
            signal.enabled = false;
            Assert.That(conflict.IsIntersection, Is.False);
            Assert.That(_stop.Validate(_network, 4.5f, 2f, out var diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("junction"));
        }

        [Test]
        public void OrphanSignalApproach_AlsoMakesStraightEndpointAConflict()
        {
            CreateBay();
            var approach = NewObject("orphan_approach", _registry.transform).AddComponent<TrafficSignalApproach>();
            approach.Configure(null, _stop.ApproachNode, _stop.ApproachFromNode, null, 0);
            approach.enabled = false;
            Assert.That(_stop.Prepare(_network), Is.False);
            Assert.That(_stop.IsPrepared, Is.False);
        }

        [Test]
        public void Prepare_RejectsMisalignedLanePoseHeadingAndDegenerateHandles()
        {
            CreateBay();
            _entry.position = Projection.GroundToWorld(new Vector2(0f, 1.5f));
            Assert.That(_stop.Prepare(_network), Is.False);
            _entry.position = Projection.GroundToWorld(new Vector2(0f, -1.5f));
            _heading.position = Projection.GroundToWorld(new Vector2(9f, -5f));
            Assert.That(_stop.Prepare(_network), Is.False);
            _heading.position = Projection.GroundToWorld(new Vector2(11f, -5f));
            _inFirst.position = _entry.position;
            Assert.That(_stop.Prepare(_network), Is.False);
        }

        [Test]
        public void Prepare_RejectsDuplicateRoadIdentityEvenWhenGetNodeReturnsFirstInstance()
        {
            CreateBay();
            Node(_stop.ApproachNode.NodeId, new Vector2(80f, 0f), _network.transform);
            _network.RebuildNetwork();
            Assert.That(_stop.Prepare(_network), Is.False);
        }

        [Test]
        public void Prepare_RejectsCuspBetweenSamplesAndNonfiniteGeometry()
        {
            CreateBay();
            // End tangents face right, but the collinear cubic reverses twice in its interior.
            _pose.position = Projection.GroundToWorld(new Vector2(10f, -1.5f));
            _inFirst.position = Projection.GroundToWorld(new Vector2(18f, -1.5f));
            _inSecond.position = Projection.GroundToWorld(new Vector2(-9f, -1.5f));
            Assert.That(_stop.Validate(_network, 4.5f, 2f, out var diagnostic), Is.False);
            Assert.That(diagnostic, Does.Contain("cusp"));
            _inFirst.position = Projection.GroundToWorld(new Vector2(3f, -1.5f));
            _inSecond.position = Projection.GroundToWorld(new Vector2(7f, -5f));
            _pose.position = Projection.GroundToWorld(new Vector2(10f, -5f));
            Assert.That(_stop.Prepare(_network, float.PositiveInfinity, 2f), Is.False);
        }

        [Test]
        public void Lease_IsExclusiveReentrantAndOnlyOwnerCanRelease()
        {
            CreateBay();
            Assert.That(_stop.Prepare(_network), Is.True);
            object first = new object(), other = new object();
            Assert.That(_stop.TryReserve(null), Is.False);
            Assert.That(_stop.TryReserve(first), Is.True);
            Assert.That(_stop.TryReserve(first), Is.True);
            Assert.That(_stop.IsAvailableFor(other), Is.False);
            _stop.Release(other);
            Assert.That(_stop.TryReserve(other), Is.False);
            _stop.Release(first);
            Assert.That(_stop.TryReserve(other), Is.True);
            _stop.enabled = false;
            Assert.That(_stop.IsAvailableFor(other), Is.False);
            _stop.enabled = true;
            Assert.That(_stop.Prepare(_network), Is.True);
            Assert.That(_stop.TryReserve(first), Is.False, "Disabling authoring cannot release another vehicle's physical bay lease.");
        }

        [Test]
        public void Registry_FiltersDuplicatesDisabledAndForeignStopsButRetainsDiagnostics()
        {
            CreateBay();
            _registry.RebuildRegistry();
            Assert.That(_registry.GetTaxiStop("stop_a"), Is.SameAs(_stop));
            Assert.That(_registry.GetTaxiStopForLocation("location_a"), Is.SameAs(_stop));
            Assert.That(_registry.GetTaxiStopForAccessNode(_stop.ApproachNode), Is.SameAs(_stop));
            TaxiStopAnchor foreign = NewObject("foreign_stop").AddComponent<TaxiStopAnchor>();
            foreign.Configure("foreign", null, null, null, null, null, null, null, null, null, null, null, null, null);
            TaxiStopAnchor duplicate = NewObject("duplicate", _registry.transform).AddComponent<TaxiStopAnchor>();
            duplicate.Configure("stop_a", null, null, null, null, null, null, null, null, null, null, null, null, null);
            duplicate.enabled = false;
            _registry.RebuildRegistry();
            Assert.That(_registry.AllTaxiStops.Count, Is.EqualTo(2));
            Assert.That(_registry.GetTaxiStop("stop_a"), Is.Null);
            Assert.That(_stop.Prepare(_network), Is.False, "Disabled duplicate identity still fails closed.");
            Assert.That(_registry.ValidateWorldData(out var issues), Is.False);
            Assert.That(issues.Exists(issue => issue.Contains("duplicated")), Is.True);
            Object.DestroyImmediate(duplicate.gameObject);
            _registry.RebuildRegistry();
            Assert.That(_registry.GetTaxiStop("stop_a"), Is.SameAs(_stop));
            _stop.enabled = false;
            Assert.That(_registry.GetTaxiStopForLocation("location_a"), Is.Null);
        }

        [Test]
        public void Registry_AmbiguousLocationOrAccessLookupFailsClosed()
        {
            CreateBay();
            TaxiStopAnchor second = NewObject("second_stop", _registry.transform).AddComponent<TaxiStopAnchor>();
            second.Configure("stop_b", _stop.Location, _stop.ApproachFromNode, _stop.ApproachNode,
                _stop.DepartureNode, _stop.DepartureNextNode, _entry, _inFirst, _inSecond, _pose,
                _outFirst, _outSecond, _exit, _heading);
            _registry.RebuildRegistry();
            Assert.That(_registry.GetTaxiStop("stop_a"), Is.SameAs(_stop));
            Assert.That(_registry.GetTaxiStop("stop_b"), Is.SameAs(second));
            Assert.That(_registry.GetTaxiStopForLocation("location_a"), Is.Null);
            Assert.That(_registry.GetTaxiStopForAccessNode(_stop.ApproachNode), Is.Null);
        }

        [Test]
        public void Registry_RejectsForeignLocationEvenWhenGlobalLegacyLookupCanSeeIt()
        {
            CreateBay();
            _stop.Location.transform.SetParent(null, true);
            _registry.RebuildRegistry();
            Assert.That(_registry.TryGetLocation("location_a", out var legacyLocation), Is.True);
            Assert.That(legacyLocation, Is.SameAs(_stop.Location));
            Assert.That(_stop.IsPrepared, Is.True, "Geometry can be valid independently of registry ownership.");
            Assert.That(_registry.GetTaxiStop("stop_a"), Is.Null);
            Assert.That(_registry.GetTaxiStopForLocation("location_a"), Is.Null);
        }

        [Test]
        public void Registry_NestedDuplicateLocationDoesNotPoisonOuterStopIdentity()
        {
            CreateBay();
            var inner = NewObject("inner_world", _registry.transform).AddComponent<CityWorldRegistry>();
            var location = NewObject("inner_location", inner.transform).AddComponent<CityLocationAnchor>();
            location.Configure(null, _stop.ApproachNode, Vector2Int.zero, Vector2Int.one, "location_a");
            _registry.RebuildRegistry();
            Assert.That(_registry.AllLocations.Count, Is.EqualTo(1));
            Assert.That(_registry.GetTaxiStop("stop_a"), Is.SameAs(_stop));
            Assert.That(_registry.GetTaxiStopForLocation("location_a"), Is.SameAs(_stop));
        }

        private void CreateBay()
        {
            _registry = NewObject("world").AddComponent<CityWorldRegistry>();
            _network = NewObject("network", _registry.transform).AddComponent<RoadNetwork>();
            RoadNode from = Node("from", new Vector2(-20f, 0f), _network.transform);
            RoadNode approach = Node("approach", Vector2.zero, _network.transform);
            RoadNode departure = Node("departure", new Vector2(20f, 0f), _network.transform);
            RoadNode next = Node("next", new Vector2(40f, 0f), _network.transform);
            from.AddConnection(approach);
            approach.AddConnection(departure);
            departure.AddConnection(next);
            CityLocationAnchor location = NewObject("location", _registry.transform).AddComponent<CityLocationAnchor>();
            location.Configure(null, approach, Vector2Int.zero, Vector2Int.one, "location_a");
            _entry = Control("entry", new Vector2(0f, -1.5f));
            _inFirst = Control("in_first", new Vector2(3f, -1.5f));
            _inSecond = Control("in_second", new Vector2(7f, -5f));
            _pose = Control("stop_pose", new Vector2(10f, -5f));
            _outFirst = Control("out_first", new Vector2(13f, -5f));
            _outSecond = Control("out_second", new Vector2(17f, -1.5f));
            _exit = Control("exit", new Vector2(20f, -1.5f));
            _heading = Control("heading", new Vector2(11f, -5f));
            _stop = NewObject("stop", _registry.transform).AddComponent<TaxiStopAnchor>();
            _stop.Configure("stop_a", location, from, approach, departure, next,
                _entry, _inFirst, _inSecond, _pose, _outFirst, _outSecond, _exit, _heading);
            _registry.Configure(_network, null, null);
        }
        private void Configure(Transform entry)
        {
            _stop.Configure(_stop.StopId, _stop.Location, _stop.ApproachFromNode, _stop.ApproachNode,
                _stop.DepartureNode, _stop.DepartureNextNode, entry, _inFirst, _inSecond, _pose,
                _outFirst, _outSecond, _exit, _heading);
        }
        private RoadNode Node(string id, Vector2 ground, Transform parent)
        {
            RoadNode node = NewObject(id, parent).AddComponent<RoadNode>();
            node.Configure(id, laneOffsetMeters: 1.5f);
            node.transform.position = Projection.GroundToWorld(ground);
            return node;
        }
        private Transform Control(string name, Vector2 ground)
        {
            Transform control = NewObject(name, _registry.transform).transform;
            control.position = Projection.GroundToWorld(ground);
            return control;
        }
        private GameObject NewObject(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            if (parent != null) go.transform.SetParent(parent);
            return go;
        }
        private static void AssertVector(Vector2 actual, Vector2 expected) =>
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.0001f));
    }
}
