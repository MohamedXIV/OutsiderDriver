# Systemic Open-World Sandbox & Procedural Taxi Demand

**Status:** Owner direction, 2026-10-28. **APPROVED:** a freeform, open-world-style taxi/life sandbox with **procedural/systemic work and ride opportunities, not a scripted or static playlist**. **NOT a claim of implementation.** **The city map, street network and building layout are fully hand-authored (P-036).**
**Precedence:** supersedes earlier blanket "no procedural generation" language **for jobs, ride requests and everyday systemic opportunities** (Decision P-005 and related older passages). Read alongside [GAME_DESIGN](GAME_DESIGN.md), [Decisions](DECISIONS_AND_OPEN_QUESTIONS.md), [World Content Contract](WORLD_CONTENT_DATA_CONTRACT.md), and [Roadmap](ROADMAP.md).

## 1. Game identity: an independent city, not a ride selection campaign

- Player: a human illegally resident in a lively alien city, working as a taxi driver, living and making choices in an ongoing sandbox. There is **no prescribed main story, quest chain, fixed ride order, or reset after a completed shift**.
- **Open-world experience:** player can choose where to go and which available journeys to accept along **actual connected streets**, rather than moving through isolated scripted mission levels. The current production area may be small and incrementally expanded; full-city pedestrian exploration is **not implied** by open-world driving. Semi-autonomous taxi driving remains the default.
- **Systemic/procedural demand is a core pillar:** who needs a ride, where/when, whether they are available, how the request reaches the driver and what trip is possible arise from current world circumstances and authored rules, rather than a designer-maintained fixed order of `home -> market -> home` tasks.
- Different runs and passing time should make **materially different, coherent opportunities**, while retaining continuity of residents and their social context.
- **A ride is ordinary work, not a quest.** Accepting/declining alters availability and the world state where appropriate. No mandatory narrative or reward ladder.

## 2. Generated opportunities over grounded, authored data

**Input sources (reuse existing contracts before creating more):**
- Time of day and continuous simulation clock / coarse ticks.
- Existing road graph, reachable pickup/drop-off nodes, stops and `CityLocationAnchor` locations; disallow physically impossible routes.
- District and location category, traffic/availability and local context where data exists.
- Passenger identity, routine, relevant social state and languages when represented. An ordinary transient resident may be simpler than a persistent significant resident. Do not fabricate identities or canonical alien names from random syllables.
- Driver access: legal/social restrictions, informal versus official dispatch (availability of these channels and detailed mechanics remain separate design questions).

**Output:** an offer with an eligible pickup, eligible destination, selected passenger (or justified generated/transient identity), timing/expiration where relevant, route estimate and grounded fare. Keep one coherent source of truth. Content is **data-driven**, but the **opportunity instance is generated at runtime**.

**Content/logic boundary:** Writers author reusable character definitions, dialogues, district traits, circumstances, language constraints and templates; the simulation chooses and composes fitting opportunities. A bespoke authored encounter is allowed as rare content, **not** the entire taxi economy or a forced sequence.

## 3. First playable gameplay milestone: P0 acceptance

P0 must exercise the **systemic opportunity source** from the first playable implementation. A one-passenger, one-route **test fixture** may exist in unit tests, but cannot be the only production offer stream.

Minimum acceptable player experience:
1. Enter Play Mode in `OutsiderCity`, starting in taxi near home for convenience (initial start is staging, **not a permanent fixed job contract**).
2. World time and valid locations produce one or more eligible, reachable job opportunities through the simplest currently appropriate access channel. Accept / decline / ignore; new opportunities continue to arise without scene reload. No need for multiple job-market apps in P0.
3. Pickup and destination are selected from actual traversable world nodes/locations; multiple requests can differ between situations/clock windows/seeds without arbitrary teleportation.
4. Player chooses a job, semi-auto taxi travels to pickup, passenger boards, rides, can remain silent, arrives, is settled in **ground metres**, credits update, and player may take further work.
5. Declining does not instantly replay the same hardcoded job. The player is never forced to accept a special passenger or a scripted route.
6. Relevant EditMode + genuine PlayMode tests demonstrate the above, including changes in time/state leading to different eligible opportunities and repeated rides without restarting.

**Implementation boundary:** reuse `RideLifecycleService` (one active ride state machine), `CityWorldRegistry`, `RoadNetwork`, `TimeService`, `PassengerDefinitionSO`, state services, existing Ink/UI Toolkit. A small scene-scoped offer source/orchestrator is reasonable; **do not** create a new giant NPC/economy/simulation engine or hardcode a timeline of jobs. Fix the existing fare and pickup proximity computations still using raw Unity coordinates to use the approved ground-meter projection. Reuse passenger definitions as **content pool**, not a schedule of scripted rides.

## 4. Follow-on systemic depth — staged, not a P0 prerequisite

- **P1:** reusable contextual Ink dialogue and optional silence for relevant passengers, recurring IDs and simple reactions; not one long storyline per fare.
- **P2:** coherent resident availability/routines, offscreen travel/demand and local consequences, so the player is not the sole origin of city activity. Keep cost proportional to actual gameplay.
- **Later:** additional road-connected authored city areas, access networks, informal work, language, social risk and housing; gate on playtesting the core taxi loop.

## 5. Explicit open questions and non-inferences

- **Map/world geometry is NOT procedural (P-036).** The **entire** city, its planned geography, roads/lanes and buildings/placements are human-authored from the outset using native Unity 2D isometric assets. Neither a runtime seed nor a design-time generator may invent the street grid or buildings. Procedurally generated *work opportunities* consume this fixed spatial source of truth; they do not generate geometry. O-030 is **resolved** by P-036.
- Exact ride demand probabilities, expiry, resident lifecycle detail, player discovery channels and anti-repetition heuristics are **implementation tuning**, not canon. Start with clear minimal rules that produce variety and coherent eligibility.
- **Open world does not mean:** procedural maps/roads/buildings, mandatory manual driving, universal indoor access, full-city foot traversal immediately, a GTA-scale physics city, an all-NPC AI simulation or a main quest.
- Never confuse **authored content** with **scripted gameplay**. The city, locations and reusable dialogue assets can be hand-authored while the **gameplay situations are procedural**.

## 6. Scope/quality guardrails

- No offline LLM requirement, no random ungrounded dialog/lore, no guaranteed same first three ride offers, no scripted daily job ladder, no passenger instantiated at an unreachable road node, no repeatedly recycled identical job on Decline.
- Persist meaningful resident/driver state when existing save architecture supports it; do not require backward save migrations or architect a new universal persistence schema solely for the first shift.
- Keep the current approved 2D-isometric, meter-scaled visual city. The procedural ride source must use the real physical road graph and fare distances.
- **Validation distinguishes** authored test fixture, verified dynamically generated production offers, PlayMode mechanics, and human enjoyment. Passing one static test ride does not satisfy the systemic milestone.

**Core invariant:** The city continues regardless of the driver; taxis are work in that city, not missions built for the driver.
