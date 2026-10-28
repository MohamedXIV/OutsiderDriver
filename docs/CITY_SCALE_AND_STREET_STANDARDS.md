# City Spatial Scale, Street Cross-Sections & 2D Isometric Authoring

**Status:** Owner-approved city-scale direction on 2026-10-24; numerical widths are **baseline design standards**, configurable by street class rather than mandatory for every unusual place. **Implementation update (2026-10-25):** the production `OutsiderCity` scene now implements the logical metre scale, a 10 m local two-way cross-section, lane offsets and metre-based vehicle motion; see §8. Final pixel density, PPU, camera zoom, props and final art remain open. **Owner directional visual acceptance (2026-10-25):** the updated scene is approved as the provisional production baseline for visual direction, not as final sprites, colors, camera or complete world design.
**References:** [Decisions](DECISIONS_AND_OPEN_QUESTIONS.md), [World Content Contract](WORLD_CONTENT_DATA_CONTRACT.md), [Visual Direction](VISUAL_DIRECTION.md); creative board: [Outsider Driver — World & Creative Bible](https://miro.com/app/board/uXjVEWYP5T8=/).

## 1. Authoritative spatial convention

- **One logical planning-grid cell represents 1 meter in the city's ground plane.** This is the canonical design language for blocks, footprints, streets, vehicles, kerbs, home/garage, pedestrian space and route distances. City-scale plans and placement must not use the former one-cell-wide abstract roads.
- A logical ground cell is a **square 1 m × 1 m on the city's ground plane**, displayed as an isometric diamond when projected into the game's 2D screen. This is **not** a claim that the diamond's screen width/height, Unity Transform X/Y deltas, or Unity's `Grid.cellSize.y` is a measured real-world meter. Separate **logical ground coordinates / physical meters**, **Unity scene-space/projection**, and **pixels/PPU/zoom**; calibrate and document the conversion before changing movement speeds, collision extents, camera sizes or fares. In particular, the current `IsometricZAsY` grid's `cellSize=(1, 0.5, 1)` is a projection/layout setting, **not proof** that the existing build already has coherent 1-meter real-world dimensions.
- Treat current placeholder assets and the existing narrow `OutsiderCity.unity` road tiles as **pre-standard geometry to revise**, not an approved full-width streetscape. Do not scale a whole scene or tune game speeds blindly. Apply the standard by deliberate scene-authoring work with tests and a before/after view.

## 2. Baseline dimensional kit (nominal, editable)

| Ground-plan element | Baseline width / length | Intended meaning |
| --- | --- | --- |
| Logical ground cell | **1 m × 1 m** | Planning/snap unit for all ground footprints |
| Ordinary taxi/car body | **2 m wide; 4–5 m long** | Nominal visual and motion-envelope starting point, not every alien vehicle's immutable spec |
| Ordinary travel lane | **3 m wide** | One driving lane; authored turning clearance and vehicle dimensions still matter |
| Sidewalk on each side | **2 m** | Typical walkable width before street-furniture obstruction, not a mandated universal minimum |
| Local two-way street | **10 m total** | 2 × 3 m travel lanes + 2 × 2 m sidewalks (no parking, median or bike lane) |
| Major two-way street, two lanes per direction | **16 m total** | 4 × 3 m travel lanes + 2 × 2 m sidewalks (no median or additional reservations) |
| Optional 3 lanes / direction | **22 m total baseline** | 6 × 3 m lanes + 4 m sidewalks |
| Optional 4 lanes / direction | **28 m total baseline** | 8 × 3 m lanes + 4 m sidewalks |

These are **easy-to-author nominal conventions** rather than civil-engineering codes, hard legal minima or a promise of eight-lane roads. A larger sidewalk, curb setback, parking bay, median, cycle track, bus stop, frontage, service access or traffic island **adds its own space** beyond the simple cross-section above. Narrow lanes/alleys and context-specific streets are exceptions designers may author deliberately and visibly; do not silently apply a universal 10 m street template everywhere. Different-sized vehicles and different alien bodies may justify later local adjustments.

Keep a small, reusable **street-class cross-section** definition if/when implementation needs shared widths; don't scatter magic numbers across separate visuals, navigation and data assets. No generalized city generator is authorized.

## 3. Separate rendered street width from navigation

- **Street graphics:** Render/paint the *full physical cross-section* in native Unity 2D Isometric Tilemaps/Sprites: contiguous driveable surface, lane markings/directions, sidewalks/curbs, access bays and intersections at consistent scale. A full 10 m local street must not render as a one-cell abstract ribbon.
- **Navigation graph:** `RoadNode`s and routes represent connected travel **lane centerlines, turns, junction choices, stops and ramps**. Do not place a RoadNode in each painted cell or conflate a reusable tile with a unique Node ID.
- A 2-way street's opposing directions need **distinct, sensible lane-center paths or equivalent lane-aware guidance** whenever vehicle meeting/collision/turning makes a single bidirectional centerline incorrect. The present sparse graph may need deliberate refinement, not replacement with a per-tile graph. Test separation, intersection reservations, turns, curb approaches and route alternatives at the new scale.
- Repeatable visual Road Tile/Prefab pieces **must not carry hard-coded, duplicate navigation IDs**. Scene-owned `RoadNode` identity must be unique and validated. Painting graphics does **not automatically add road links** unless a specifically reviewed authoring helper does so.
- Keep road connections consistent with *visible* intersections and elevation; bridges crossing above street level do not connect to the lower road without real ramps. RoadNetwork remains authoritative for driveable connectivity; the Tilemap remains authoritative for visuals.

## 4. Built environment in the same world scale

- Lots, building footprints, garages, doors, curb cuts, parking/loading spots, pedestrians and props all obey the **same logical meter grid**; no independent per-feature "tile width" system. Road/lot/house planning shares one spatial scale.
- The player's home remains **exactly two floors**: ground-floor garage/workshop and upstairs residence. Its design should allow a plausibly sized 2 m-wide taxi and access/clearance, rather than shrink a car to fit an undersized symbolic house.
- Modular lots and block footprints can use 1 m snaps or larger multiples for efficiency, but their size is **authored by design**; no automatic city generation.

## 5. Isometric presentation and Unity editing workflow

- **Keep TRUE 2D ISOMETRIC**: native Unity Isometric Z as Y `Grid`/`Tilemap`, sprites/Sorting/Tile Palette, not 3D geometry or a new camera projection. The current screenshot has an isometric diamond ground plane and therefore does not alone show that the projection is incorrect.
- **Art direction:** The existing scene is a **flat geometric layout/schematic**, NOT final visual acceptance. Author buildings with believable facade height and mass, entrances, shadow/occlusion cues, larger connected street surfaces, convincing frontage, lot boundaries and restrained sidewalks/props. Vary block/lot use organically within an authored city. Do not replace it with procedural noise, a giant prop system or 3D.
- **Editor visibility:** Inspect/select the `Grid` / active Isometric Tilemap and Tile Palette for the diamond-cell overlay and correct snapping. The default grey Scene View background grid can remain orthogonal; that alone does not invalidate an isometric Tilemap. Do not build a custom editor grid solely to rotate the background. `IsometricZAsY` sets the tile/grid coordinate convention; actual pixel art, camera size, sorting, PPU and sprite pivots remain subject to visual calibration.
- **Production authoring:** Continue native Unity Editor + Tile Palette + Prefabs + optionally the official Unity CLI/`com.unity.pipeline` for repeatable editor actions. Never restore `TaxiSandboxBuilder` or a monolithic successor.

## 6. Acceptance before district expansion

1. In a new small real authored production street sample, measure two 3 m lane strips and two 2 m sidewalks (10 m total), using the **logical ground grid** (not screen pixel widths); demonstrate a car ~2 m × 4–5 m in the same scale.
2. Check road paint and navigation: forward/outbound/return route, opposite lane separation, believable curb stops, corners and intersections, and an alternate route that follows actual tiles.
3. Move or extend street pieces manually with Tile Palette/Prefabs; demonstrate that road IDs remain unique and broken links are caught, without rerunning the original city-authoring script.
4. Show **Scene View (including the actual isometric cell selection)** and Game View screenshots at human-readable zoom. Check building height/grounding, curb/door relationship, readability and sorting. Do not call a screenshot aesthetically accepted without owner's approval.
5. Run Unity EditMode **and** PlayMode movement/traffic checks if Editor is accessible; distinguish those results from visual approval and standalone build behavior.

## 7. Explicitly still open

Exact pixel density/PPU, camera zoom/angle, sprite scale/pivots, lot/block dimensions, parking and transit lane standards, detailed intersection radius/curves, dynamic street-class data representation, special alley/minor-street exceptions and final visual art are **not** fixed by this decision. The four-region layout is still a **proposal**, not immutable geography.

**Core invariant:** Ground-plan units are coherent and near-real proportions, while the city remains **authored, stylized, approachable and easy to edit**.

## 8. Implementation status (2026-10-25)

This section records what was actually authored in the production scene; it does not change the approved standards above. Implementation was performed in the existing production area rather than a new sample scene.

### 8.1 Ground conversion and motion

- `IsometricGroundProjection` samples the live Grid. Measured production basis: `(0.5, 0.25)` for +X and `(-0.5, 0.25)` for +Y world units per 1 m cell axis. A world unit is **not** a metre by assumption.
- A* distance, max speed/acceleration/deceleration, forward sensors, intersection clearance and lane offsets all use ground metres. Cars use 9 m/s (taxi) / 7.5 m/s (ambient); the 4.5 m car sprite is 208×104 px at 64 PPU.
- No 3D or custom-projection change: native `IsometricZAsY` Grid, Tilemaps and sprites remain the workflow.

### 8.2 Authored production area

- The existing area was re-authored in place as a 3×3 grid of 50 m blocks at ground X/Y ∈ {0, 50, 100}; home bay `(-12, 0)`; market spur ends at `(50, 130)`.
- The 10 m local two-way cross-section (2 m sidewalk + 3 m lane + 3 m lane + 2 m sidewalk) is applied across the authored streets using `StreetSurface_Tilemap`, `SidewalksAndCurbs_Tilemap` and `CityLots_Tilemap`.
- Lane-aware traffic keeps right with 1.5 m lane offsets; vehicles merge once per segment and follow a short pure-pursuit lookahead. Real-time measurement: **2.95 m center-to-center** passing gap, no mutual yielding.
- Additive east loop: `node_d02_east_link (100,75)` → `node_d02_east_bend (120,75)` → `node_d02_east_outer (120,100)` → `node_d02_east_corner (100,100)`. It is an explicit drivable route covered by `CityExtensionTests`.
- Custom-axis sorting `(0, 1, -0.26)`; world sprites share order 0 so the axis controls occlusion. Owner camera direction retained; 22 ortho taxi / 14 ortho on foot.

### 8.3 Verification

- **EditMode:** 62/62 passed in the connected Editor and headlessly (`unity test --mode EditMode`).
- **PlayMode:** 1/1 passed headlessly (`unity test --mode PlayMode`): real-time opposing pass, separate lanes, no mutual yielding.
- **Visual evidence:** [city_scale_scene.png](evidence/city_scale_scene.png) (approved directional baseline) and [two_vehicles_passing_game.png](evidence/two_vehicles_passing_game.png). Later visual-polish evidence is listed in §9.
- The production changes were applied through the connected Unity Editor/Pipeline C# API and saved as normal scene/tilemap/prefab assets; **mouse-driven Tile Palette editing was not performed**. Owner GUI validation: open `OutsiderCity`, select the Grid/Tilemap, run `RoadNetwork > Validate Network`, inspect the 10 m cross-section, and drive the taxi in Play Mode.

### 8.4 Remaining gaps

- Final art/pixel density/PPU/camera, traffic side (right-hand is a technical demo convention), the 16 m class in an actual scene, final lane markings/signage, and home interior/on-foot layout.
- Lot variety, frontage and everyday street props are addressed by the subsequent visual-polish pass and recorded in §9.
