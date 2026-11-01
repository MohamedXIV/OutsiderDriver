# Outsider Driver — Traffic Behavior & Public Transportation Direction

**Status:** Owner-approved direction, 2026-11-01. **Planning and acceptance contract, NOT an implementation claim.**
**Authority:** Decision Register P-037/P-038, P-021/P-024/P-031/P-032/P-036, [Game Design](GAME_DESIGN.md), [City Standards](CITY_SCALE_AND_STREET_STANDARDS.md), and [Active Roadmap](ROADMAP.md).
**Tracking:** The new Traffic & Transportation Epic (linked from the roadmap); the existing taxi Epic #17 and owner acceptance #22 remain open until core comfort/safety issues are resolved and playtested.

## The goal, not a full traffic simulator

**Driving through the city and observing it are central to the game.** Completing a ride in PlayMode is a technical baseline, not proof that the traffic feels natural or that stops happen in safe, plausible places.

Current Unity 2D isometric production facts (reviewed on `main` at `27ecf2e`):
- `VehicleController` already has a forward sensor, kinematic acceleration/deceleration, sparse road-node guidance, simple pure-pursuit lane offsets, intersection yield/reservation and deterministic tie-breaks; `TrafficDirector` pools cars in a camera bubble.
- **Gaps:** following and braking respond mostly as an on/off yield rather than speed/distance management; corner transitions may feel abrupt; there are **no operating signal phases**; the ride service still treats a destination `RoadNode` as the physical boarding/dropoff point, which can be an intersection.
- The earlier agent reported 89/89 EditMode, 2/2 PlayMode and a ~12.2-minute traffic soak after fixes. These are **reported verification**, not proof of polished driving or owner sign-off. Issues #4/#5 implemented the earlier baseline and remain closed, not a certification of future sophistication.

## Locked constraints

1. **Fully handcrafted city, permanently:** all geography, street geometry, intersections, bridges, buildings, lay-bys, track beds and placements are designed and authored by people in Unity 2D Isometric (P-036). **No procedural city, road or building generation**, whether at runtime or via automatic editor layout. A data-driven/transit scheduler can use *authored* infrastructure.
2. **Semi-autonomous, kinematic vehicle motion** (P-024). Continue with meter-based navigation and current 2D sorting; no default rigidbody/physics-simulator rewrite or monolithic traffic brain.
3. **Procedural/systemic taxi work** (P-034). Real ride offers must use eligible **safe pickup/dropoff locations** over the handcrafted traversable network. A random valid road-node is not automatically a legal stopping point.
4. **Player-facing stop interactions remain voluntary:** Accept, Board, Collect continue to be explicit actions. Roads should remain usable while the taxi waits for boarding or fare collection.
5. Keep authored data identities stable and separate from localized display names. No arbitrary canonical alien names or redesign of the existing map to satisfy a toy traffic demo.

## Phase A — Immediately: P0 comfort/safety blockers

### A1. Safe curbside boarding and dropoff, with explicit stop anchors

**Do not conflate:** (a) navigation/intersection `RoadNode`; (b) physically safe, authored curbside/lay-by `StopAnchor`; (c) passenger location/`CityLocationAnchor`.

- Author a few real curbside taxi stopping positions in the existing production scene near relevant locations. Each must specify approach/access from a legal road/lane, intended grounded stopping pose (position and direction), and no-stop exclusions for junction conflict zones. Stop bays, kerb cutouts or safe pull-over areas need authored visuals and appropriate actual space; a purely logical offset does not make a roadside stop safe.
- Resolve passenger requests to reachable **legal stop anchors**, not merely to the nearest road intersection. Road connectivity and quote length still come from the authoritative `RoadNetwork`; pickup/dropoff completion requires the taxi reaching the **actual stop pose**, not merely crossing an unrelated intersection node.
- Allow a simple initial method using a small authored stop pool; don't rewrite the whole graph. **No suitable stop -> no offer**, not teleportation or unsafe snapping. Keep transitions, active ride cancellation and fare accounting coherent.
- Waiting taxi must not occupy/reserve an intersection, block cross traffic or create a deadlock. Safe stops may still need traffic handling for a curbside maneuver. Test both taxi and ambient traffic with a waiting passenger action.
- The player can still get meaningful work after this filtering; validate enough safe eligible pairs in the current small city.

### A2. Traffic awareness / basic longitudinal control

- Vehicles should adapt **target speed** to a visible leading vehicle in the **same usable lane**, a queue, or a stopped obstacle, not merely switch yielding on/off at a fixed sensor distance.
- Use ground-metre gap and closing speed, headway/stopping-distance/braking margin bounded by vehicle size, speed and deceleration; follow at a believable stable distance, slow progressively and stop before contact. Handle stop/start queues and a slow leading car; avoid oscillation, collisions and permanent yields.
- Preserve opposite-lane passing and intersection right-of-way. Sensors must neither ignore a real vehicle in the same lane nor treat perpendicular/yielding cross traffic as a same-lane forward car. Avoid false lane detection at tight curves and elevations.
- Regression tests cover: slower leader, fully stopped leader, restart, intersection queues, two opposing lanes, and the two previously fixed deadlocks. Keep values data-driven, measured, and economical for camera-bubble vehicles.

**P0 acceptance requires a human-readable normal-speed Game View check after A1/A2**, plus the existing automated ride tests. Other phases below are **not** conditions for closing the first taxi-shift milestone.

## Phase B — Traffic 2.0, after a usable P0

### B1. Smoother, natural turning

- Retain sparse topology, add **authored drivable corner/approach geometry** or sampled designer-defined curves when needed. Smooth heading and visual facing, limit speed by turning curvature and approach distance, avoid pivot/snap turns and lateral lane cuts.
- Separate **road connectivity** (which route) from **drivable movement geometry** (how the car follows its lane). Do not run procedural street generation or create road nodes per tile.
- Cover intersection corners, turning through queues, alternate roads, visible sprite orientation and stop approaches.

### B2. Traffic signals and junction controls

- Author signalized intersections selectively: stop lines, coherent phased right-of-way, amber/clearance if suitable, and signals visible in the world. Uncontrolled junctions, yields and roundabouts continue to use appropriate rules.
- Stop gracefully *before* the line on red; signal changes and lane queues must not deadlock. No all-city traffic-light controller and no need to simulate every pedestrian phase immediately.
- Explicitly account for vehicles approaching/occupying conflict areas. Existing node reservation logic alone does not implement signals.

## Phase C — Authored public transportation and larger city infrastructure

**These are approved features/directions, NOT currently implemented and NOT P0 blockers.** Favor one vertically complete small authored example before copying systems across districts.

### C1. Buses and bus bays

- Ground-contact buses use the authored street network with vehicle-type dimensions/turn radius and regular **authored routes and stops** (not random taxi offers).
- Selected stops include **recessed bus bays / lay-bys** integrated into the pedestrian curb line and built by hand. Preserve adequate pedestrian walkway: allocate enough extra sidewalk/frontage width instead of simply consuming the only walking area.
- Buses approach, decelerate, enter the bay, dwell for boarding/alighting ambiance, yield/merge back into traffic safely. Avoid placing stops in conflict junctions. Initial minimal bus system can be ambient-only; player ridership is a separate decision.

### C2. Bridges, overpasses and authored ramps

- Implement a contained real **two-elevation** authored example using existing `ElevationLevel` / `IsElevationRamp` checks, 2D visual layers, sorting/occlusion and legal connectivity. No implicit routing at a visual crossing.
- Validate visually aligned deck/rails/ramps, vehicle continuity at the height transition and readable shadows/occlusion. All bridge geometry is hand-designed.

### C3. Tram

- Use **authored rail paths, switches, stops and road/rail crossings**; rail vehicles follow track constraints, not arbitrary road-node lane changes. Prioritize convincing movement, signals/priorities at conflicts and safe stop positions.
- The initial tram can be visual/ambient service with schedules; player ridership, tickets and tram network strategy are separate future decisions.

### C4. Monorail

- **Authored elevated single-beam guideway and stations**; separate from car lanes and conventional tram tracks. Support movement, station dwell and proper isometric occlusion at road crossings.
- Minimal first implementation may be ambient only. No new NPC economy or public ridership system required.

## Technical composition, not mandated class proliferation

Prefer preserving existing `RoadNetwork`, `RoadNode`, `VehicleController`, `TrafficDirector`, `RideLifecycleService`, `IRideDemandService`, world registry and Unity scene-authored assets.
Where a real player problem demands it, give explicit responsibilities to a **small stop-anchor/vehicle-speed policy** rather than extending road intersection nodes with every unrelated feature. Use one shared 1m ground-plane convention and test real authored scene geometry. A new helper type is justified by a genuinely independent responsibility; don't create a universal multimodal transport simulation layer in advance.

## Release/acceptance gates

- **P0:** safe, non-junction taxi pickup/dropoff + basic sensible car following/braking + owner-operated repeat taxi shift, tests and normal-speed observation. The existing PlayMode test is necessary, **not** the whole acceptance criterion.
- **Traffic 2.0:** visible turn quality and optional signalized intersection with regression tests and live normal-speed Game View validation.
- **Transit:** each mode must show meaningful authored infrastructure plus real movement and avoid wrecking road/rail crossing rules; do not mark a bus/tram/monorail implemented based solely on a prefab.
- **Performance:** continue bounded camera bubble/pooling and measured on-screen behavior. Do not claim long-run performance from one short or synthetic unit test.

**Product north star:** A city intentionally designed by hand, with credible vehicle behavior and public transit, where the player's taxi life arises systemically without authored job sequences.
