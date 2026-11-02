# Issue #28 — implementation and local verification

> Batch A supersedes the initial reservation/trajectory integration notes below. See [TRAFFIC_BATCH_A.md](TRAFFIC_BATCH_A.md) for the current implementation, cancellation semantics, review fixes and unified deferred Unity QA plan.

Authority: P-024/P-031/P-037, Traffic & Transportation §A2; Epic #26.
Base: remote main `97773386de5f738e3957733dab4c0610d1e2c104` (read/fetched during this task); no overlapping open PRs at inspection.

## Scope and decisions

Keep the existing kinematic movement, road graph, lane steering, TrafficDirector bubble/pooling and handcrafted city. No scene/assets, topology, safe-stop (#27), signals, turn-authoring (#29), or ride changes. Canonical art, public ridership and other OPEN product choices remain undecided. This is a C# implementation for local verification, not visual acceptance or permission to close #28/#22/#17.

A small stateless `CarFollowing.TargetSpeed` supplies longitudinal control. Gap subtracts half of each model's length from measured path distance. Cruise speed is capped by time headway and a braking budget:

`v*T + v²/(2*b) <= gap - minimumGap + leaderSpeed²/(2*leaderDeceleration)`.

The independent gap/headway cap converges to a spacing of minimum gap plus speed times headway. Current leader speed relative to follower speed changes whether the existing acceleration/deceleration moves speed up or down. Paused/idle leaders are treated as stationary because their existing movement path stops immediately. Default model length 4.5 m, minimum bumper gap 2 m and headway 1.2 s are initial engineering tuning, not owner acceptance. CarModelSO exposes these values; model-less pooled vehicles use matching defaults.

Sensing follows lane-offset route segments within a speed-dependent stopping horizon (existing sensor range is the minimum). Heading agreement rejects opposing/crossing moving traffic; the existing corridor rejects adjacent lanes. Idle vehicles remain obstacles regardless of stored heading. Route elevation is interpolated on ramps and compared to the candidate segment; idle/cancelled vehicles retain their current elevation. Junction priority, deterministic equal-arrival tie-break, and forward-queue checks before reservation remain. A blocked approach releases its reservation and reacquires after clearing. A reduced-speed follower stops before waypoint advancement if it cannot own the upcoming junction; moving queues must reacquire before crossing. No changes to dead-end destination filtering.

Normal speed changes use existing acceleration/deceleration. An emergency per-step travel bound preserves minimum spacing when a leader stops abruptly or a long frame exhausts braking room; this can exceed comfortable deceleration. It cannot repair an already overlapping spawn. There is no teleportation to resolve queues.

## Checks and limits

- Added EditMode regressions: slower leader; progressive stationary braking at 20/100 ms ticks and restart; three-car queue in both tick orders; adjacent lane/elevation filtering; corner lookahead; reservation release/recovery; moving queues at unowned junctions; terminal footprint overlap; cancelled elevated leaders; offset-corner lane separation; braking-budget arithmetic.
- Retain `RoadYieldingTests` equal-arrival and blocked-reservation regressions, TrafficDirector dead-end tests and real-time opposing-lane PlayMode test. The old sensor test moves its obstacle fully clear of the route rather than expecting release while still inside physical stopping spacing.
- Cloud: `git diff --check` passed. Independent Python numerical experiment of the equations passed stationary-stop, slower-leader and three-car-queue cases in both tick orders. This experiment is not execution of the C# or Unity tests.
- Unity Editor/CLI and C# compiler were unavailable. Installing a standalone compiler was blocked by system package-directory permissions. No Unity compilation, EditMode, PlayMode, rendered behavior, production soak or performance result is claimed.

One following scan per moving vehicle per tick, over active bubble vehicles and route segments inside the horizon; no physics queries, LINQ or per-tick collection allocations. Worst-case quadratic in active vehicle count, with segment lookahead; current bubble defaults to six cars. Actual CPU/frame behavior requires profiling.

Known uncertainties: existing lane merges/corner steering are approximated by a lane polyline for sensing; very tight bends, sparse short segments and ramp height tolerance need local inspection. No authored lane IDs exist, so lane membership uses geometry/heading rather than an invented road schema. Reservations remain the existing node-based conflict model, not geometric swept-body collision handling. Terminal snapping is gated on free travel; pre-existing idle/spawn overlap is not repaired. Vehicles created idle on elevated roads without route history lack elevation metadata (default ground); route them before parking. Existing unsafe junction taxi stops remain #27 work.

## Exact local Unity checklist

1. Check out this branch, open in **Unity 6000.6.0f1** (ProjectVersion.txt), allow the pinned packages to resolve, and confirm zero compile errors in Console. Do not upgrade packages/editor as part of verification.
2. In Window → General → Test Runner, run **all EditMode tests**. Confirm `CarFollowingTests` executes every parameterized case, plus `RoadYieldingTests` (including both historical deadlocks) and `TrafficDirectorTests` (dead-end destination avoidance). Record counts/results and export XML.
3. Run **all PlayMode tests**, including `VehiclePassingPlayModeTests.OpposingVehicles_PassInSeparateLanesInRealTime` and the production two-ride tests. Export fresh XML; distinguish failures/skips from passes.
4. Open existing `OutsiderCity` from Editor Build Settings. At time scale 1 and normal model speeds, observe faster car catching a slow car, braking behind stationary taxi, full queue stop and smooth restart. Inspect no bumper overlap or repeated stop/go oscillation. Use temporary runtime model overrides or an unsaved test fixture; do not alter authored production roads/scenes.
5. Observe three-car queues, both opposing and adjacent lanes, corner approaches and any available authored elevation crossings. Confirm cars in other lanes/levels do not impose false stops and a same-lane car beyond a corner is detected.
6. At the previously affected `node_mid_center`, repeat equal-arrival perpendicular traffic, a stopped taxi with following cars, moving cross traffic, then clear the taxi. Confirm blocked followers do not hold the junction reservation, cross traffic drains and followers restart. Repeat after route cancellation and ambient pool reuse/dead-end rerouting.
7. Complete two consecutive player-operated Accept/Board/Collect rides. Record the known #27 stop-location limitation separately; do not treat this PR as implementing safe curbs.
8. Run a normal-speed 15-minute traffic session with Profiler CPU/GC and Console open. Record car count, frame cost, GC, queue recovery and any exceptions. Save normal-speed clips showing slowdown/stop/restart and both fixed junction cases. Human Game View acceptance is still required; leave #28 open.
