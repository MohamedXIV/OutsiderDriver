# Traffic behavior Batch A — #28, #29, #30

> Batch B now integrates authored curb stops and taxi gameplay. Use [TRAFFIC_BATCH_AB_INTEGRATION.md](TRAFFIC_BATCH_AB_INTEGRATION.md) as the single current local authoring and Unity QA gate; notes below retain Batch A history.

## Delivery state and authority

Continues draft PR #35 from `759bb16`, with remote main `9777338` inspected on 2026-11-01. Owner explicitly requested this combined code batch and deferred **all Unity compilation, Editor, EditMode, PlayMode and Game View execution** to one later local integration phase. P-024/P-031/P-032/P-036/P-037 and Traffic & Transportation A2/B1/B2 apply. No art/PPU/traffic-side/transit product decision is inferred. No scene, road topology, package or asset placement is changed. This is authorable runtime support plus regression source, not shipped/visually accepted production signals or turns. All referenced issues remain open.

## #28 — junction and following hardening

A reservation has two distinct states: speculative approach and physical commitment. The vehicle requests before its front enters `IntersectionClearanceRadius`, using body half-length and minimum gap as an approach margin. Following constraints or red/amber prevent acquiring an uncommitted reservation. A committed owner retains protection while braking/stopping; passing a waypoint does not release it. Linear movement releases only after the rear clears the radius. An authored turn retains the reservation through its entire curve, whose exit must be beyond the rear-clear boundary. Disabling/removing a vehicle releases its reservation; there is no unsafe timeout that grants passage through an occupied car.

Waypoint advancement checks ownership before changing path index, including when a route begins inside an occupied node's arrival radius. The old tie-break and dead-end destination rules remain. The equal-arrival fixture now places exits 16 m from its junction instead of 6 m: a 4.5 m vehicle must be able to leave the 6 m conflict radius completely before parking at its terminal. The regression still requires both tied vehicles to finish.

Terminal snapping uses the lane pose checked before index advancement. Cancellation retains current elevation. The existing following equation, bumper gap, dynamic stopping horizon and emergency travel bound remain. The emergency bound can exceed comfortable deceleration if a leader stops abruptly or a frame is very long; it prevents new advance into a detected gap and cannot repair existing overlaps.

## #29 — authored trajectories, speed and heading

`RoadTurnPath` represents one directed incoming→junction→outgoing movement using four **manually placed** Transform controls. It never derives curves or roads from the graph. `Prepare` converts controls through the existing isometric projection and caches a 32-interval arc-length approximation. It validates graph connections, finite geometry, no interior stationary cusp, right-hand lane endpoints (0.15 m tolerance) and entry/exit tangent alignment. It supports same-elevation curves; elevation-changing ramps retain legacy guidance until separate ramp-trajectory semantics are authored. An invalid/disabled/ambiguous registered authored movement stops safely instead of reverting to a diagonal chord.

VehicleController approaches the authored entry, follows positions by distance along that exact cubic, and advances the junction waypoint at its authored exit. Following projects candidates onto the same cached curve, then continues along the outgoing lane. Curvature caps speed via `sqrt(maximumLateralAcceleration / curvature)`; a sampled braking envelope lowers speed before sharp curvature. CarModelSO supplies lateral acceleration and visual heading rate. Physical tangent and visual heading are distinct; heading changes are rate-limited. Existing four-direction sprite events remain available. Continuous rotation of a separately authored visual child is opt-in; existing sprite rigs are not blindly rotated.

`NavigateTo`/alternative requests during traversal validate a route from the outgoing node, resume if paused, and apply it after the authored exit. Unreachable requests return false. Direct `NavigateAlongPath` replacement during traversal returns false. `CancelRoute` during traversal drains the current movement to its safe exit before discarding the remaining route; another valid navigation request can supersede that cancellation. This intentionally changes cancellation timing for crossing safety. Cancelling other committed motion still retains its physical reservation; consumers must not interpret route cancellation as removing the vehicle body.

## #30 — local exclusive signals

`TrafficSignalController` owns an explicitly assigned junction and approach array. `TrafficSignalApproach` assigns incoming node, stop-line Transform and phase group. Configuration uses unique contiguous group indices; **one approach per group**, so no unsupported inference of non-conflicting simultaneous movements is made. The controller cycles green→amber→all-red→next group. Amber forbids new entry; committed cars finish. All-red extends while the junction reservation is occupied. Disabled, duplicate, missing-reference or invalid-duration configuration fails closed. State properties are available for later light presentation; no visible production lights have been placed.

A vehicle with a controlled target and no valid matching approach stops. Stop-line geometry must be on the incoming lane and upstream of the conflict radius; body front plus a 0.1 m margin stays behind it. Signal braking feeds the same longitudinal speed/travel caps as following and turns. Green permission still requires junction reservation. Legacy uncontrolled proximity/tie arbitration is bypassed for signal-controlled movements, so a closer red-light queue cannot veto green. Uncontrolled junctions continue using the historical rules.

Registered disabled authoring components remain discoverable to prevent an accidental uncontrolled fallback. Authoring GameObjects must be active at scene load (components may be disabled): an object which has never awakened cannot enter a runtime registry without a scene preload. Hot structural editing of shared curve controls during traffic simulation is unsupported; stop traffic/reprepare routes after authoring edits.

## Authored content still required locally

- Manually place RoadTurnPath controls for selected real bends/junction movements, aligned to existing lanes. Ensure each full vehicle swept envelope stays within actual drivable surface and clear of sidewalks, opposing lanes and buildings. Endpoint checks alone cannot prove this from sparse road nodes.
- Keep curve entries upstream of commitment and exits beyond conflict radius + half the largest allowed vehicle length. Do not overlap consecutive trajectories or create conflict regions so close that a car cannot clear the first before needing the next. These invalid street/vehicle combinations safely wait but may jam; fix authored geometry rather than weakening reservations.
- Place at least one controller, its complete approach set and physical stop lines on an existing intentionally selected junction. Keep stop lines upstream of curve entries and outside conflict regions; add visible lamps bound to State. No existing production scene gains signals/curves merely by adding these classes.
- Verify all enabled/disabled component registration, node/Transform references and phase groups through the Inspector. Maintain original .meta GUIDs. No migration is required for existing scenes without these optional components.

## Evidence: authored versus executed

There are 37 additional cases in this batch, giving 52 relevant authored cases in total. Authored relevant cases: CarFollowingTests 18 (including prior PR tests), RoadTurnPathTests 11, TrafficSignalTests 8, TrafficBehaviorIntegrationTests 15. Coverage includes queues/tick orders, committed braking ownership, occupied initial waypoint, lane/elevation filtering, curve geometry/speed/headings, red/green queues, red crossing priority, phase change at commitment, cancellation/rerouting/unreachable requests and route completion. Historical traffic/ride tests remain in place.

Executed cloud checks:

- Tree-sitter C# syntax parsing: all 97 source files parsed without syntax errors. This does **not** resolve C# types, assemblies or Unity APIs.
- `git diff --check`: passed. Reproduce source/metadata checks with `python tools/traffic_static_checks.py` in a Python environment containing `tree-sitter` and `tree-sitter-c-sharp` (cloud invocation used `PYTHONPATH=/tmp/traffic-static-parser`).
- Asset .meta duplicate-GUID and new-source metadata checks: passed. Existing production scenes/prefabs/assets, package manifests, lockfile and assembly definitions remain unchanged.
- `python tools/traffic_numerical_smoke.py`: passed independent scalar following/stop/restart-queue, stop-line and cubic geometry/curvature calculations. At 20/100 ms the scalar red front stops near -16.1 m for a -16 m line; the 11.5 m-radius cubic fixture has ~18.064876 m cached arc length and midpoint speed cap ~5.378717 m/s at 2.5 m/s² lateral acceleration. These are Python arithmetic experiments, **not execution of C# or the Unity test methods**.
- Static code review performed on signal/curve helpers and controller integration; identified ownership, signal-priority, commitment timing, reroute/cancel and terminal-pose defects were corrected and regression source added. Static review cannot establish rendered safety or absence of runtime defects.

**Not executed:** Unity compilation/import, EditMode, PlayMode, Game View, actual production rides, soak, builds or profiling. No tests are described as passing. The authored Test Runner assembly already includes the Navigation/Vehicles namespaces through OutsiderDriver.Runtime; no package or assembly dependency was added. All new runtime APIs use existing UnityEngine/C# collection facilities.

## Batch B (#27) integration risks

- Stop anchors must be physically outside conflict regions and off drivable through-lanes, not simply renamed RoadNodes. A stationary committed vehicle deliberately keeps protection; do not add reservation expiry to make traffic pass through it.
- CancelRoute during a turn now finishes that movement before becoming idle; ride/UI state and any destination-reached listeners must tolerate this timing. Do not teleport or start boarding immediately when requesting cancellation.
- Use NavigateTo return values. Direct path replacement may be refused during a turn; queue a reachable destination or wait for the exit. Preserve incoming movement identity when accessing signals/curves.
- A parked controller without route history has no authored elevation metadata; establish a legal route/arrival before parking at elevated anchors. Stopped physical vehicles stay registered; visual-only parked-car props remain outside this system.
- Connect curb approaches/merges to the same measured lane/trajectory convention. Check vehicle dimensions, body clearance and stop-line positions after any new model or bay geometry. Existing unsafe taxi destinations are not solved in this batch.

## One unified local Unity QA phase

1. Check out the latest PR #35 branch with Unity **6000.6.0f1**. Resolve the pinned package lock without upgrading dependencies. Confirm project import and zero Console compilation errors; inspect every new component and metadata reference.
2. Run **all EditMode** tests and export fresh XML/counts, including all cases in the four suites above plus RoadYieldingTests and TrafficDirectorTests. Failures/skips/zero-test results are distinct from passes. Specifically run both commitment tick orders and the occupied-waypoint guard.
3. Run **all PlayMode** tests, including VehiclePassingPlayModeTests and the production two-ride taxi tests. Export fresh results. Recheck terminal lane placement and both historic deadlocks after any fixture/code correction.
4. Author the selected turn trajectories and one signalized junction by hand in an intentional scene change. Validate right/left and short turns, lane endpoints, complete body envelopes, missing/disabled/duplicate authoring, stop-line positions and model tuning. Bind real visible signal lamps to State. Record this as scene integration, not as work already present in this code batch.
5. At normal time scale/speeds, observe red/amber/all-red/green over multiple cycles, occupied-clearance extension, green release despite closer red cross traffic, queues on both approaches and a stopped leader after the junction. Change phase on the exact entry step and confirm the committed car clears before cross traffic is permitted.
6. Capture normal-speed turn and following footage: faster catching slower car, progressive stop/restart, three-car queues, both lane directions/adjacent lanes, curves, elevations, route changes and cancellation on/before/after the turn. Check no teleport, diagonal cut, overlap, oscillation, premature reservation release or permanent yield. Include safe-curb tests when Batch B lands.
7. Complete two consecutive normal player-operated Accept/Board/Collect rides with Batch B integrated. Until then record #27's stop-location limitation separately. Owner gameplay acceptance remains separate from automated results.
8. Profile a **15-minute** normal-speed production traffic session and a representative built runtime. Record active car count, CPU/frame, allocations, queues/recovery and Console errors. Curve queries are bounded/no per-tick collections; following remains bubble-local O(vehicles² × bounded path work), with extra per-curve projection and curvature sampling. Measure before claiming performance.

Do not merge or close #28/#29/#30/#22/#17/#26 from this cloud evidence. There is no request for an intermediate Gemini/Unity run between these issues.
