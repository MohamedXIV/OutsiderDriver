# Outsider Driver — Active Roadmap & GitHub Execution Map

**Updated:** 2026-10-28. **Status:** gameplay planning and issue-tracking **created**, not evidence that upcoming work has been implemented.
**Source of authority:** [Game Design](GAME_DESIGN.md), [Decision Register](DECISIONS_AND_OPEN_QUESTIONS.md) (**P-034 / P-035**), [Systemic Open-World & Procedural Ride Demand](SYSTEMIC_OPEN_WORLD_AND_RIDE_DEMAND.md).
**Active parent Epic:** [#17 — Playable Systemic Taxi Sandbox (P0)](https://github.com/MohamedXIV/OutsiderDriver/issues/17).

## Product north star

Build a **2D isometric open-world-style alien taxi/life sandbox**. An illegal human drives passengers, navigates connected city streets, learns local society and chooses whether to interact or remain quiet. **Ordinary taxi jobs arise procedurally from time, reachable places, passenger availability and relevant world state**. No static scripted mission queue, forced passenger order, obligatory narrative campaign or end-of-shift restart.

- **Authored content is not scripted gameplay.** World locations, roads, characters, rules and dialogue pieces may be designer-authored; **individual work opportunities are assembled by the runtime**. A stable named resident may appear again for grounded reasons.
- **Open world is an experience promise:** free non-linear taxi travel and ordinary work in a connected city. That does not force all-city pedestrian traversal immediately, 3D, manual driving or a giant all-resident simulation.
- **City geometry is definitively 100% hand-authored (P-036)**: geography, road networks, street surfaces, building footprints and placement, across the whole game. Unity Tilemaps/Prefabs and reusable assets make human design flexible, but no algorithm generates the city or buildings. [#25](https://github.com/MohamedXIV/OutsiderDriver/issues/25) is the **resolved** decision record; only jobs, ride opportunities and permitted ambient world activity are systemic/procedural. Other future procedural features may be discussed separately.
- Scope stays grounded in actual game feel: repeated jobs, contextual people, time that moves, no forced main story.

## Completed production foundation — maintain, don't rebuild

| Area | Verified repository evidence | What it does not prove |
| --- | --- | --- |
| Core Unity 6 / VContainer / time / ride / UI/Ink code | Earlier closed issues [#1–#16](https://github.com/MohamedXIV/OutsiderDriver/issues?q=is%3Aissue+is%3Aclosed) | A human can play the complete dynamic work loop |
| Meter-based city + road hardening | [City Foundation commit `c5c74d4`](https://github.com/MohamedXIV/OutsiderDriver/commit/c5c74d4) | Final art or unlimited city scale |
| Polished city frontage, props and test evidence | [Polish commit `55cf1bc`](https://github.com/MohamedXIV/OutsiderDriver/commit/55cf1bc) and [City Scale Standards](CITY_SCALE_AND_STREET_STANDARDS.md) | Player acceptance of gameplay, dynamic passenger demand or final visual art |

The previous agent reports 62/62 EditMode and 1/1 PlayMode passing for the **road/traffic** systems. Treat those as **reported test results**, not proof of the future gameplay milestone. The remaining manual Tile Palette authoring check is non-blocking unless the next task touches road authoring.

## NOW — P0: Playable **systemic** taxi work (one production milestone)

**Epic #17**: [Playable Systemic Taxi Sandbox](https://github.com/MohamedXIV/OutsiderDriver/issues/17)

| Order | Issue | Purpose | Exit evidence / dependencies |
| --- | --- | --- | --- |
| 1 | [#18 P0-A — Metric fares and pickup](https://github.com/MohamedXIV/OutsiderDriver/issues/18) | Fix ride distance, settlement and pickup threshold using real ground metres | Verified distance/projection tests; unblocks downstream fare correctness |
| 2 | [#19 P0-B — Procedural demand](https://github.com/MohamedXIV/OutsiderDriver/issues/19) | Produce changing eligible ride requests from time, residents and reachable places | **Core player requirement:** multiple coherent possibilities, no fixed authored work queue, decline/ignore regeneration |
| 3 | [#20 P0-C — Dynamic offers UI](https://github.com/MohamedXIV/OutsiderDriver/issues/20) | UI Toolkit offer, accept/decline, current phase, boarding, arrival and income | Real Game View controls consume **dynamic** provider; can develop alongside #19 |
| 4 | [#21 P0-D — Production scene orchestration](https://github.com/MohamedXIV/OutsiderDriver/issues/21) | Wire selected offer through existing RideLifecycleService and the actual taxi/road graph | Reusable, repeatable interactive loop; depends on #18/#19 and integrates #20 |
| 5 | [#22 P0-E — Acceptance tests/playtest](https://github.com/MohamedXIV/OutsiderDriver/issues/22) | EditMode systemic variation and PlayMode 2+ consecutive actual rides, screenshots, no regression | P0 close gate; depends on #18–#21 |

**Implementation priority**: **#18 → #19**, then #20 / #21 as interfaces permit, followed by #22. Work in coherent runnable batches rather than creating a new giant architecture. A single authored test passenger/home-to-market pair is allowed *inside tests*, never as the production game's only work source.

### P0 acceptance contract — no shortcut

A player opens `OutsiderCity`, chooses among available *world-generated*, reachable taxi jobs, may accept/decline, drives to pickup, boards, travels along valid real roads, optionally remains silent, arrives, receives grounded fare, sees credits update, and continues receiving **new opportunities** without restarting. Tests show available jobs genuinely depend on changing time/world state (and valid reproducible seeds if used). **Do not close #17 on one hardcoded demo trip**, even if a PlayMode test passes.

No city/road/building generator (at any milestone), quest system, extra districts in P0, optional romance, final-art overhaul, advanced economy, new passenger AI, full neighborhood-on-foot expansion or broad save migration in P0.

## NEXT — after P0 is actually enjoyable

| Stage | Tracked work | Player-facing outcome | Gate |
| --- | --- | --- | --- |
| **P1 — People & conversation** | [#23](https://github.com/MohamedXIV/OutsiderDriver/issues/23) | Context-sensitive reusable Ink talk, optional silence, recurring passenger identity and first simple social memory | P0 real work repeatability; no scripted missions |
| **P2 — Living-city continuity** | [#24](https://github.com/MohamedXIV/OutsiderDriver/issues/24) | Availability/routines/offscreen coherence affecting actual requests as city clock moves | Demonstrable need after human playtesting, avoid all-NPC simulation |
| **CLOSED design gate — city geography** | [#25](https://github.com/MohamedXIV/OutsiderDriver/issues/25) | **Resolved P-036:** fully hand-designed city, road/street layout, buildings and placement; never procedural | No further decision required for the authored map; only new unrelated procedural concepts are open to future discussion |

## Triage and implementation rules

- Inspect live `main`, issues and PRs before selecting work; never assume this roadmap's implementation state is newer than GitHub. When a child issue completes, add evidence/commit/test links and only then close it.
- `AGENTS.md`, P-034/P-035, and the systemic contract override stale old text saying "no procedural content." The procedural job/ride demand **MUST** be implemented as gameplay, not just proposed.
- Use the current Unity / 2D isometric / meter-scale / lane-aware production city. Use real reachable nodes for work, real measured distances for fare and real time for demand. Keep the implementation testable without requiring the real GUI for all unit tests.
- Tests and explicit human Game View/playability evaluation are distinct gates. UI polish can follow usability evidence; do not reopen endless world-building before making the taxi loop playable.
- Other features beyond this roadmap belong in future scoped issues with real player-value justification. No implementation claim without source and runtime evidence.

**Roadmap objective:** First make **ordinary taxi work repeatedly playable and procedurally arising**, then make the city's people feel more alive. The city is not a scripted ride menu.
