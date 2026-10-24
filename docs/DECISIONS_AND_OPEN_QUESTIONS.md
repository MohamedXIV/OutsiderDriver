# Decision Register, Supersession Rules & Open Questions

**Owner decisions through:** 2026-10-24  
**Purpose:** Prevent developers and AI agents from filling design gaps with guesses, importing obsolete obligations, or mistaking a historical prototype for canonical gameplay.

This file is authoritative alongside [GAME_DESIGN.md](GAME_DESIGN.md). It records **what the owner actually settled**, **what prior project documents contain**, and **what is still open**. It is not a speculative feature roadmap.

## Decision labels — mandatory in future docs and PRs

| Label | Meaning | Agent permission |
| --- | --- | --- |
| **APPROVED** | Explicit current owner decision or a prior decision clearly retained by the newest owner direction | May design/implement within its actual scope; no expansion by inference |
| **HISTORICALLY AGREED / RETAINED IDEA** | Product detail strongly established in prior discussions and not contradicted; must be reconciled with the current simpler scope | May document as intent, not silently turn implementation detail into requirement |
| **REFERENCE / EXAMPLE** | Illustrative situation, visual mood board, historical technique, or previous implementation | Not authorization to implement as a feature |
| **PROPOSED** | Plausible approach suggested by an assistant or historical document without fresh final owner approval | Do not mark as requirement; seek decision when relevant |
| **OPEN** | Missing or ambiguous product choice | Mark TBD; no fabricated answer |
| **REJECTED / OUT OF SCOPE** | Explicitly prohibited or clearly superseded | Do not implement without a new explicit owner reversal |
| **IMPLEMENTED** | Verified feature exists in *this* Unity repository | Must cite source files plus working runtime evidence; prior-repo evidence is not implementation here |

**Never use 'approved' as shorthand for 'was once in a Godot issue' or 'the AI suggested it.'** Approval, implementation, and visual/user acceptance are different statuses.

## Precedence and source of truth

1. **Most recent explicit owner decision** wins on that topic, including this conversation's October 23 cleanup.
2. Current [GAME_DESIGN.md](GAME_DESIGN.md), this register, [VISUAL_DIRECTION.md](VISUAL_DIRECTION.md), [ENGINEERING_PRINCIPLES.md](ENGINEERING_PRINCIPLES.md) and [SYSTEMS_ARCHITECTURE.md](SYSTEMS_ARCHITECTURE.md) record current interpretation.
3. Live Unity source only says **what has been implemented**, never automatically **what must be kept**.
4. Prior repositories provide product-design history and cautionary lessons, not automatic Unity requirements.
5. Older design documents cannot overrule a newer owner correction; old test assertions do not veto changed game design.
6. When two current documents genuinely conflict, do not silently choose one and invent rationale. Record the contradiction and ask the owner before implementation.

### Current repository reality as reviewed October 23, 2026

- Repository: [MohamedXIV/OutsiderDriver](https://github.com/MohamedXIV/OutsiderDriver).
- Unity project editor version: **6000.6.0f1** in ProjectSettings/ProjectVersion.txt.
- Clean project structure under `Assets/_Project/` with dedicated `OutsiderDriver.Runtime.asmdef` and `OutsiderDriver.Editor.asmdef`.
- Initial 2D isometric navigation & vehicle movement verified in `Assets/_Project/Scenes/TaxiSandbox.unity`.
- Documentation completion is not gameplay implementation. Gameplay features exist only as verified in code.

## A. Confirmed current owner decisions (APPROVED)

| ID | Decision | Interpretation and strict limit |
| --- | --- | --- |
| P-001 | Clean restart in **Unity 6**, C#, with a real **2D isometric** game | No engine-port project; no 3D-first Babylon/Godot architecture transplanted |
| P-002 | **Keep the game simple**: drive passengers, talk with them, get to know them if desired, live freely | Systems must serve these experiences, not become the game themselves |
| P-003 | **Freeform sandbox** | No mandatory campaign or mission ladder; not a scripted ride playlist |
| P-004 | **No combat** | No fighting, combat stats, enemies or attack gameplay |
| P-005 | **No procedural generation** as game model | Authored city and narrative content; systemic requests/AI reactions are not permission to generate world maps or arbitrary stories |
| P-006 | **No quests / missions** as progression framework | Taxi fares are regular work, not objectives in a role-playing quest chain |
| P-007 | A **living city** matters | Not permission to build a whole city-scale life simulator or every-citizen economy |
| P-008 | The human driver lives **illegally among non-human societies** | Social premise remains; exact enforcement/disguise gameplay is still open |
| P-009 | **Can get out and walk**, initially in **the protagonist's neighborhood and home** | Do not infer city-wide free roaming or dozens of interior scenes |
| P-010 | **Beautiful, cheerful, colorful alien retro-futurism** | Distinct lived-in architecture, bright palettes, attractive people; no generic grim cyberpunk |
| P-011 | **Character representation not finalized** | Small/simple possibly pixel-art-style in-world people, with expressive portraits, are acceptable directions rather than a locked production technique |
| P-012 | **No migration of old broken code or structure** | Re-author the production approach from clean Unity foundations; previous code may only inform warnings and historical rationale |
| P-013 | **Do not build a disposable technical slice and then let agents extend it as permanent architecture** | Small increments can be production-quality; experiments must never quietly become the core game |
| P-014 | **Unity-native authoring first** | Favor actual static scene objects, isometric tilemaps and normal engine workflows over custom polygon world rendering |
| P-015 | Architecture should be coherent but **not overengineered** | Avoid monolithic god classes and the opposite extreme of countless meaningless ten-line abstraction files |
| P-016 | Real user experience and prolonged performance matter | Old Godot playtest suffered progressive FPS drops and vehicle/pedestrian stalls; clean Unity game must be measured, not assumed fast |
| P-017 | **Continuous real-time simulation clock** | Time advances continuously in real time (like GTA / The Sims), firing discrete coarse ticks (e.g. 1 in-game minute = ~1s) to drive schedules and system updates without per-frame overhead (supersedes O-011) |
| P-018 | **VContainer dependency injection & LifetimeScopes** | Clean, fast DI container (`GameLifetimeScope` for root services, `SceneLifetimeScope` for scene actors); no monolithic God GameManager |
| P-019 | **Modern Unity 6 tech stack: UI Toolkit (MVP), Cinemachine 3, Input System, and early Localization** | Modern event-driven UI Toolkit decoupled via Presenters; Cinemachine 3 camera blending between taxi and on-foot modes; early Arabic RTL localization support (supersedes O-020) |
| P-020 | **Ink dialogue engine integration** | Ink (`ink-unity-integration`) is the canonical narrative engine for branching contextual dialogue, bound to game state via external functions and variable listeners (supersedes O-017 and H-015) |
| P-021 | **Ambient traffic via GTA-style camera bubble & pooling** | Ambient ground cars spawn/despawn in a radius around the player, obey road nodes, and yield at intersections with right-of-way rules (supersedes O-010) |
| P-022 | **Character portraits via Layered 2D & Native 2D Animation** | Expressive portraits built from modular layered sprites and Unity's built-in 2D Animation bones; Live2D and Inochi2D rejected (supersedes O-002) |
| P-023 | **Full DOTS/ECS architecture rejected** | Unnecessary architectural overhead for a budgeted visible entity scale (~20-50 actors); standard C# + object pooling used; Jobs/Burst reserved solely for surgical math optimizations if measured |
| P-024 | **Pure Kinematic Vehicle Navigation (Physics Engine & Rigidbodies Banned for Vehicles)** | Vehicles use deterministic kinematic movement (acceleration, deceleration, yielding, and route following). Rigidbody2D, wheel colliders, and slip/drift physics simulations are explicitly banned to prevent physics deadlocks, collision clipping, and progressive frame rate drops |
| P-025 | **Native Unity 2D Isometric Pipeline & Depth Sorting** | Use Unity's native `Grid` + `Tilemap` (`Isometric Z as Y`) for roads, sidewalks, and environment footprints. Native Project Transparency Sorting (`CustomAxis` {x:0, y:1, z:-0.26}) is used for automatic sprite depth sorting without custom z-indexing scripts. Cinemachine 3 Virtual Cameras replace custom camera scripts for smooth isometric tracking |
| P-026 | **Refined VehicleController Architecture & Sandbox Harness Isolation** | `VehicleController` is confirmed as the permanent production kinematic vehicle actor. `TaxiTestDriver` is explicitly scoped as a temporary sandbox test harness to be superseded by the production `RideLifecycleService` and on-foot player controls |

| P-027 | **True 2D ISOMETRIC, not a Unity 3D world** (reaffirmed 2026-10-24) | All world/art production must remain genuinely Unity 2D isometric, even when illustrated objects have visual depth; 3D-looking concept art is not a rendering/asset pipeline decision |
| P-028 | **Data-driven content/world wherever practical** (owner, 2026-10-24) | Use stable opaque IDs (`district_01`, etc.) independent of editable names, candidate names, descriptions, histories, locations/coordinates, bounds, cultural and visual metadata, referenced authored streets/places; add fields only when used, prefer existing Unity SO + scene workflows. See [World & Content Data Contract](WORLD_CONTENT_DATA_CONTRACT.md) |

## B. Earlier product decisions retained as direction (not an implementation mandate)

These come from the prior design archive. Unless explicitly overridden, they describe the **intended flavor and likely player-facing behaviors**, not a task list to rebuild every corresponding old subsystem.

| ID | Historical design intent | How to treat it now |
| --- | --- | --- |
| H-001 | Taxi journeys are **semi-autonomous**, with driver-chosen destinations/routes rather than mandatory manual steering | Retain; route-control UI and handling details are OPEN |
| H-002 | The city has real roads, turns, bridges, ramps and elevation separation | Retain as authored visual/topological goal; no custom renderer authorization |
| H-003 | Genuine city distances create comfortable ride times for dialogue/scenery | Retain; don't fix pacing with arbitrary slowdown |
| H-004 | Passengers have lives, needs and trip demand independent of prewritten mission episodes | Retain, but do not require a huge population engine |
| H-005 | Characters may recognize previous encounters and maintain selected memories and relationships | Retain, bounded by importance and actual gameplay |
| H-006 | Human cover, statements, witnesses and trust can matter over time | Retain as narrative/social context, not a mandatory policing framework |
| H-007 | Species, language, culture, community and allegiance are conceptually independent | Retain as worldbuilding rule, not five huge simulation modules |
| H-008 | A basic Common/Trade translator plus a GPS / additional-language-module / Radio opening bonus | Last ratified prior-Godot equipment design; retain as design precedent, but no exact balance or mandated immediate work |
| H-009 | Translation is per installed language, not a word-by-word XP grind | Retain as a simple, understandable design rule |
| H-010 | Alien local speech, strange radio/music and optional forbidden human signals | Retain as setting/content possibilities; forbidden frequencies are FUTURE/OPTIONAL |
| H-011 | Legal and informal work access differ; ordinary costs/earnings matter | Retain as lightweight daily-life texture, not a hardcore business sim |
| H-012 | Home/garage and a few personal places support ordinary life | Retain, subject to the newest **home + own neighborhood** walking scope |
| H-013 | Optional bonds beyond the taxi: friendships, appointments, occasional romance | Retain as voluntary content; do not convert to a dating-sim system |
| H-014 | World time ordinarily moves during travel and dialogue, with graceful arrival handling | Retain as player comfort objective; continuous real-time clock ratified in P-017 |
| H-015 | Authored contextual narrative with a consistent source of game truth | Ratified as Ink narrative engine in P-020 |
| H-016 | Accessibility, legible HUD, Arabic/RTL-aware localization and skippable effects | Ratified early in P-019 via Unity Localization package |
| H-017 | The old retro-futurism style guide favored tactile, repairable technology, rounded alien design, colored road surfaces and ground vehicles | Retain visual vocabulary but not the original 3D mesh/shader pipeline |

## C. Explicitly rejected or superseded assumptions

| ID | Rejected assumption | Why it must not return by default |
| --- | --- | --- |
| X-001 | 'The game is a succession of fixed authored clinic passengers and prescribed rides' | Original technical fixture, not the sandbox product |
| X-002 | 'The map is a five-district debug diagram with ten-minute abstract edges' | Old prototype data, not authored city scale |
| X-003 | 'All game content is generated procedurally or by an LLM' | Owner said no procedural generation; authored language and character quality matter |
| X-004 | 'The game requires a main quest, objective chain, or combat to be fun' | Explicit owner rejection |
| X-005 | 'No on-foot exploration anywhere' | Superseded by latest permission to walk in home and initial neighborhood |
| X-006 | 'Walking means an entire GTA-scale open world' | Latest walk scope is intentionally local |
| X-007 | 'We must port Godot world state, old event ledgers, versioned saves or Babylon's layered abstractions' | Owner explicitly wants no code/architecture transfer |
| X-008 | 'Proof-of-concept polygon renderer/temporary scene is production engine architecture' | Previous mistake; no repeat |
| X-009 | 'Each citizen needs full physics, deep cognition, persistent economy and pathfinding while off-screen' | Violates simplicity and performance intent |
| X-010 | 'All inhabitants are defined by one species-language-culture-faction bundle' | Conflicts with established alien society model |
| X-011 | 'A passenger likes the player therefore knows they are human / trusts them completely' | Relationships, belief and identity are separate |
| X-012 | 'The player sees English when translation happens, regardless of chosen UI language' | Localization/translated speech are not the same as English-only design |
| X-013 | 'GPS / Translator / Radio are three exclusive devices, none standard' | Superseded historically by free Basic Common translator + bonus choice |
| X-014 | 'We should implement every historical issue or automated test before building the new game' | Old repo work is not an import queue |
| X-015 | 'Green automated checks prove the game feels good and runs well on Windows' | Old real user playtest disproved that assumption |
| X-016 | 'A beautiful city must be gloomy/neon/rainy or depend on high-end 3D graphics' | Contradicts current bright alien art direction and 2D decision |
| X-017 | 'A full DOTS/ECS rewrite is required for performance' | Premature overengineering for ~20-50 visible entities; creates severe friction with Ink, UI Toolkit, and 2D animation |
| X-018 | 'Use Live2D Cubism for character portraits' | Heavy restrictions on free tier, expensive recurring commercial subscription, complex proprietary SDK |
| X-019 | 'Use Inochi2D for character portraits' | Fragile, experimental, unmaintained community Unity runtime; unnecessary risk |

## D. Open product questions — leave as TBD; don't choose for the owner

| ID | Topic | Known boundary; what is NOT known |
| --- | --- | --- |
| O-001 | In-game art style for small characters | Could be pixel art, stylized sprites, another simple treatment; **not finalized** |
| O-002 | Portrait animation/runtime | **SETTLED by P-022**: Layered 2D sprites with Unity 2D Animation bones |
| O-003 | Camera geometry and specific zoom | 2D isometric, close and cinematic; Cinemachine 3 approved in P-019; exact zoom/offsets TBD |
| O-004 | City map extent and precise data | Authored connected districts and coherent journeys; city size, district count/placement and geography TBD. Four-district images/names are unapproved concept proposals; stable `district_01`-style IDs and metadata independence ARE approved |
| O-005 | On-foot boundary beyond starter area | Home and own neighborhood approved initially; other districts and interiors not promised |
| O-006 | Home/garage layout and interactions | True walkable home and previously intended garage; content and scene scale TBD |
| O-007 | Taxi manual controls | Semi-auto intent; whether any direct steering/minor intervention exists TBD |
| O-008 | Passenger acquisition UX | Street hails, app-like offers, underground calls are precedents; exact UI and mix TBD |
| O-009 | NPC numbers, significance and retention | Some ambient/persistent/significant distinction sensible; exact numbers and thresholds TBD |
| O-010 | NPC AI implementation / Traffic | **SETTLED by P-021**: GTA-style camera bubble & pooling with intersection right-of-way yielding |
| O-011 | Time progression/pausing | **SETTLED by P-017**: Continuous real-time clock with discrete coarse simulation ticks |
| O-012 | Human disguise, documents and exposure | Human/illegal premise approved; actual reveal/risk systems TBD |
| O-013 | Economy specifics | Prices, maintenance, failure state, living cost balance, optional cash difficulty TBD |
| O-014 | Social representation | Exact traits/stat model, memories, friendship/romance triggers, authored significant cast TBD |
| O-015 | Language naming and artwork | Historical Common, Local, Cant; final names, cultural naming, scripts and audio TBD |
| O-016 | Translator progression | Previous binary modules and starter rules guide design; final merchandise/catalog/prices TBD |
| O-017 | Ink adoption / dialogue integration | **SETTLED by P-020**: Ink canonical narrative engine via `ink-unity-integration` |
| O-018 | Radio music/production | Alien stations desired; soundtrack format, licenses and content plan TBD |
| O-019 | Visual asset pipeline | Unity 2D target, artistic references; actual sprite/tile resolutions, lighting, animation and sorting approach TBD |
| O-020 | Final HUD/navigation/input | **SETTLED by P-019**: UI Toolkit with MVP Presenters + Unity Input System |
| O-021 | Platforms and release targets | Unity template alone does not imply shipped Windows/Web/Linux/mobile; targets and performance thresholds TBD |
| O-022 | Weather/day-night scope | Atmosphere is wanted; continuous clock (P-017) drives lighting transitions; dynamic weather TBD |
| O-023 | Legal social consequences | Meaningful setting, but surveillance systems, arrests, pursuit, punishments and endings TBD |
| O-024 | Non-quest special situations | No quests; events can arise from normal life, but no event tree or campaign commitment |
| O-025 | Asset reference inclusion | User images are present in the conversation, not committed to this repo; agents cannot assume they possess source images |

### How to settle an open question

A future change must state exactly:
1. Which **O-xxx** decision is being made and the scope of approval.
2. What was approved by the owner, versus what was proposed.
3. Which previously written passages/assumptions it supersedes.
4. Whether it is **design only**, implemented, technically verified, or actually playtested.
5. Why its implementation complexity is proportionate to our small-game fantasy.

Never change an OPEN item to APPROVED because an agent found a helpful library or a screenshot makes the answer seem obvious.

## E. Historical decisions: changes across the three project eras

| Era | What it contributed | What it DOES NOT impose on Unity |
| --- | --- | --- |
| Babylon.js [outsider-driver](https://github.com/MohamedXIV/outsider-driver) | Identity/social-stealth fantasy, passenger intimacy, language, radio, daily life and detailed alien retro-futurist visuals | 3D cockpit/interiors, Babylon scene setup, TypeScript architecture, Inochi bridge, exhaustive save migrations, fixed authored ride schedule |
| Godot [outsider-driver-2d](https://github.com/MohamedXIV/outsider-driver-2d) | Systemic sandbox, 2D isometric framing, persistent social encounters, autonomous neighborhood feel, route choices, multilingual residents, playability feedback | GDScript classes, procedural drawing, giant simulation ledger, old movement/traffic implementation, proprietary adapters, schematic test fixtures |
| Unity [OutsiderDriver](https://github.com/MohamedXIV/OutsiderDriver) | New clean, authored 2D isometric production project; voluntary taxi/social life; bounded on-foot neighborhood; deliberately simpler architecture | Nothing is playable merely because it was playable in either earlier repo |

## F. Concrete prior sources (for verifying provenance)

- [Original game high concept](https://github.com/MohamedXIV/outsider-driver/blob/main/docs/GAME_DESIGN.md)
- [Original scope](https://github.com/MohamedXIV/outsider-driver/blob/main/docs/SCOPE.md)
- [Original visual bible](https://github.com/MohamedXIV/outsider-driver/blob/main/docs/STYLE_BIBLE.md)
- [Godot sandbox DNA](https://github.com/MohamedXIV/outsider-driver-2d/blob/main/docs/SANDBOX_GAME_DNA.md)
- [Godot living city agreement vs proposals](https://github.com/MohamedXIV/outsider-driver-2d/blob/main/docs/LIVING_CITY_DESIGN_CONCLUSIONS.md)
- [Godot supersession logic](https://github.com/MohamedXIV/outsider-driver-2d/blob/main/docs/SUPERSESSION_REGISTER.md)
- [Last confirmed three-language starter design](https://github.com/MohamedXIV/outsider-driver-2d/blob/main/docs/SANDBOX_LANGUAGES_P0.md)
- [Godot user playability feedback](https://github.com/MohamedXIV/outsider-driver-2d/blob/main/docs/PLAYABILITY_FEEDBACK.md)
- [Godot Windows FPS and deadlock bug report](https://github.com/MohamedXIV/outsider-driver-2d/issues/86)

**Current October 22–23 conversation explicitly supersedes earlier project scope wherever it says: simple life game, no combat/quests/procedural generation, clean Unity restart, real home/neighborhood walking and warm colorful visuals.**
