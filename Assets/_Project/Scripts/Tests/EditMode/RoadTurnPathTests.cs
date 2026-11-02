using System.Collections.Generic;
using NUnit.Framework;
using OutsiderDriver.Navigation;
using UnityEngine;

namespace OutsiderDriver.Tests.EditMode
{
    public class RoadTurnPathTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private static IsometricGroundProjection Projection => IsometricGroundProjection.Default;

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }

        [Test]
        public void Prepare_UsesGroundMetresAndPreservesAuthoredEndpoints()
        {
            RoadTurnPath path = CreateTurn();
            Assert.That(path.Prepare(Projection), Is.True);
            Assert.That(path.IsValid, Is.True);
            AssertVector(path.PositionAtDistance(0f), new Vector2(-5f, -1.5f));
            AssertVector(path.PositionAtDistance(path.Length), new Vector2(1.5f, 5f));
            Assert.That(path.Length, Is.EqualTo(Mathf.PI * 6.5f / 2f).Within(0.02f));
            AssertVector(path.PositionAtDistance(-100f), path.PositionAtDistance(0f));
            AssertVector(path.PositionAtDistance(100f), path.PositionAtDistance(path.Length));
        }

        [Test]
        public void DistanceSampling_MovesMonotonicallyAlongCurveWithContinuousTangent()
        {
            RoadTurnPath path = CreateTurn();
            Assert.That(path.Prepare(Projection), Is.True);
            Vector2 previous = path.PositionAtDistance(0f);
            Vector2 previousTangent = path.TangentAtDistance(0f);
            float measuredLength = 0f;
            for (int i = 1; i <= 100; i++)
            {
                float distance = path.Length * i / 100f;
                Vector2 next = path.PositionAtDistance(distance);
                Vector2 tangent = path.TangentAtDistance(distance);
                Assert.That(next.x, Is.GreaterThanOrEqualTo(previous.x - 0.00001f));
                Assert.That(next.y, Is.GreaterThanOrEqualTo(previous.y - 0.00001f));
                Assert.That(tangent.magnitude, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(Vector2.Dot(previousTangent, tangent), Is.GreaterThan(0.999f));
                measuredLength += Vector2.Distance(previous, next);
                previous = next;
                previousTangent = tangent;
            }
            Assert.That(measuredLength, Is.EqualTo(path.Length).Within(0.01f));
            AssertVector(path.TangentAtDistance(0f), Vector2.right);
            AssertVector(path.TangentAtDistance(path.Length), Vector2.up);
        }

        [Test]
        public void Curvature_LimitsTurnSpeedUsingLateralAcceleration()
        {
            RoadTurnPath turn = CreateTurn();
            Assert.That(turn.Prepare(Projection), Is.True);
            float midpoint = turn.Length / 2f;
            float curvature = turn.CurvatureAtDistance(midpoint);
            Assert.That(curvature, Is.EqualTo(1f / 6.5f).Within(0.005f));
            Assert.That(turn.SpeedLimitAtDistance(midpoint, 2f, 9f),
                Is.EqualTo(Mathf.Sqrt(2f / curvature)).Within(0.0001f));
            Assert.That(turn.SpeedLimitAtDistance(midpoint, 2f, 9f), Is.LessThan(9f));
            Assert.That(turn.SpeedLimitAtDistance(midpoint, 2f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void StraightPath_HasMetreLengthAndNoCurvatureSpeedPenalty()
        {
            RoadNode incoming = Node("incoming", new Vector2(-10f, 0f));
            RoadNode junction = Node("junction", Vector2.zero);
            RoadNode outgoing = Node("outgoing", new Vector2(10f, 0f));
            RoadTurnPath path = Path(incoming, junction, outgoing,
                new Vector2(-5f, -1.5f), new Vector2(-2f, -1.5f),
                new Vector2(2f, -1.5f), new Vector2(5f, -1.5f));
            Assert.That(path.Prepare(Projection), Is.True);
            Assert.That(path.Length, Is.EqualTo(10f).Within(0.0001f));
            AssertVector(path.PositionAtDistance(5f), new Vector2(0f, -1.5f));
            Assert.That(path.TryProject(new Vector2(8f, -1.5f), out float along, out float lateral), Is.True);
            Assert.That(along, Is.EqualTo(path.Length));
            Assert.That(Mathf.Abs(lateral), Is.EqualTo(3f).Within(0.0001f),
                "Endpoint overshoot must not look like an arbitrarily distant in-lane vehicle.");
            Assert.That(path.CurvatureAtDistance(5f), Is.Zero);
            Assert.That(path.SpeedLimitAtDistance(5f, 2f, 9f), Is.EqualTo(9f));
        }

        [Test]
        public void TryProject_ReportsGroundArcDistanceAndSignedLaneDistance()
        {
            RoadTurnPath turn = CreateTurn();
            Assert.That(turn.Prepare(Projection), Is.True);
            float midpoint = turn.Length / 2f;
            Vector2 tangent = turn.TangentAtDistance(midpoint);
            Vector2 right = new Vector2(tangent.y, -tangent.x);
            Vector2 point = turn.PositionAtDistance(midpoint) + right * 0.8f;
            Assert.That(turn.TryProject(point, out float along, out float lateral), Is.True);
            Assert.That(along, Is.EqualTo(midpoint).Within(0.05f));
            Assert.That(lateral, Is.EqualTo(0.8f).Within(0.01f));
            Assert.That(turn.TryProject(turn.PositionAtDistance(midpoint) - right * 0.8f,
                out _, out float leftLateral), Is.True);
            Assert.That(leftLateral, Is.EqualTo(-0.8f).Within(0.01f));
            Assert.That(turn.TryProject(new Vector2(float.NaN, 0f), out _, out _), Is.False);
        }

        [Test]
        public void Prepare_RejectsMissingControlsAndClearsPreviousCache()
        {
            RoadTurnPath path = CreateTurn();
            Assert.That(path.Prepare(Projection), Is.True);
            path.Configure(path.Incoming, path.Junction, path.Outgoing, null, null, null, null);
            Assert.That(path.Prepare(Projection), Is.False);
            Assert.That(path.IsValid, Is.False);
            Assert.That(path.Length, Is.Zero);
        }

        [Test]
        public void Prepare_RejectsDegenerateHandlesAndMisalignedLaneEndpoints()
        {
            RoadTurnPath path = CreateTurn();
            RoadNode incoming = path.Incoming;
            RoadNode junction = path.Junction;
            RoadNode outgoing = path.Outgoing;
            Transform start = Control("start", new Vector2(-5f, -1.5f));
            path.Configure(incoming, junction, outgoing, start, start,
                Control("handle2", new Vector2(1.5f, 1f)), Control("end", new Vector2(1.5f, 5f)));
            Assert.That(path.Prepare(Projection), Is.False, "A stationary endpoint has no usable heading.");
            path.Configure(incoming, junction, outgoing,
                Control("startOffLane", new Vector2(-5f, 1.5f)),
                Control("handle1", new Vector2(-1f, 1.5f)),
                Control("handle2", new Vector2(1.5f, 1f)), Control("end", new Vector2(1.5f, 5f)));
            Assert.That(path.Prepare(Projection), Is.False, "Opposing lane endpoints must not be silently joined.");
        }

        [Test]
        public void Prepare_RejectsInteriorCuspsBetweenCacheSamples()
        {
            RoadNode incoming = Node("incoming", new Vector2(-20f, 0f));
            RoadNode junction = Node("junction", Vector2.zero);
            RoadNode outgoing = Node("outgoing", new Vector2(20f, 0f));
            RoadTurnPath path = Path(incoming, junction, outgoing,
                new Vector2(-5f, -1.5f), new Vector2(8f, -1.5f),
                new Vector2(-9f, -1.5f), new Vector2(5f, -1.5f));
            Assert.That(path.Prepare(Projection), Is.False,
                "Aligned endpoints cannot make an internally reversing curve safe to traverse.");
        }

        [Test]
        public void Prepare_RejectsInvalidProjectionAndDisconnectedMovement()
        {
            RoadTurnPath path = CreateTurn();
            Assert.That(path.Prepare(new IsometricGroundProjection(Vector2.zero, Vector2.zero, Vector2.zero)), Is.False);
            Assert.That(path.Prepare(new IsometricGroundProjection(Vector2.zero,
                new Vector2(float.PositiveInfinity, 0f), Vector2.up)), Is.False);
            path.Incoming.RemoveConnection(path.Junction);
            Assert.That(path.Prepare(Projection), Is.False);
        }

        [Test]
        public void Find_UsesExactMovementAndEnabledRegistryAndRejectsAmbiguousDuplicates()
        {
            RoadTurnPath first = CreateTurn();
            Assert.That(RoadTurnPath.Find(first.Incoming, first.Junction, first.Outgoing), Is.SameAs(first));
            Assert.That(RoadTurnPath.Find(first.Outgoing, first.Junction, first.Incoming), Is.Null);
            first.enabled = false;
            Assert.That(RoadTurnPath.HasAuthoredMovement(first.Incoming, first.Junction, first.Outgoing), Is.True);
            Assert.That(RoadTurnPath.Find(first.Incoming, first.Junction, first.Outgoing), Is.Null);
            first.enabled = true;
            RoadTurnPath duplicate = NewObject("duplicate").AddComponent<RoadTurnPath>();
            duplicate.Configure(first.Incoming, first.Junction, first.Outgoing, null, null, null, null);
            Assert.That(RoadTurnPath.Find(first.Incoming, first.Junction, first.Outgoing), Is.Null);
            duplicate.enabled = false;
            Assert.That(RoadTurnPath.Find(first.Incoming, first.Junction, first.Outgoing), Is.SameAs(first));
        }

        [Test]
        public void PreparedGeometry_RemainsCachedUntilExplicitPrepare()
        {
            RoadTurnPath path = CreateTurn();
            Assert.That(path.Prepare(Projection), Is.True);
            Vector2 midpoint = path.PositionAtDistance(path.Length / 2f);
            // Moving scene parents cannot mutate a route's already-prepared geometry mid-traversal.
            path.transform.position += Vector3.right;
            AssertVector(path.PositionAtDistance(path.Length / 2f), midpoint);
        }

        private RoadTurnPath CreateTurn()
        {
            RoadNode incoming = Node("incoming", new Vector2(-10f, 0f));
            RoadNode junction = Node("junction", Vector2.zero);
            RoadNode outgoing = Node("outgoing", new Vector2(0f, 10f));
            const float handle = 6.5f * 0.55228475f;
            return Path(incoming, junction, outgoing, new Vector2(-5f, -1.5f),
                new Vector2(-5f + handle, -1.5f), new Vector2(1.5f, 5f - handle), new Vector2(1.5f, 5f));
        }

        private RoadTurnPath Path(RoadNode incoming, RoadNode junction, RoadNode outgoing,
            Vector2 start, Vector2 handle1, Vector2 handle2, Vector2 end)
        {
            incoming.AddConnection(junction);
            junction.AddConnection(outgoing);
            RoadTurnPath path = NewObject("path").AddComponent<RoadTurnPath>();
            Transform p0 = Control("start", start);
            Transform p1 = Control("handle1", handle1);
            Transform p2 = Control("handle2", handle2);
            Transform p3 = Control("end", end);
            p0.SetParent(path.transform, true);
            p1.SetParent(path.transform, true);
            p2.SetParent(path.transform, true);
            p3.SetParent(path.transform, true);
            path.Configure(incoming, junction, outgoing, p0, p1, p2, p3);
            return path;
        }

        private RoadNode Node(string name, Vector2 ground)
        {
            RoadNode node = NewObject(name).AddComponent<RoadNode>();
            node.transform.position = Projection.GroundToWorld(ground);
            node.LaneOffsetMeters = 1.5f;
            return node;
        }

        private Transform Control(string name, Vector2 ground)
        {
            Transform control = NewObject(name).transform;
            control.position = Projection.GroundToWorld(ground);
            return control;
        }

        private GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.0001f));
        }
    }
}
