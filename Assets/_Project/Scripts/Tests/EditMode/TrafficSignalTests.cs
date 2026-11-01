using System.Collections.Generic;
using NUnit.Framework;
using OutsiderDriver.Navigation;
using UnityEngine;

namespace OutsiderDriver.Tests.EditMode
{
    public class TrafficSignalTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private RoadNode _junction;
        private TrafficSignalController _controller;
        private TrafficSignalApproach _first;
        private TrafficSignalApproach _second;

        [SetUp]
        public void SetUp()
        {
            _junction = Create("Junction").AddComponent<RoadNode>();
            _junction.PieceType = RoadPieceType.FourWayIntersection;
            _controller = Create("Signals").AddComponent<TrafficSignalController>();
            _first = CreateApproach("First", 0);
            _second = CreateApproach("Second", 1);
            _controller.Initialize(_junction, new[] { _first, _second }, 5f, 2f, 1f);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void Cycle_StartsWithClearanceAndNeverGrantsConflictingGreens()
        {
            Assert.AreEqual(SignalState.AllRed, _first.State);
            Assert.IsFalse(_first.AllowsEntry);
            _controller.Tick(1f);
            Assert.IsTrue(_first.AllowsEntry);
            Assert.AreEqual(SignalState.Red, _second.State);
            _controller.Tick(5f);
            Assert.AreEqual(SignalState.Amber, _first.State);
            Assert.IsFalse(_first.AllowsEntry);
            _controller.Tick(2f);
            Assert.AreEqual(SignalState.AllRed, _first.State);
            Assert.AreEqual(SignalState.AllRed, _second.State);
            _controller.Tick(1f);
            Assert.AreEqual(SignalState.Red, _first.State);
            Assert.IsTrue(_second.AllowsEntry);
            _controller.Tick(8f);
            Assert.IsTrue(_first.AllowsEntry);
            Assert.IsFalse(_second.AllowsEntry);
        }

        [Test]
        public void OccupiedJunction_HoldsClearanceUntilVehicleReleasesReservation()
        {
            _controller.Tick(1f);
            var vehicle = new object();
            Assert.IsTrue(_junction.TryReserveIntersection(vehicle));
            _controller.Tick(100f);
            Assert.AreEqual(SignalState.AllRed, _first.State);
            Assert.IsFalse(_second.AllowsEntry);
            _junction.ReleaseIntersection(vehicle);
            _controller.Tick(0f);
            Assert.IsTrue(_second.AllowsEntry);
        }

        [Test]
        public void DisabledSignals_RemainDiscoverableAndBlockEntry()
        {
            _controller.Tick(1f);
            _controller.enabled = false;
            Assert.AreSame(_controller, TrafficSignalController.Find(_junction));
            Assert.IsFalse(_first.AllowsEntry);
            _controller.enabled = true;
            _controller.Tick(1f);
            _first.enabled = false;
            Assert.AreSame(_first, TrafficSignalApproach.Find(_junction, _first.Incoming));
            Assert.AreEqual(SignalState.Red, _first.State);
            Assert.IsFalse(_second.AllowsEntry);
        }

        [Test]
        public void MissingStopLine_AndInvalidDuration_FailClosed()
        {
            _first.Configure(_controller, _junction, _first.Incoming, null, 0);
            _controller.Tick(20f);
            Assert.AreSame(_first, TrafficSignalApproach.Find(_junction, _first.Incoming));
            Assert.AreEqual(SignalState.Red, _first.State);
            Assert.IsFalse(_second.AllowsEntry);
            _controller.Initialize(_junction, new[] { _second }, float.NaN, 2f, 1f);
            _controller.Tick(20f);
            Assert.IsFalse(_second.AllowsEntry);
        }

        [Test]
        public void DuplicateApproachAndDuplicateController_FailClosed()
        {
            var duplicate = Create("Duplicate").AddComponent<TrafficSignalApproach>();
            duplicate.Configure(_controller, _junction, _first.Incoming, _first.StopLine, 0);
            _controller.Tick(1f);
            Assert.IsFalse(_first.AllowsEntry);
            Object.DestroyImmediate(duplicate);
            var secondController = Create("DuplicateController").AddComponent<TrafficSignalController>();
            secondController.Initialize(_junction, new[] { _first, _second }, 5f, 2f, 1f);
            _controller.Tick(1f);
            Assert.IsNotNull(TrafficSignalController.Find(_junction));
            Assert.IsFalse(_first.AllowsEntry);
        }

        [Test]
        public void LargeAndInvalidTicks_AreBoundedAndPreservePhaseTiming()
        {
            _controller.Tick(float.NaN);
            _controller.Tick(float.PositiveInfinity);
            _controller.Tick(-1f);
            Assert.AreEqual(SignalState.AllRed, _first.State);
            _controller.Tick(16001f);
            Assert.IsTrue(_first.AllowsEntry);
            _controller.Tick(4.5f);
            Assert.IsTrue(_first.AllowsEntry);
            _controller.Tick(0.5f);
            Assert.AreEqual(SignalState.Amber, _first.State);
        }

        [Test]
        public void SameGroupApproaches_FailClosedWithoutAuthoredConflictMetadata()
        {
            var sameGroup = CreateApproach("SameGroup", 0);
            _first.Incoming.transform.position = new Vector3(-10f, 0f, 0f);
            sameGroup.Incoming.transform.position = new Vector3(0f, -10f, 0f);
            _controller.Initialize(_junction, new[] { _first, _second, sameGroup }, 5f, 2f, 1f);
            _controller.Tick(1f);
            Assert.IsFalse(_first.AllowsEntry);
            Assert.IsFalse(sameGroup.AllowsEntry);
            Assert.IsFalse(_second.AllowsEntry);
        }

        [Test]
        public void UnconnectedIncomingOrMissingGroup_FailsClosed()
        {
            _first.Incoming.RemoveConnection(_junction);
            _controller.Tick(1f);
            Assert.IsFalse(_first.AllowsEntry);
            Assert.IsFalse(_second.AllowsEntry);
            _first.Incoming.AddConnection(_junction);
            _second.Configure(_controller, _junction, _second.Incoming, _second.StopLine, 2);
            _controller.Tick(1f);
            Assert.IsFalse(_first.AllowsEntry);
        }

        private TrafficSignalApproach CreateApproach(string name, int group)
        {
            var incoming = Create(name + "Incoming").AddComponent<RoadNode>();
            incoming.AddConnection(_junction);
            var stopLine = Create(name + "StopLine").transform;
            var approach = Create(name).AddComponent<TrafficSignalApproach>();
            approach.Configure(_controller, _junction, incoming, stopLine, group);
            return approach;
        }

        private GameObject Create(string name)
        {
            var result = new GameObject(name);
            _objects.Add(result);
            return result;
        }
    }
}
