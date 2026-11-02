# Outsider Driver — Street-first taxi work, map, pedestrians and energy

**Owner direction:** 2026-11-02. **Status:** player-experience correction after direct owner playtest of PR #35, not a claim of full implementation.
**Precedence:** This document **supersedes** the fixed TaxiStopAnchor-for-every-fare restriction in P-037, Issue #27's original wording and any proposal that all starting work appears in an Uber-style offer list. It retains P-024 kinematic driving, P-034 systemic opportunities, P-036 entirely hand-authored city topology and P-037's essential no-standing-in-intersections safety rule.
**Related:** [Game Design](GAME_DESIGN.md), [Decision Register](DECISIONS_AND_OPEN_QUESTIONS.md), [Systemic Demand](SYSTEMIC_OPEN_WORLD_AND_RIDE_DEMAND.md), [Traffic & Transportation](TRAFFIC_AND_TRANSPORTATION.md), [Roadmap](ROADMAP.md), PR #35.

## Owner-observed current reality (PR #35, not main)

The owner personally reports: actual taxi jobs complete; cars slow for vehicles ahead and restart; taxi enters the authored bays; traffic signals operate. However **congestion/deadlocks still occur** and vehicles **visibly slide sideways through turns then correct their sprite facing**. Existing 4 authored recessed bays and the signalized junction represent a valid technical integration proof, not final taxi-stop or turn quality. Do not close owner acceptance #22 / taxi P0 #17 just because Unity's reported 191 EditMode and 2 PlayMode tests passed. Avoid silently promoting tests or technical prototypes to finished city aesthetics. PR #35 remains a draft pending gameplay revisions.

## 1. First-day street taxi loop — APPROVED

**Default work starts organically on the streets**, not on a phone-style dispatch board, list of remote fares, or fixed taxi stop network.

1. Player freely explores/rides around the hand-designed city, looking for *physically present* people who want a taxi. Everyday demand remains **systemic** from time, place, resident needs/availability and valid routes.
2. An eligible NPC can wait/wave near a sidewalk; they are visible in the local world (within the budgeted camera bubble) and have a stable underlying world/request identity. Player chooses whether to approach/stop; no compulsory work chain. Refusing/ignoring is fine.
3. The taxi pulls over to a **contextually valid roadside stopping position** near the NPC, not an intersection or unsafe lane/crosswalk. Boarding is explicit and occurs only when stopped safely; the passenger can move a short distance to the vehicle where appropriate. No miraculous boarding while the taxi is driving or forced dedicated bay.
4. Passenger requests a concrete location/address; destination is resolved to a legal nearby curbside dropoff with the route determined on actual authored roads. Not every address has its own fixed taxi bay. Driver may roam when empty and select another location.
5. Pickup/dropoff, quote, payment, world time and identity still use existing `RideDemandService`, `RideLifecycleService`, `RideFareCalculator`, `RoadNetwork` and UI plumbing where applicable. **Change the discovery/access/presentation** rather than deleting all working demand/state machinery.

**Important:** Street-hail visibility is a representation of systemic demand, *not* world generation: don't instantiate heavy NPC simulations for the whole city or convert authored rides to static schedules. Demand should be present in the world even when not shown as a remote job-card list.

## 2. Taxi roadside stops versus bus bays — APPROVED correction

A **dedicated, physically recessed bay** is a good fit for bus stops and sometimes taxi ranks, service stands and major terminals. **It is NOT a universal prerequisite for taxi fares.** Preserve PR #35 authored `TaxiStopAnchor`, curb manoeuvre/lease/clearance systems where useful; make them an optional preferred stop type, not the only legal `RideOffer` source.

New concept: manually **authored curbside permitted stopping zones / ranges** on existing sidewalks/streets (no roads/curbs generated). At runtime, a small safety/geometry query can pick a **temporary physical roadside stop pose** near the requested pickup/destination: correct side of road, lane/heading, clear of junction conflict/stop lines, driveable clearance/visibility, not occupied by an active vehicle. Player stops only where appropriate, with sensible short dwell; fallback is a nearby suitable segment/address access point or unavailable pickup, **never snap/teleport/stop in the intersection**. Some curbs may disallow stopping. Where pickup and curbside stopping are separated, let the pedestrian reach the car when supported.

Address, point of interest and location identity remain separately authored `CityLocationAnchor`/stable IDs and can expand with the city. A named address is a **travel destination**, not necessarily an exact parking bay or road node. Keep physically safe last-metres routing, lane merging and stop completion from Batch A/B.

Bus bays (Issue #31) remain manually recessed into curb/sidewalk edges, with sufficient retained pedestrian passage and fixed stations. Not every bus or taxi stop must be instantiated from the same anchor class; share safety/manoeuvre policies where practical, not compulsory stop semantics.

## 3. Taxi access ladder — APPROVED order; official access details OPEN

**Tier 1 — Starting game:** street hails only. Player cruises to discover people. No official or underground dispatch apps at start. An existing work-list HUD is a temporary test/development interface and must not be the canonical entry gameplay.

**Tier 2 — Underground/informal dispatch:** later the player may buy/get access in suspicious or semi-legal neighbourhood locations to an unofficial popular Uber-like service used by fugitives, undocumented or informal workers. Jobs generally pay **less than official dispatch**, with socially risky or unusual clientele. This adds a convenience **request channel** over the same systemic travel/ride source, not a separate scripted mission economy. This is an *optional progression*, not mandatory criminal activity. Exact price, identity rules, risks, discovery, sanctions and gameplay gating are OPEN.

**Tier 3 — Official app:** legitimate dispatch may exist later, but *how an undocumented human obtains access, eligibility, licensing/identity and any consequences* are **explicitly OPEN**. Do not hardcode an unlock path or require official dispatch for the starting loop.

## 4. City-wide map and optional GPS — APPROVED core, detailed overlays OPEN

A **whole-city map** is accessible **even without GPS**. It is a travel/exploration tool, **not a jobs/mission list**. Player selects an authored geographic point/place and orders their semi-autonomous taxi to travel there, or can choose ordinary free roaming. Map selection is independent of holding a fare or owning an app. Do not require the GPS item just to open the map.

GPS (one of the optional initial equipment choices, alongside translator/radio per existing game design) adds *assistance* rather than granting the map: e.g. route guidance, place names, estimated congestion, likely passenger areas, day events where known and affordable. **Which specific overlays and prediction accuracies are truly available is OPEN** and depends on authored world information, not omniscient knowledge. Treat GPS as information rather than a work-offer generator.

Route selection must respect actual graph connectivity and legal turns/stop constraints. Map UI/zoom/labels/known vs unknown content styling are implementation decisions to validate with owner.

## 5. A lived-in city and visible walkers — APPROVED direction

Residents/pedestrians naturally walk around streets, sidewalks and activity locations, not only appear as taxi passenger cards. Use coherent local camera-bubble representation, safe pedestrian authored walkway geometry, simple local paths/animations and appropriate crossing rules; significant residents retain identity and lightweight coarse offscreen schedules. Ordinary background walkers can be pooled and transient. Avoid a full per-person life simulator or huge new pathfinding framework in the first slice.

**Street-hail passengers are part of this same visible street population**, not a parallel system of ghost markers disconnected from world actors. Keep opportunities generated logically when actors are offscreen; instantiate/present them when relevant nearby.

## 6. Vehicle energy, consumption and service stations — APPROVED requirement, technology OPEN

The taxi **has a finite energy/fuel resource**, travel consumes some, and periodic **refuelling/recharging at real stations** is part of ordinary life and route planning. The fictional energy technology is **not settled** (liquid, electricity, nuclear, other), nor are its exact price, rate, capacities, failures, UI terminology, service timing or difficulty. Do **not** canonize petrol/electricity/nuclear or force a chosen lore explanation.

An eventual technical system may use neutral `Energy`/range/consumption/station service abstractions, with balance/config data. Hand-author station sites and access in the city. Avoid trapping the player permanently with an empty resource during early playtesting; failure/recovery rules are still OPEN.

## 7. Owner feedback: traffic congestion and sprite-facing mismatch

**Reported working:** leading-car slowing/restart, authored bay pull-in, visible signal phases and systemic fare completion in the real Editor.

**Reported broken/unpleasant:** occasional traffic congestion despite tests; vehicle turns appear as sideways sliding with a late snap/visual correction. These are observable issues, not accepted Traffic 2.0 completion.

Investigate separately:
- Actual world-space velocity/tangent versus sprite facing at every turn; isometric sprite art convention, pivot, facing event and orientation smoothing; investigate **visual sliding versus physical diagonal corner cuts** independently.
- Enough heading/facing frames and proper interpolation/selection. Owner suggests 4, 6 or 8 sprites per vehicle; exact count not mandated. More frames can polish art but will not fix a wrong motion trajectory or mismatched tangent by themselves.
- Runtime congestion telemetry: vehicle IDs/position/speed, active target, signal phase, pending curb motion, junction reservation ownership, queue length and time stalled. Diagnose recurring cause rather than raising timeouts or bypassing right-of-way unsafely.
- Real-time no-collision/natural turn checks in actual Game View, plus sustained soak and owner review. Do not mark PR #35 merge-ready from test counts alone.

## 8. Development sequence (suggested)

1. **Fix player-visible turn/facing mismatch and diagnose remaining traffic stalls**, retaining successful tests; keep PR #35 draft. The owner has already verified rides, slowing and bays work, so don't reimplement those.
2. Restore **street-first work** by making remote offers a data source, not starting-game job UI; integrate local NPC hail and safe *contextual* curbside stops, keeping dedicated bays optional.
3. Give player **whole-city map click-to-travel** even without GPS; add clear optional GPS aid only after core travel works.
4. Establish lightweight **walking street life** sufficient to ground street hails; sequence alongside step 2 as necessary.
5. Add **fuel/energy stations** using neutral semantics; agree energy technology/balance later.
6. Add underground dispatch (and official access much later) without replacing street hails or making new hardcoded quest chains.
7. Buses/bridge/tram/monorail remain planned under Epic #26; use recessed bays for buses, separate authored rail guideways and elevation logic.

**No new implementation is claimed by this document.** End of owner correction.
