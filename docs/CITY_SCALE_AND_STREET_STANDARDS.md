# City Scale & Street Standards

**Status:** APPROVED owner physical-scale and street-authoring direction (owner brief, 2026-10-25).  
**Provenance:** The owner's 2026-10-25 City Foundation 2.0 brief referenced this document and decision IDs P-030 through P-033. At the start of that implementation `P-030` existed, while this file and `P-031`–`P-033` did not; the missing material is recorded here and in the Decision Register from that brief. No earlier approved text is superseded.

Read with [Game Design](GAME_DESIGN.md), [Decision Register](DECISIONS_AND_OPEN_QUESTIONS.md), [World & Content Data Contract](WORLD_CONTENT_DATA_CONTRACT.md), [Visual Direction](VISUAL_DIRECTION.md) and [Engineering Principles](ENGINEERING_PRINCIPLES.md).

## 1. Physical scale

**One logical planning cell = 1 m × 1 m.** This is the authored ground-plane convention; it is not a claim that one Unity world unit equals one metre.

- The production Grid is native `IsometricZAsY` with `cellSize = (1, 0.5, 1)`. Its measured `CellToWorld` basis in the production scene is `(0.5, 0.25)` for +X and `(-0.5, 0.25)` for +Y world units per cell axis. A world-unit distance is therefore **not** a metre.
- `IsometricGroundProjection` (`Assets/_Project/Scripts/Runtime/Navigation/IsometricGroundProjection.cs`) samples the live Grid and converts between world space and ground metres. Navigation distance, vehicle speed/acceleration, sensor ranges, intersection clearance and lane offsets all use ground metres.
- The game remains Unity **2D isometric**: native Tilemaps, SpriteRenderers and the existing projection. No 3D world, custom projection engine or tile-size change is implied by this convention.
- The right-hand lane-keeping used by the demo is a **technical convention for the authored street**, not an approved in-world traffic-side rule.

### Baseline dimensions (authored street classes, not universal requirements)

| Feature | Nominal dimension |
| --- | --- |
| Taxi / ordinary car | 2 m wide, 4.5 m long (4–5 m range) |
| Ordinary travel lane | 3 m |
| Sidewalk | 2 m per side |
| Local two-way road | 10 m total, including sidewalks (3 m lane + 3 m lane + 2 m + 2 m) |
| Two lanes each direction | 16 m total, including sidewalks (4 × 3 m + 2 m + 2 m) |

These values are stored as editable `StreetProfileSO` assets under `Assets/_Project/Data/Streets/`:

- `StreetClass_LocalTwoWay.asset` — 10 m, 1 lane each direction.
- `StreetClass_TwoLanesEachDirection.asset` — 16 m, 2 lanes each direction.

The production scene currently authors the 10 m local class only. The 16 m class is data for future authored streets, not a claim that a 16 m street exists in the scene.

## 2. Road authoring rules

- **Visual pieces are navigation-free.** Reusable Tile assets and `RoadPiece_*` visual prefabs contain no `RoadNode` components or production-specific node IDs. Duplicating them cannot clone navigation identity.
- **Navigation is authored separately.** `RoadNode` GameObjects live under the `RoadNetwork` root and carry stable, unique IDs (`node_d01_sw_junction`, etc.). The graph encodes driving paths and junctions, never one node per road tile.
- **Validation is mandatory.** `RoadNetwork.ValidateNetwork` reports empty/duplicate IDs, null/self/duplicate links, unregistered neighbours, one-way links without a return link, elevation transitions without an authored ramp, and nodes disconnected from the main component. The component also exposes a `Validate Network` context-menu diagnostic.
- **Scene authority.** `Assets/_Project/Scenes/OutsiderCity.unity` is the default enabled build scene. `SampleScene` remains in the project but is disabled in Build Settings. `TaxiSandbox.unity` and `TaxiTestDriver` remain legacy/test-only and are not production templates.
- **Repeatability proof.** `RoadAuthoringHardeningTests.RoadPiecePrefabs_AreVisualOnlyAndRepeatableWithoutCloningNavigationIds` instantiates every road prefab three times and asserts no navigation nodes are created.

## 3. Lane-aware traffic

- The navigation graph remains the authored path/junction graph. Vehicles keep to the **right side** of the centerline using a per-node `LaneOffsetMeters` (1.5 m for the 10 m street). Lane offsets are authored data.
- `VehicleController` merges onto the parallel lane line with a one-time per-segment merge state, then follows the lane with a short pure-pursuit lookahead. This avoids the per-frame threshold oscillation and the 0.3 m residual bias found during PlayMode verification.
- Opposing vehicles are ignored by the forward sensor when their ground direction is opposite; same-direction spacing and intersection reservation remain.
- A* routing, kinematic movement (no Rigidbody2D), and the TrafficDirector camera-bubble pool are preserved.
- **Measured result:** two 2 m-wide cars on the 10 m two-way street pass with ~2.95 m center-to-center separation (no yielding). The PlayMode test asserts ≥ 2.5 m and both routes complete.

## 4. Implemented production layout

- The existing authored area was re-scaled **in place** to a 3 × 3 grid of 50 m blocks at ground coordinates X/Y ∈ {0, 50, 100}. The home bay is at `(-12, 0)`, the market spur ends at `(50, 130)`.
- Street cross-sections are 10 cells wide: 2 sidewalk + 3 lane + 3 lane + 2 sidewalk, with dashed centerline, curbs and plain/edge tiles.
- Buildings use 10–14 m footprints with entrances/garages facing the street; vehicle sprites are 208 × 104 px at 64 PPU (~3.25 × 1.625 world units) for a 2 m × 4.5 m car.
- The additive Phase D edit adds an east loop: `node_d02_east_link (100,75)` → `node_d02_east_bend (120,75)` → `node_d02_east_outer (120,100)` → `node_d02_east_corner (100,100)`. It is a drivable alternative route and is covered by `CityExtensionTests`.
- Sprite sorting uses the project-wide custom transparency axis `(0, 1, -0.26)`; world sprites (buildings, cars, props) share sorting order 0 so the axis controls occlusion. Tilemaps keep their negative sorting orders.
- Camera framing is 22 orthographic for taxi mode and 14 for on-foot mode; PPU 64. Exact final camera/art style remains OPEN.

## 5. Verification and remaining human acceptance

- **EditMode:** 62/62 passed (headless `unity test --mode EditMode` and the connected Editor suite).
- **PlayMode:** 1/1 passed (headless `unity test --mode PlayMode`): real-time opposing traffic, separate lanes, no mutual yielding.
- **Visual evidence (committed):**
  - [docs/evidence/two_vehicles_passing_game.png](evidence/two_vehicles_passing_game.png) — Game View at the passing moment (~2.95 m center gap).
  - [docs/evidence/city_scale_scene.png](evidence/city_scale_scene.png) — Scene View of the metre-scale blocks, 10 m streets, sidewalks and buildings.
- Additional transient captures from the verification session remain under `Temp/pipeline-screenshots/` and are not versioned.
- **Authoring method:** the Phase C/D changes were applied through the connected Unity Editor via the official Pipeline/CLI C# commands and saved as normal scene/Tilemap/Prefab assets. They were **not** performed by mouse-driven Tile Palette GUI editing. Owner GUI validation steps:
  1. Open `OutsiderCity`, select `HomeNeighborhoodLayout/IsometricGrid`, and inspect the 10 m cross-section at the home street.
  2. In the Hierarchy, select `RoadNetwork/RoadNode_node_d01_sw_junction` through `..._se_corner` and use `RoadNetwork > Validate Network` to confirm no diagnostics.
  3. Enter Play Mode from the home bay and drive to the market and back; confirm the taxi keeps to the right lane and passes oncoming traffic.
  4. Select the `node_mid_east` → `node_d02_east_bend` → `node_d02_east_outer` loop and confirm the Phase D east street is editable with the Tile Palette.
- **Still open:** final art/pixel density, exact camera zoom/angle, traffic side, whether the 16 m class is used in the city, final lane markings/signage, home interior placement, and human visual acceptance of the placeholder kit.
