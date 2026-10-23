using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Data;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.EditMode
{
    public class DataDefinitionsTests
    {
        private readonly List<Object> _createdAssets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                {
                    Object.DestroyImmediate(_createdAssets[i]);
                }
            }
            _createdAssets.Clear();
            VehicleController.ClearActiveVehicles();
        }

        [Test]
        public void PassengerScheduleEntry_IsActiveAtHour_HandlesDaytimeAndOvernightWrapping()
        {
            var daytimeShift = new PassengerScheduleEntry(startHour: 8, endHour: 17, districtId: "district_market", preferredNodeId: "MarketSquare");
            Assert.IsFalse(daytimeShift.IsActiveAtHour(7));
            Assert.IsTrue(daytimeShift.IsActiveAtHour(8));
            Assert.IsTrue(daytimeShift.IsActiveAtHour(12));
            Assert.IsFalse(daytimeShift.IsActiveAtHour(17));

            var overnightShift = new PassengerScheduleEntry(startHour: 22, endHour: 4, districtId: "district_port", preferredNodeId: "TransitTerminal");
            Assert.IsFalse(overnightShift.IsActiveAtHour(21));
            Assert.IsTrue(overnightShift.IsActiveAtHour(22));
            Assert.IsTrue(overnightShift.IsActiveAtHour(2));
            Assert.IsFalse(overnightShift.IsActiveAtHour(4));
        }

        [Test]
        public void PassengerDefinitionSO_ResolvesScheduleAndLanguageCorrectly()
        {
            var passenger = CreateAsset<PassengerDefinitionSO>();
            passenger.Initialize(
                passengerId: "res_lyra",
                displayName: "Lyra of the Third Ring",
                speciesId: "Vesperian",
                nativeLanguageId: "Vesperian",
                fareMultiplier: 1.25f,
                tipGenerosity: 0.4f);

            passenger.SetSchedule(new[]
            {
                new PassengerScheduleEntry(8, 16, "district_civic", "CentralJunction"),
                new PassengerScheduleEntry(16, 23, "district_terrace", "RiversideVista")
            });

            Assert.AreEqual("res_lyra", passenger.PassengerId);
            Assert.AreEqual("Vesperian", passenger.SpeciesId);
            Assert.AreEqual("Vesperian", passenger.NativeLanguageId);
            Assert.AreEqual(1.25f, passenger.FareMultiplier, 0.001f);

            Assert.IsTrue(passenger.TryGetScheduleForHour(10, out var morningEntry));
            Assert.AreEqual("CentralJunction", morningEntry.PreferredNodeId);

            Assert.IsTrue(passenger.TryGetScheduleForHour(19, out var eveningEntry));
            Assert.AreEqual("RiversideVista", eveningEntry.PreferredNodeId);

            Assert.IsFalse(passenger.TryGetScheduleForHour(2, out _));
        }

        [Test]
        public void DistrictConfigSO_CalculatesEstimatedFareFromDistanceAndMultiplier()
        {
            var district = CreateAsset<DistrictConfigSO>();
            district.Initialize(
                districtId: "district_amber",
                displayName: "Amber Terrace",
                targetAmbientVehicles: 6,
                basePickupFare: 20,
                creditsPerUnitDistance: 2.5f,
                landmarkNodeIds: new[] { "HomeGarage", "CentralJunction" });

            Assert.AreEqual("district_amber", district.DistrictId);
            Assert.AreEqual(6, district.TargetAmbientVehicles);
            Assert.AreEqual(2, district.LandmarkNodeIds.Count);

            // Base 20 + (10m * 2.5 = 25) = 45 * 1.0 = 45
            int standardFare = district.CalculateEstimatedFare(travelDistance: 10f, passengerFareMultiplier: 1.0f);
            Assert.AreEqual(45, standardFare);

            // 45 * 1.2 = 54
            int boostedFare = district.CalculateEstimatedFare(travelDistance: 10f, passengerFareMultiplier: 1.2f);
            Assert.AreEqual(54, boostedFare);
        }

        [Test]
        public void CarModelSO_ApplyCarModel_ConfiguresVehicleControllerSpecs()
        {
            var carModel = CreateAsset<CarModelSO>();
            carModel.Initialize(
                modelId: "car_shuttle_heavy",
                displayName: "Heavy Orbital Shuttle Cab",
                maxSpeed: 7.5f,
                acceleration: 5.5f,
                deceleration: 8.0f,
                forwardSensorDistance: 2.4f,
                passengerCapacity: 4);

            var vehicleGo = new GameObject("TestVehicle");
            _createdAssets.Add(vehicleGo);
            var controller = vehicleGo.AddComponent<VehicleController>();

            controller.ApplyCarModel(carModel);

            Assert.AreSame(carModel, controller.CarModel);
            Assert.AreEqual(7.5f, controller.MaxSpeed, 0.001f);
            Assert.AreEqual(5.5f, controller.Acceleration, 0.001f);
            Assert.AreEqual(8.0f, controller.Deceleration, 0.001f);
            Assert.AreEqual(2.4f, controller.ForwardSensorDistance, 0.001f);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(instance);
            return instance;
        }
    }
}
