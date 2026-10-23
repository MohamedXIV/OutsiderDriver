using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.World;

namespace OutsiderDriver.Tests.EditMode
{
    public class HomeNeighborhoodLayoutTests
    {
        private readonly List<Object> _spawnedObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                if (_spawnedObjects[i] != null)
                {
                    Object.DestroyImmediate(_spawnedObjects[i]);
                }
            }
            _spawnedObjects.Clear();
        }

        [Test]
        public void EnsureIsometricGridConfigured_SetsNativeIsometricZAsYAndTwoToOneCellSize()
        {
            var rootGo = CreateTrackedObject("HomeNeighborhoodLayout", Vector3.zero);
            var layout = rootGo.AddComponent<HomeNeighborhoodLayout>();

            layout.EnsureIsometricGridConfigured();

            Assert.IsNotNull(layout.IsometricGrid);
            Assert.AreEqual(GridLayout.CellLayout.IsometricZAsY, layout.IsometricGrid.cellLayout);
            Assert.AreEqual(HomeNeighborhoodLayout.DefaultIsometricCellSize, layout.IsometricGrid.cellSize);
        }

        [Test]
        public void CanWalkAt_EnforcesBoundedWalkableZonesAndObstacleCells()
        {
            var rootGo = CreateTrackedObject("HomeNeighborhoodLayout", Vector3.zero);
            var layout = rootGo.AddComponent<HomeNeighborhoodLayout>();
            layout.EnsureIsometricGridConfigured();

            // Create HomeInterior zone centered at (-5, 3) with 6x4 rect
            var homeZoneGo = CreateTrackedObject("HomeZone", new Vector3(-5f, 3f, 0f));
            homeZoneGo.transform.SetParent(rootGo.transform, false);
            var homeZone = homeZoneGo.AddComponent<WalkableZone>();
            homeZone.Configure("zone_home", "Driver's Home", WalkableZoneType.HomeInterior, new Rect(-3f, -2f, 6f, 4f));

            // Create Starter Street Sidewalk zone centered at (0, 0) with 8x4 rect
            var streetZoneGo = CreateTrackedObject("StreetZone", Vector3.zero);
            streetZoneGo.transform.SetParent(rootGo.transform, false);
            var streetZone = streetZoneGo.AddComponent<WalkableZone>();
            streetZone.Configure("zone_street", "Starter Sidewalk", WalkableZoneType.NeighborhoodSidewalk, new Rect(-4f, -2f, 8f, 4f));

            layout.RebuildLayoutRegistry();

            // Point inside HomeInterior is walkable
            Assert.IsTrue(layout.CanWalkAt(new Vector2(-5f, 3f), out var matchedHome));
            Assert.AreSame(homeZone, matchedHome);

            // Point inside Starter Street Sidewalk is walkable
            Assert.IsTrue(layout.CanWalkAt(new Vector2(1f, 0.5f), out var matchedStreet));
            Assert.AreSame(streetZone, matchedStreet);

            // Point far outside the bounded neighborhood (e.g. (50, 50)) is NOT walkable (Decision P-009)
            Assert.IsFalse(layout.CanWalkAt(new Vector2(50f, 50f), out _));

            // Blocking a specific isometric cell inside the home prevents walking on that cell
            Vector3Int furnitureCell = layout.WorldToCell(new Vector3(-5f, 3f, 0f));
            layout.SetCellBlocked(furnitureCell, true);
            Assert.IsFalse(layout.CanWalkAt(new Vector2(-5f, 3f), out _), "Blocked furniture/wall cell should not be walkable.");
        }

        [Test]
        public void FindNearestInteractable_ReturnsClosestDomesticOrStreetFixtureWithinRadius()
        {
            var rootGo = CreateTrackedObject("HomeNeighborhoodLayout", Vector3.zero);
            var layout = rootGo.AddComponent<HomeNeighborhoodLayout>();

            var bedGo = CreateTrackedObject("Bed", new Vector3(-5f, 3f, 0f));
            bedGo.transform.SetParent(rootGo.transform, false);
            var bed = bedGo.AddComponent<NeighborhoodInteractable>();
            bed.Configure("interact_bed", "Rest", InteractableKind.HomeBed, interactionRadius: 1.5f);

            var benchGo = CreateTrackedObject("Workbench", new Vector3(-3f, 3f, 0f));
            benchGo.transform.SetParent(rootGo.transform, false);
            var bench = benchGo.AddComponent<NeighborhoodInteractable>();
            bench.Configure("interact_bench", "Translator Console", InteractableKind.TranslationWorkbench, interactionRadius: 1.5f);

            layout.RebuildLayoutRegistry();

            // Actor standing near the bed at (-4.8, 3.0)
            var foundNearBed = layout.FindNearestInteractable(new Vector2(-4.8f, 3.0f));
            Assert.AreSame(bed, foundNearBed);

            bool interactedWithBed = false;
            bed.OnInteracted += _ => interactedWithBed = true;
            Assert.IsTrue(bed.TryInteract(new Vector2(-4.8f, 3.0f)));
            Assert.IsTrue(interactedWithBed);

            // Actor standing far away at (20, 20) finds nothing
            Assert.IsNull(layout.FindNearestInteractable(new Vector2(20f, 20f)));
        }

        private GameObject CreateTrackedObject(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawnedObjects.Add(go);
            return go;
        }
    }
}
