# Outsider Driver — Current Roadmap & Verified GitHub Issue Status

**Updated:** 2026-11-02. **Planning state:** Owner personally tested the PR #35 Unity integration: rides, car-following, bay docking and signals work, but congestion and turn-facing presentation remain incomplete. PR #35 includes implementation of #27–#30 but remains a **DRAFT** with no merge; the new street-hail-first player experience is NOT implemented. Original historical P0 table below needs to be read in conjunction with the newest owner correction section.
**Authority:** [Game Design](GAME_DESIGN.md), [Decision Register](DECISIONS_AND_OPEN_QUESTIONS.md) P-034–P-038, [Systemic Sandbox](SYSTEMIC_OPEN_WORLD_AND_RIDE_DEMAND.md), [Traffic & Transportation](TRAFFIC_AND_TRANSPORTATION.md) and [AGENTS.md](../AGENTS.md).
**Live work tracking:** [Taxi Gameplay Epic #17](https://github.com/MohamedXIV/OutsiderDriver/issues/17), [Traffic & Transportation Epic #26](https://github.com/MohamedXIV/OutsiderDriver/issues/26). GitHub issues/commits override the dated snapshot in this file.

## LATEST OWNER PRODUCT PRIORITY — 2026-11-02

**Override:** [Street-first Taxi Life, Maps and Energy](STREET_HAIL_CITY_LIFE_AND_EQUIPMENT.md) is the newest design authority (P-039–P-043). **Taxi work must start from finding visible people in the street**, not using the remote work offer-board as if a dispatch app were already owned. The same P-034 systemic demand, resident identity, ride service, fares, time and legal road graph remain valuable. **Fixed recessed bays are optional taxi ranks / future bus bays**, not mandatory targets for every fare: select safe contextual roadside positions along human-authored curbs (P-040). Whole-city map and location click-to-travel work without GPS (P-041). Lightweight walkers and hails are part of city life (P-042). Finite taxi energy/fuel and authored service stations are planned, but the power source and consequences are undecided (P-043). An optional informal underground-dispatch app can come later, with formal app access explicitly open.

**PR [#35](https://github.com/MohamedXIV/OutsiderDriver/pull/35) integration facts:** The Unity agent reported **191/191 EditMode and 2/2 PlayMode** after authoring four fixed bays, three turn curves and one visible signalized junction in `OutsiderCity`. The owner independently reports fares complete, cars follow and restart, bay docking works, and signals run. However the owner sees **remaining congestion and sideways/late-facing turns**. Do not call this owner-accepted traffic or merge simply because automated suites passed. The 15-minute profiler/traffic-soak requirement and actual graphical orientation still need evidence.

### Immediate work — protect and improve the real play loop

| Priority | Issue | Outcome |
| --- | --- | --- |
| **Now, quality gate** | [#42 — Congestion and sideways-turn diagnosis](https://github.com/MohamedXIV/OutsiderDriver/issues/42) + [#29 — Turn/facing](https://github.com/MohamedXIV/OutsiderDriver/issues/29) | Diagnose real jam causes; align sprite facing with actual movement tangent, fix any genuine path cuts, normal-speed Game View proof; don't hide with teleportation |
| **Now, stop policy** | [#27 — Flexible safe roadside taxi stopping](https://github.com/MohamedXIV/OutsiderDriver/issues/27) | Preserve PR #35 fixed-bay pull-in implementation, make **ordinary taxi curb stops contextual at eligible authored curb zones**; ensure passenger/address proximity and no stopping in active junctions |
| **P0 first-day work** | [#37 — Street-hail passenger discovery](https://github.com/MohamedXIV/OutsiderDriver/issues/37) + thin [#39 — Visible pedestrian slice](https://github.com/MohamedXIV/OutsiderDriver/issues/39) | Begin as old-fashioned street taxi: find real on-street NPCs, stop, board, serve systemic destinations; remote offer-card board not the default |
| **P0 player travel** | [#38 — Whole-city map without GPS](https://github.com/MohamedXIV/OutsiderDriver/issues/38) | Click place to drive there and explore/search for passengers; GPS assists but does not unlock the map |
| **Then owner gate** | [#22 — Human acceptance](https://github.com/MohamedXIV/OutsiderDriver/issues/22) + [#17 — First sandbox Epic](https://github.com/MohamedXIV/OutsiderDriver/issues/17) | At normal speeds: coherent turns/traffic, street-discovered repeat rides, legal roadside boarding and independently chosen map travel |

### Follow-on living city and services

| Issue | Later scope |
| --- | --- |
| [#39 — Walking city life](https://github.com/MohamedXIV/OutsiderDriver/issues/39) | Expand beyond minimal street-hail actors: ordinary walkers, activities and coherent sidewalk flows |
| [#23 — Contextual passenger Ink](https://github.com/MohamedXIV/OutsiderDriver/issues/23) / [#24 — World availability](https://github.com/MohamedXIV/OutsiderDriver/issues/24) | Optional conversations, recurring people, offscreen life; build on physical street encounters |
| [#40 — Taxi energy & stations](https://github.com/MohamedXIV/OutsiderDriver/issues/40) | Finite resource, authored service stops; exact fuel technology, pricing, recovery and UI unresolved |
| [#41 — Informal dispatch](https://github.com/MohamedXIV/OutsiderDriver/issues/41) | Later optional paid/earned access through semi-legal local life; lower expected fares and potentially risky riders; reuse current remote-offer UI backend; official app details undecided |
| [Traffic Epic #26](https://github.com/MohamedXIV/OutsiderDriver/issues/26), [#31–#34](https://github.com/MohamedXIV/OutsiderDriver/issues/31) | Buses with proper recessed bays, authored bridges, trams and monorail; not required before the starter experience |

**Execution:** keep #35 in draft; don't throw out working technical systems. Rebase/merge `main` documentation decisions into the implementation branch only in a deliberate conflict-aware operation, and do not claim the 2026-11-02 gameplay pivot is already coded. Avoid bundling an unlimited new simulation into one PR.

---

## Product north star (unchanged)

A **2D isometric**, handcrafted, beautiful alien retro-futurist **open-world-style taxi/life sandbox**. The player's jobs/ride opportunities emerge **procedurally from time, passenger availability and world context** (P-034). **Every street, road, district, building, bridge, bus lay-by and transit guideway is designer-authored**, not procedurally generated (P-036). Driving should be comfortable enough that the player enjoys looking around, talking or staying quiet during the ride. No scripted job sequence, compulsory campaign, physics-heavy GTA rewrite or giant per-NPC AI architecture.

## 1. Verified completed foundations — do not reopen simply to add polish

| Status | Issues / work | Evidence | What this does *not* certify |
| --- | --- | --- | --- |
| **CLOSED** | [#1–#16](https://github.com/MohamedXIV/OutsiderDriver/issues?q=is%3Aissue+is%3Aclosed) — package/core/time, RoadNetwork/right of way **#4**, pooled ambient TrafficDirector **#5**, ride/narrative/localization/home/save code | Closed historical foundational issues | Not polished live traffic or finished conversations/home experiences |
| **SHIPPED** | City Foundation 2.0 and in-place city polish | [`c5c74d4`](https://github.com/MohamedXIV/OutsiderDriver/commit/c5c74d4), [`55cf1bc`](https://github.com/MohamedXIV/OutsiderDriver/commit/55cf1bc); [City Scale Standards](CITY_SCALE_AND_STREET_STANDARDS.md) | Not final art; no claim of a full expansive city |
| **CLOSED** | [#18](https://github.com/MohamedXIV/OutsiderDriver/issues/18) — ground-metre ride and fare, [#19](https://github.com/MohamedXIV/OutsiderDriver/issues/19) — procedural eligible offers | [`b19e1f3`](https://github.com/MohamedXIV/OutsiderDriver/commit/b19e1f3), [`3b1e07b`](https://github.com/MohamedXIV/OutsiderDriver/commit/3b1e07b), [`83eafad`](https://github.com/MohamedXIV/OutsiderDriver/commit/83eafad) | Not rich NPC routines; small placeholder content pool |
| **CLOSED** | [#20](https://github.com/MohamedXIV/OutsiderDriver/issues/20) — UI Toolkit taxi HUD/stable offer board, [#21](https://github.com/MohamedXIV/OutsiderDriver/issues/21) — real ride orchestration | [`e6baccd`](https://github.com/MohamedXIV/OutsiderDriver/commit/e6baccd) | HUD is functional but not final styling, physical boarding avatar or polished stops |
| **SHIPPED BUG FIXES** | Equal-arrival intersection gridlock, stuck reservation behind waiting taxi, dead-end ambient destinations, payment tip breakdown | [`3029c2d`](https://github.com/MohamedXIV/OutsiderDriver/commit/3029c2d), [`6d67cb6`](https://github.com/MohamedXIV/OutsiderDriver/commit/6d67cb6), [`e11f5d3`](https://github.com/MohamedXIV/OutsiderDriver/commit/e11f5d3) | Not sophisticated following, legal stop placement, signal phases or smooth turns |
| **AUTOMATION IMPLEMENTED, ACCEPTANCE OPEN** | [#22](https://github.com/MohamedXIV/OutsiderDriver/issues/22) — production two-ride PlayMode + Game View evidence | [`27ecf2e`](https://github.com/MohamedXIV/OutsiderDriver/commit/27ecf2e); agent reported **89/89 EditMode, 2/2 PlayMode**, 12.2 min soak | Not owner gameplay acceptance; tests used simulated UI events and accelerated taxi; new curb/following defects still matter |
| **CLOSED DESIGN DECISION** | [#25](https://github.com/MohamedXIV/OutsiderDriver/issues/25) — wholly handcrafted city (P-036) | Design/documentation, not generated road code | Not a procedural/hybrid city generator project |

**Important distinction:** #4 and #5 were the **original traffic foundation**. They stay closed; Epic #26 now tracks required *maturity and transit features*, rather than reopening or pretending the previous issues fully delivered them.

## 2. NOW — P0 owner-acceptable taxi shift: fix player-facing traffic defects

The **repeatable procedural ride loop is implemented**. [#17](https://github.com/MohamedXIV/OutsiderDriver/issues/17) and [#22](https://github.com/MohamedXIV/OutsiderDriver/issues/22) remain **OPEN**, because the owner has identified clearly visible driving/stop shortcomings despite automated success.

| Priority | Issue | Concrete deliverable | Why now |
| --- | --- | --- | --- |
| **P0 / first** | [#27 — Safe curbside pickup/dropoff](https://github.com/MohamedXIV/OutsiderDriver/issues/27) | Designer-authored physically plausible stop poses, legal approaches, ride demand tied to stops rather than intersections, no obstructive boarding/delivery | Taxi currently can stop in the *middle of a junction*, a visible game-breaking behavior |
| **P0 / second (parallel if safe)** | [#28 — Car following + progressive braking](https://github.com/MohamedXIV/OutsiderDriver/issues/28) | Lane-aware lead car recognition, smooth deceleration/headway, full stop/restart, no collisions or jams | Existing sensors are mostly binary yielding; cars do not behave convincingly around slower traffic |
| **P0 / gate** | [#22 — Owner acceptance](https://github.com/MohamedXIV/OutsiderDriver/issues/22) | Re-run real `OutsiderCity` two-ride UI test, normal-speed taxi waiting at **safe curb**, see traffic pass/recover; owner's own Game View feedback and no blocking defect | Automated two-ride acceptance alone is *already* done; do not turn tests into owner sign-off |
| **P0 / then** | [#17 — Close original taxi Epic](https://github.com/MohamedXIV/OutsiderDriver/issues/17) | After #27/#28 and #22 accepted, close as **first playable taxi shift**, not final traffic or all systems | Prevent endless P0 scope creep: signals, buses, tram and monorail are **not** blockers |

**Definition of P0:** A player can drive two consecutive procedural jobs, interact Accept/Decline/Board/Collect, stop at legal curb positions without blocking intersections, watch ambient cars actually brake/follow/queue, and receive correct fares. Existing EditMode + genuine PlayMode regression tests and a human normal-speed check must agree. No need for complete character dialogue, buses, final visual art or city expansion before this gate.

## 3. NEXT — Traffic 2.0 (after P0, tracked under Epic #26)

| Order | Issue | Outcome |
| --- | --- | --- |
| 1 | [#29 — Smooth, speed-aware turns](https://github.com/MohamedXIV/OutsiderDriver/issues/29) | Physically plausible-looking lane trajectories and smooth sprites/orientation using designer-authored junction geometry, without a new road generator |
| 2 | [#30 — Traffic signals](https://github.com/MohamedXIV/OutsiderDriver/issues/30) | First working, visible selectively signalized hand-authored intersection with stop lines and safe phases, integrated with following and yield reservations |

These improve the core taxi experience, **not** a full traffic microsimulation.

## 4. LATER — Handcrafted public transportation and infrastructure (Epic #26)

| Proposed order | Issue | Minimal initial integrated experience |
| --- | --- | --- |
| 1 | [#31 — Buses and recessed bus bays](https://github.com/MohamedXIV/OutsiderDriver/issues/31) | Authored bus line, stop, physically inset lay-by preserving walking width, dwell and safe merge; ambient-only initially |
| 2 | [#32 — Bridges/overpasses/ramps](https://github.com/MohamedXIV/OutsiderDriver/issues/32) | Real two-level hand-built crossing, correct 2D isometric occlusion/elevation-aware graph and actual cars on both levels |
| 3 | [#33 — Trams](https://github.com/MohamedXIV/OutsiderDriver/issues/33) | Track-bound tram, small authored route/stations and safe conflict control at a road crossing |
| 4 | [#34 — Monorail](https://github.com/MohamedXIV/OutsiderDriver/issues/34) | Elevated authored guideway, stations, simple atmospheric service and visual crossing over the city |

These are intended city features, **not yet implemented**. Complete one believable localized example before introducing a network. Actual player rideability/tickets remain open future decisions. All infrastructure must be hand-authored (P-036).

## 5. Passenger/narrative/systemic life — preserved, not cancelled

| Status | Issue | Sequencing |
| --- | --- | --- |
| **OPEN, deferred until P0 comfort/stop blockers** | [#23 — Contextual Ink passenger conversations, optional silence and returning acquaintances](https://github.com/MohamedXIV/OutsiderDriver/issues/23) | Resume after #22/#17 P0 acceptance; can progress **before** full buses/tram/monorail. Keep work procedural, content authored and contextual. |
| **OPEN, later** | [#24 — Modest real resident availability/offscreen continuity](https://github.com/MohamedXIV/OutsiderDriver/issues/24) | After #23 or concrete repeated-play demand needs; no universal population AI. |

**Planning principle:** Later bus/tram/monorail work need **not** block the narrative/social game indefinitely. Traffic 2.0 and passenger dialogue can be independently scoped after the two current P0 defects.

## 6. Implementation and issue hygiene

- Always inspect actual remote `main`, open PRs/issues and scene before changing code; document concrete evidence/commits in each issue. Do not claim closed work was re-implemented.
- **Do not close #22/#17** from automated success alone; genuine owner playtest still controls the final gate. Close #27/#28 only on actual safe Game View behavior and regression tests.
- The hand-designed physical city and core data IDs are canonical. `RoadNode` provides navigation/topology, not automatically a legal place to stand still. Do not create a procedural roads/lot/building or transit generator in the name of convenience.
- Respect P-024: inexpensive kinematic, data-driven C# systems; no Rigidbody-based car physics, pathfinding rewrite, one monolithic manager, speculative city simulation or wholesale DOTS.
- For future issues, prove playable value, ordinary-speed visual inspection and performance where needed; tests, evidence and owner art/comfort approval are **distinct**.
- **Do not roll every feature into one DeepSeek/Gemini batch.** Start with #27, then #28 (or parallel if integration boundaries permit), then validate #22 and accept P0.

**Immediate implementation priorities (2026-11-02 override):** [#42 remaining jams/turn feel](https://github.com/MohamedXIV/OutsiderDriver/issues/42), then [#27 flexible curb policy](https://github.com/MohamedXIV/OutsiderDriver/issues/27) and [#37 actual street-hails](https://github.com/MohamedXIV/OutsiderDriver/issues/37) with [#38 map](https://github.com/MohamedXIV/OutsiderDriver/issues/38); see latest-owner section above. Prior historical phase plans are snapshots, not automatic approval to force dedicated taxi bays or offer-board-first work.
