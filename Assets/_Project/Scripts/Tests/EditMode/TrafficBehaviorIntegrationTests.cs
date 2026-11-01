using System.Collections.Generic;
using NUnit.Framework;
using OutsiderDriver.Data;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;
using UnityEngine;

namespace OutsiderDriver.Tests.EditMode
{
    // Hand-authored metre-space fixtures. No Editor Update or wall-clock timing is required.
    public class TrafficBehaviorIntegrationTests
    {
        private const float Dt = 0.02f;
        private const float LaneOffset = 1.5f;
        private const float StopLineX = -16f;
        private const float TurnRadius = 11.5f;
        private static readonly Vector2 TurnStart = new Vector2(-10f, -LaneOffset);
        private static readonly Vector2 TurnEnd = new Vector2(LaneOffset, 10f);
        private static readonly Vector2 Handle1 = TurnStart + Vector2.right * (TurnRadius * 0.55228475f);
        private static readonly Vector2 Handle2 = TurnEnd - Vector2.up * (TurnRadius * 0.55228475f);

        private readonly List<Object> _created = new List<Object>();
        private RoadNetwork _network;
        private RoadNode _incoming, _junction, _outgoing;
        private TrafficSignalController _signal;
        private TrafficSignalApproach _approach;
        private TrafficSignalApproach _crossApproach;
        private static IsometricGroundProjection Projection => IsometricGroundProjection.Default;

        [SetUp]
        public void SetUp()
        {
            VehicleController.ClearActiveVehicles();
            _network = Create("network", Vector2.zero).AddComponent<RoadNetwork>();
            _network.ConfigureGrid(null); // Explicit default ground model, independent of open scenes.
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            VehicleController.ClearActiveVehicles();
        }

        [TestCase(0.02f)]
        [TestCase(0.1f)]
        public void RedSignal_BrakesBeforeFrontBumperCrossesLine_AndGreenCompletesRoute(float dt)
        {
            CreateRoad(false);
            CreateSignals();
            VehicleController car = CarOnApproach("car", new Vector2(-80f, -LaneOffset));
            bool brakedWhileMoving = false;
            float previousSpeed = car.CurrentSpeed;
            int arrivals = 0;
            car.OnDestinationReached += node => { Assert.AreSame(_outgoing, node); arrivals++; };

            for (int i = 0; i < Mathf.RoundToInt(20f / dt); i++)
            {
                _signal.Tick(dt);
                car.TickMovement(dt);
                if (car.CurrentSpeed < previousSpeed - 0.001f && car.CurrentSpeed > 1f)
                    brakedWhileMoving = true;
                Assert.That(Ground(car).x + car.LengthMeters * 0.5f, Is.LessThanOrEqualTo(StopLineX + 0.001f));
                Assert.IsNull(_junction.OccupyingVehicle);
                previousSpeed = car.CurrentSpeed;
            }

            Assert.AreEqual(SignalState.Red, _approach.State);
            Assert.IsTrue(brakedWhileMoving, "A car at cruise speed must brake progressively for red.");
            Assert.That(car.CurrentSpeed, Is.LessThan(0.05f));
            Assert.That(Ground(car).x + car.LengthMeters * 0.5f, Is.InRange(StopLineX - 0.4f, StopLineX));
            Assert.AreEqual(0, arrivals);
            Assert.IsTrue(car.NavigateTo(_outgoing), "Re-routing a waiting car must retain its authored incoming approach.");
            car.TickMovement(dt);
            Assert.That(Ground(car).x + car.LengthMeters * 0.5f, Is.LessThanOrEqualTo(StopLineX + 0.001f));

            GrantApproachGreenAfterTwentySeconds();
            for (int i = 0; i < Mathf.RoundToInt(40f / dt) && car.HasActiveRoute; i++)
            {
                _signal.Tick(dt);
                car.TickMovement(dt);
                Assert.That(car.CurrentSpeed - previousSpeed, Is.LessThanOrEqualTo(car.Acceleration * dt + 0.001f));
                previousSpeed = car.CurrentSpeed;
            }
            Assert.AreEqual(1, arrivals);
            Assert.IsFalse(car.HasActiveRoute);
            AssertGround(Ground(car), new Vector2(100f, -LaneOffset));
            Assert.IsNull(_junction.OccupyingVehicle);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SignalQueue_ThreeCarsStopWithoutContact_AndDrainInEitherTickOrder(bool reverse)
        {
            CreateRoad(false);
            CreateSignals();
            var cars = new[]
            {
                CarOnApproach("front", new Vector2(-50f, -LaneOffset)),
                CarOnApproach("middle", new Vector2(-75f, -LaneOffset)),
                CarOnApproach("rear", new Vector2(-100f, -LaneOffset))
            };
            var completed = new List<VehicleController>();
            foreach (VehicleController car in cars)
                car.OnDestinationReached += node =>
                {
                    Assert.AreSame(_outgoing, node);
                    completed.Add(car);
                    car.gameObject.SetActive(false); // Equivalent to returning completed ambient traffic to its pool.
                };

            for (int i = 0; i < 1000; i++)
            {
                _signal.Tick(Dt);
                TickCars(cars, reverse);
                AssertQueueSpacing(cars);
                Assert.That(Ground(cars[0]).x + cars[0].LengthMeters * 0.5f, Is.LessThanOrEqualTo(StopLineX + 0.001f));
                Assert.IsNull(_junction.OccupyingVehicle);
            }
            foreach (VehicleController car in cars) Assert.That(car.CurrentSpeed, Is.LessThan(0.05f));

            GrantApproachGreenAfterTwentySeconds();
            for (int i = 0; i < 3000 && completed.Count != cars.Length; i++)
            {
                _signal.Tick(Dt);
                TickCars(cars, reverse);
                AssertQueueSpacing(cars);
            }
            CollectionAssert.AreEqual(cars, completed, "The queue must complete in lane order without a permanent yield.");
            Assert.IsNull(_junction.OccupyingVehicle);
        }

        [Test]
        public void AuthoredCorner_FollowsCubicAndEndpoint_WithCurvatureBrakingAndContinuousHeading()
        {
            CreateRoad(true);
            RoadTurnPath turn = CreateTurn();
            VehicleController car = CarOnApproach("turning", new Vector2(-80f, -LaneOffset));
            var model = ScriptableObject.CreateInstance<CarModelSO>();
            _created.Add(model);
            car.ApplyCarModel(model);
            bool cruisedBeforeTurn = false;
            bool brakedBeforeTurn = false;
            bool visitedEntry = false;
            int curveSamples = 0;
            int junctionPasses = 0;
            car.OnWaypointPassed += node =>
            {
                if (node != _junction) return;
                junctionPasses++;
                AssertGround(Ground(car), TurnEnd);
            };
            Vector2 previous = Ground(car);
            Vector2 previousVisual = car.VisualGroundDirection;
            float previousSpeed = car.CurrentSpeed;

            for (int i = 0; i < 4000 && car.HasActiveRoute; i++)
            {
                car.TickMovement(Dt);
                Vector2 position = Ground(car);
                if (position.x < -30f && car.CurrentSpeed > car.MaxSpeed - 0.05f) cruisedBeforeTurn = true;
                if (position.x < TurnStart.x && car.CurrentSpeed < previousSpeed - 0.001f)
                    brakedBeforeTurn = true;
                if (Vector2.Distance(position, TurnStart) < 0.001f) visitedEntry = true;
                if (car.HasActiveRoute)
                    Assert.That(Vector2.Distance(previous, position), Is.LessThanOrEqualTo(car.CurrentSpeed * Dt + 0.001f),
                        "Entering or leaving the authored curve must not teleport the car.");
                Assert.That(Vector2.Angle(previousVisual, car.VisualGroundDirection),
                    Is.LessThanOrEqualTo(model.VisualHeadingDegreesPerSecond * Dt + 0.02f));

                if (position.x > TurnStart.x + 0.01f && position.y < TurnEnd.y - 0.01f)
                {
                    float t = AssertOnAuthoredCubic(position);
                    Vector2 tangent = CubicDerivative(t).normalized;
                    Assert.That(Vector2.Dot(tangent, car.ForwardGroundDirection), Is.GreaterThan(0.9999f));
                    Vector2 first = CubicDerivative(t);
                    Vector2 second = 6f * ((1f - t) * (Handle2 - 2f * Handle1 + TurnStart)
                        + t * (TurnEnd - 2f * Handle2 + Handle1));
                    float curvature = Mathf.Abs(first.x * second.y - first.y * second.x) / (first.sqrMagnitude * first.magnitude);
                    Assert.That(car.CurrentSpeed * car.CurrentSpeed * curvature,
                        Is.LessThanOrEqualTo(model.MaximumLateralAcceleration + 0.1f));
                    curveSamples++;
                }
                previous = position;
                previousSpeed = car.CurrentSpeed;
                previousVisual = car.VisualGroundDirection;
            }
            Assert.IsTrue(turn.IsValid);
            Assert.IsTrue(cruisedBeforeTurn);
            Assert.IsTrue(brakedBeforeTurn);
            Assert.IsTrue(visitedEntry);
            Assert.That(curveSamples, Is.GreaterThan(100));
            Assert.AreEqual(1, junctionPasses);
            Assert.IsFalse(car.HasActiveRoute);
            AssertGround(Ground(car), new Vector2(LaneOffset, 100f));
            Assert.IsNull(_junction.OccupyingVehicle);
        }

        [Test]
        public void RedThenStoppedLeaderOnTurn_RecoversAndCompletesAuthoredRoute()
        {
            CreateRoad(true);
            CreateTurn();
            CreateSignals();
            VehicleController car = CarOnApproach("turning", new Vector2(-80f, -LaneOffset));
            VehicleController parked = Create("stopped leader", new Vector2(LaneOffset, 14f)).AddComponent<VehicleController>();
            parked.Construct(_network);

            for (int i = 0; i < 1000; i++)
            {
                _signal.Tick(Dt);
                car.TickMovement(Dt);
                Assert.That(Ground(car).x + car.LengthMeters * 0.5f, Is.LessThanOrEqualTo(StopLineX + 0.001f));
            }
            GrantApproachGreenAfterTwentySeconds();
            for (int i = 0; i < 1500; i++)
            {
                _signal.Tick(Dt);
                car.TickMovement(Dt);
                Assert.That(Vector2.Distance(Ground(car), Ground(parked)),
                    Is.GreaterThanOrEqualTo((car.LengthMeters + parked.LengthMeters) * 0.5f + 1.8f));
            }
            Assert.That(car.CurrentSpeed, Is.LessThan(0.05f));
            Assert.That(Ground(car).y, Is.InRange(5f, 9f), "The outgoing queue should stop this car on its authored turn.");
            AssertOnAuthoredCubic(Ground(car));
            Assert.IsTrue(car.HasActiveRoute);

            parked.gameObject.SetActive(false);
            for (int i = 0; i < 3000 && car.HasActiveRoute; i++)
            {
                _signal.Tick(Dt);
                car.TickMovement(Dt);
            }
            Assert.IsFalse(car.HasActiveRoute);
            AssertGround(Ground(car), new Vector2(LaneOffset, 100f));
            Assert.IsNull(_junction.OccupyingVehicle);
        }

        [Test]
        public void GreenSignal_CloserRedCrossTrafficCannotVetoThePermittedApproach()
        {
            CreateRoad(false);
            _junction.Priority = RoadNodePriority.YieldIntersection;
            _junction.IntersectionClearanceRadius = 10f;
            CreateSignals(new Vector2(LaneOffset, -11f));
            _signal.Tick(123.1f);
            Assert.IsTrue(_approach.AllowsEntry);
            Assert.AreEqual(SignalState.Red, _crossApproach.State);
            VehicleController crossing = CarOnApproach("red crossing", new Vector2(LaneOffset, -13.5f), _crossApproach.Incoming);
            VehicleController permitted = CarOnApproach("green approach", new Vector2(-20f, -LaneOffset));
            Vector2 crossingStart = Ground(crossing);

            for (int i = 0; i < 1000; i++)
            {
                _signal.Tick(Dt);
                crossing.TickMovement(Dt);
                permitted.TickMovement(Dt);
                Assert.That(Ground(crossing).y + crossing.LengthMeters * 0.5f, Is.LessThanOrEqualTo(-11f + 0.001f));
            }
            Assert.That(Vector2.Distance(crossingStart, Ground(crossing)), Is.LessThan(0.5f));
            Assert.That(Ground(permitted).x, Is.GreaterThan(20f));
        }

        [Test]
        public void SignalChangesAfterCurveEntersConflict_CommittedCarKeepsReservationAndClears()
        {
            CreateRoad(true);
            CreateTurn();
            CreateSignals();
            _signal.Tick(123.1f);
            VehicleController car = CarOnApproach("committed turn", new Vector2(-80f, -LaneOffset));
            float boundary = _junction.IntersectionClearanceRadius + car.LengthMeters * 0.5f;
            bool entered = false;
            for (int i = 0; i < 2000; i++)
            {
                float before = Ground(car).magnitude;
                _signal.Tick(Dt);
                car.TickMovement(Dt);
                if (before > boundary && Ground(car).magnitude <= boundary)
                {
                    entered = true;
                    break;
                }
            }
            Assert.IsTrue(entered, "The fixture must switch lights on the exact tick that enters the conflict area.");
            Assert.AreSame(car, _junction.OccupyingVehicle);
            Vector2 entryPosition = Ground(car);
            _signal.Tick(120f);
            Assert.IsFalse(_approach.AllowsEntry);

            for (int i = 0; i < 3000 && car.HasActiveRoute; i++)
            {
                _signal.Tick(Dt);
                car.TickMovement(Dt);
                if (Ground(car).magnitude <= boundary)
                    Assert.AreSame(car, _junction.OccupyingVehicle);
                if (i == 10) Assert.That(Vector2.Distance(entryPosition, Ground(car)), Is.GreaterThan(0.2f));
            }
            Assert.IsFalse(car.HasActiveRoute);
            AssertGround(Ground(car), new Vector2(LaneOffset, 100f));
            Assert.IsNull(_junction.OccupyingVehicle);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void MidCurveRouteRequest_IncludingAfterCancellation_FinishesAuthoredExitBeforeChangingDestination(bool alternative, bool cancelFirst)
        {
            CreateRoad(true);
            CreateTurn();
            RoadNode requestedDestination = Node("new destination", new Vector2(0f, 160f));
            _outgoing.AddConnection(requestedDestination);
            _network.RebuildNetwork();
            VehicleController car = CarOnApproach("rerouting turn", new Vector2(-80f, -LaneOffset));
            ReachCurveInterior(car);
            Vector2 beforeRequest = Ground(car);
            car.Stop(); // a successful navigation request must also resume a paused crossing
            if (cancelFirst)
            {
                car.CancelRoute();
                Assert.IsTrue(car.HasActiveRoute, "Cancellation must first drain the occupied authored movement.");
            }
            Assert.IsFalse(car.NavigateAlongPath(new[] { requestedDestination }),
                "Direct replacement cannot discard a partially traversed authored movement.");
            Assert.IsTrue(alternative ? car.NavigateToAlternative(requestedDestination, true) : car.NavigateTo(requestedDestination));
            AssertGround(Ground(car), beforeRequest);
            Assert.AreSame(_junction, car.CurrentTargetNode);
            Assert.AreSame(_outgoing, car.DestinationNode);
            int exits = 0;
            int arrivals = 0;
            car.OnWaypointPassed += node =>
            {
                if (node != _junction) return;
                exits++;
                AssertGround(Ground(car), TurnEnd);
            };
            car.OnDestinationReached += node => { Assert.AreSame(requestedDestination, node); arrivals++; };
            for (int i = 0; i < 4000 && car.HasActiveRoute; i++)
            {
                car.TickMovement(Dt);
                if (exits == 0) AssertOnAuthoredCubic(Ground(car));
            }
            Assert.AreEqual(1, exits);
            Assert.AreEqual(1, arrivals);
            Assert.IsFalse(car.HasActiveRoute);
            AssertGround(Ground(car), new Vector2(LaneOffset, 160f));
            Assert.IsNull(_junction.OccupyingVehicle);
        }

        [Test]
        public void MidCurveCancellation_DrainsPausedCarToSafeAuthoredExitWithoutDestinationArrival()
        {
            CreateRoad(true);
            CreateTurn();
            VehicleController car = CarOnApproach("cancelled turn", new Vector2(-80f, -LaneOffset));
            ReachCurveInterior(car);
            Vector2 beforeCancellation = Ground(car);
            int exits = 0;
            int arrivals = 0;
            car.OnWaypointPassed += node =>
            {
                if (node != _junction) return;
                exits++;
                AssertGround(Ground(car), TurnEnd);
            };
            car.OnDestinationReached += node => arrivals++;
            car.Stop();
            car.CancelRoute();
            AssertGround(Ground(car), beforeCancellation);
            Assert.IsTrue(car.HasActiveRoute);
            for (int i = 0; i < 2000 && car.HasActiveRoute; i++)
            {
                car.TickMovement(Dt);
                if (exits == 0) AssertOnAuthoredCubic(Ground(car));
            }
            Assert.AreEqual(1, exits);
            Assert.AreEqual(0, arrivals);
            Assert.IsFalse(car.HasActiveRoute);
            AssertGround(Ground(car), TurnEnd);
            car.TickMovement(Dt); // Observe physical rear clearance after the cancellation's final movement.
            Assert.IsNull(_junction.OccupyingVehicle);
            AssertGround(Ground(car), TurnEnd);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MidCurveUnreachableRouteRequest_ReturnsFalseAndPreservesOriginalMovement(bool alternative)
        {
            CreateRoad(true);
            CreateTurn();
            RoadNode unreachable = Node("disconnected destination", new Vector2(70f, 180f));
            _network.RebuildNetwork();
            VehicleController car = CarOnApproach("unreachable reroute", new Vector2(-80f, -LaneOffset));
            ReachCurveInterior(car);
            Vector2 beforeRequest = Ground(car);
            Assert.IsFalse(alternative ? car.NavigateToAlternative(unreachable, true) : car.NavigateTo(unreachable));
            AssertGround(Ground(car), beforeRequest);
            Assert.AreSame(_junction, car.CurrentTargetNode);
            Assert.AreSame(_outgoing, car.DestinationNode);
            int exits = 0;
            int arrivals = 0;
            car.OnWaypointPassed += node =>
            {
                if (node != _junction) return;
                exits++;
                AssertGround(Ground(car), TurnEnd);
            };
            car.OnDestinationReached += node => { Assert.AreSame(_outgoing, node); arrivals++; };
            for (int i = 0; i < 4000 && car.HasActiveRoute; i++)
            {
                car.TickMovement(Dt);
                if (exits == 0) AssertOnAuthoredCubic(Ground(car));
            }
            Assert.AreEqual(1, exits);
            Assert.AreEqual(1, arrivals);
            Assert.IsFalse(car.HasActiveRoute);
            AssertGround(Ground(car), new Vector2(LaneOffset, 100f));
            Assert.IsNull(_junction.OccupyingVehicle);
        }

        private static void ReachCurveInterior(VehicleController car)
        {
            for (int i = 0; i < 2000; i++)
            {
                car.TickMovement(Dt);
                if (Ground(car).y > 1f && Ground(car).y < 5f) return;
            }
            Assert.Fail("The fixture must enter the interior of its authored curve before the request.");
        }

        private void CreateRoad(bool corner)
        {
            _incoming = Node("incoming", new Vector2(-160f, 0f));
            _junction = Node("junction", Vector2.zero);
            _junction.PieceType = RoadPieceType.FourWayIntersection;
            _junction.IntersectionClearanceRadius = 3f;
            _outgoing = Node("outgoing", corner ? new Vector2(0f, 100f) : new Vector2(100f, 0f));
            _incoming.AddConnection(_junction);
            _junction.AddConnection(_outgoing);
            _network.RebuildNetwork();
        }

        private RoadTurnPath CreateTurn()
        {
            RoadTurnPath turn = Create("authored corner", Vector2.zero).AddComponent<RoadTurnPath>();
            turn.Configure(_incoming, _junction, _outgoing,
                Create("entry", TurnStart).transform, Create("handle 1", Handle1).transform,
                Create("handle 2", Handle2).transform, Create("exit", TurnEnd).transform);
            Assert.IsTrue(turn.Prepare(Projection), "The authored lane endpoints, tangents and connected graph must be valid.");
            Assert.That(TurnStart.magnitude, Is.GreaterThan(_junction.IntersectionClearanceRadius + 2.25f));
            Assert.That(TurnEnd.magnitude, Is.GreaterThan(_junction.IntersectionClearanceRadius + 2.25f));
            return turn;
        }

        private void CreateSignals(Vector2? crossStop = null)
        {
            _signal = Create("signals", Vector2.zero).AddComponent<TrafficSignalController>();
            RoadNode crossIncoming = Node("cross incoming", new Vector2(0f, -160f));
            crossIncoming.AddConnection(_junction);
            _crossApproach = Create("cross approach", Vector2.zero).AddComponent<TrafficSignalApproach>();
            _crossApproach.Configure(_signal, _junction, crossIncoming,
                Create("cross stop line", crossStop ?? new Vector2(LaneOffset, -16f)).transform, 0);
            _approach = Create("west approach", Vector2.zero).AddComponent<TrafficSignalApproach>();
            _approach.Configure(_signal, _junction, _incoming,
                Create("west stop line", new Vector2(StopLineX, -LaneOffset)).transform, 1);
            _signal.Initialize(_junction, new[] { _crossApproach, _approach }, 120f, 2f, 1f);
            _signal.Tick(1f);
            Assert.IsTrue(_signal.IsOperational);
            Assert.AreEqual(SignalState.Red, _approach.State);
            _network.RebuildNetwork();
        }

        private void GrantApproachGreenAfterTwentySeconds()
        {
            // After 20 seconds in group 0, 100 seconds of green + amber + clearance remain.
            _signal.Tick(104f);
            Assert.IsTrue(_approach.AllowsEntry);
        }

        private VehicleController CarOnApproach(string name, Vector2 ground, RoadNode incoming = null)
        {
            incoming = incoming ?? _incoming;
            Vector2 direction = (Projection.WorldToGround(_junction.Position) - Projection.WorldToGround(incoming.Position)).normalized;
            Vector2 laneStart = Projection.WorldToGround(incoming.Position) + new Vector2(direction.y, -direction.x) * LaneOffset;
            VehicleController car = Create(name, laneStart).AddComponent<VehicleController>();
            car.Construct(_network);
            Assert.IsTrue(car.NavigateAlongPath(new[] { incoming, _junction, _outgoing }));
            // Consume the incoming waypoint before positioning a fixture partway along that approach.
            car.TickMovement(Dt);
            Assert.AreSame(_junction, car.CurrentTargetNode);
            car.transform.position = Projection.GroundToWorld(ground);
            return car;
        }

        private void TickCars(VehicleController[] cars, bool reverse)
        {
            for (int i = 0; i < cars.Length; i++)
            {
                VehicleController car = cars[reverse ? cars.Length - 1 - i : i];
                if (car.gameObject.activeSelf) car.TickMovement(Dt);
            }
        }

        private void AssertQueueSpacing(VehicleController[] cars)
        {
            for (int i = 0; i + 1 < cars.Length; i++)
                if (cars[i].gameObject.activeSelf && cars[i + 1].gameObject.activeSelf)
                    Assert.That(Ground(cars[i]).x - Ground(cars[i + 1]).x,
                        Is.GreaterThanOrEqualTo((cars[i].LengthMeters + cars[i + 1].LengthMeters) * 0.5f + 1.9f));
        }

        private RoadNode Node(string name, Vector2 ground)
        {
            RoadNode node = Create(name, ground).AddComponent<RoadNode>();
            node.transform.SetParent(_network.transform, true);
            node.LaneOffsetMeters = LaneOffset;
            return node;
        }

        private GameObject Create(string name, Vector2 ground)
        {
            var result = new GameObject(name);
            _created.Add(result);
            result.transform.position = Projection.GroundToWorld(ground);
            return result;
        }

        private static Vector2 Ground(VehicleController car) => Projection.WorldToGround(car.transform.position);

        private static void AssertGround(Vector2 actual, Vector2 expected)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.001f));
        }

        private static Vector2 Cubic(float t)
        {
            float u = 1f - t;
            return u * u * u * TurnStart + 3f * u * u * t * Handle1
                + 3f * u * t * t * Handle2 + t * t * t * TurnEnd;
        }

        private static Vector2 CubicDerivative(float t)
        {
            float u = 1f - t;
            return 3f * u * u * (Handle1 - TurnStart) + 6f * u * t * (Handle2 - Handle1)
                + 3f * t * t * (TurnEnd - Handle2);
        }

        private static float AssertOnAuthoredCubic(Vector2 ground)
        {
            // Invert this monotone authored cubic independently of RoadTurnPath's arc cache/projection.
            float low = 0f, high = 1f;
            for (int i = 0; i < 24; i++)
            {
                float middle = (low + high) * 0.5f;
                if (Cubic(middle).x < ground.x) low = middle;
                else high = middle;
            }
            float t = (low + high) * 0.5f;
            Assert.That(Vector2.Distance(Cubic(t), ground), Is.LessThan(0.003f),
                "The vehicle must follow the authored curve rather than an intersection chord.");
            return t;
        }
    }
}
