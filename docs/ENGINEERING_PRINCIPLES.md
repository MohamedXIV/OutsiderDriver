# Engineering Principles — Clean Unity 2D Project

**Applies to:** Any contributor or AI agent implementing Outsider Driver in [this repository](https://github.com/MohamedXIV/OutsiderDriver).  
**Status:** Current owner-level architectural constraints, not a prebuilt architecture or authority to add systems.  
**Read with:** [Game Design](GAME_DESIGN.md), [Decision Register](DECISIONS_AND_OPEN_QUESTIONS.md), [Visual Direction](VISUAL_DIRECTION.md).

## Two mandatory invariants (owner-confirmed 2026-10-24)

- **ACTUAL 2D ISOMETRIC:** All city, character, taxi and neighborhood visual world-building must remain Unity **2D** isometric, even if artwork conveys volume. 3D dioramas in image references are not 3D runtime requirements. Exact 2D sort/camera/art specifications are still open.
- **DATA-DRIVEN BY DEFAULT WHEN PRACTICAL:** Represent district/location/street/resident identity with stable IDs (e.g. `district_01`), not temporary descriptive names. Place names, proposed alternate names, descriptions, cultural backstory, authored world positions/bounds, art and relevant relationships should be content metadata or scene-authored references rather than hardcoded in logic. Reuse existing ScriptableObjects and Unity-authored scenes, with one source of truth per property.
- **NO PROCEDURAL CITY REQUIREMENT:** Data-driven means **editable, deliberately authored** content, not automatic map/lore generation. Do not erect a universal schema/registry/authoring platform in the name of flexibility; expand the current simple data types only as content requires.
- **Legacy quarantine:** `TaxiSandboxBuilder.cs` and its unused Editor assembly were deleted (owner decision 2026-10-24). `TaxiSandbox.unity` is a legacy test fixture **only**, not a production template; do not extend its five-node placeholder graph. `TaxiTestDriver` is a separate temporary harness. Review actual usage before retiring shared runtime types.
- **PHYSICAL CITY SCALE (P-031/P-032/P-033):** Use **1 logical meter per ground-plan cell** across all authored roads, cars, sidewalks, lots and buildings. Baselines: 2m-wide, 4–5m-long taxi; 3m lane; 2m sidewalk on each side; 10m basic two-way street; 16m two-lanes-each-way arterial. Keep full-width 2D isometric tile surfaces separate from sparse lane-aware, uniquely identified RoadNodes. Current scene/graph was authored before this decision: DO NOT treat it as scaled correctly already; calibrate ground-grid projection, route lanes, vehicle motion, PPU and camera in Unity with tests. Do not mistake the default orthogonal Scene View grey background grid for the selected isometric Tilemap overlay. See [City Scale & Street Standards](CITY_SCALE_AND_STREET_STANDARDS.md).
- **Unity scene authoring:** Use native Tile Palette / Grid / Tilemaps and authored scene/prefab data. For ad hoc repetitive edits, the official Unity CLI with the existing `com.unity.pipeline` package can drive a connected Editor and evaluate C# live; it does **not** eliminate Editor/session requirements or the need to save, validate and playtest assets. No replacement monolithic `CityBuilder`.

Canonical detailed rules: [World & Content Data Contract](WORLD_CONTENT_DATA_CONTRACT.md).

## 1. Start clean, not by porting the mistakes

The owner explicitly requested a **fresh Unity project**, not migration from Godot 4 or Babylon.js. We preserve the **game's identity and useful lessons**, not code, files, event schemas, abstractions, performance liabilities, scene graphs, persistence migrations or technology decisions from the previous repos.

- Do not paste GDScript/TypeScript into C# mechanically.
- Do not recreate a gigantic event-sourcing system because one existed historically.
- Do not retain old save compatibility, fixed fixtures or test APIs without a new product reason.
- Do not write an adapter layer simply to impersonate another engine's abstractions.
- Don't import previous Godot taxi physics, path reservations or occupant lifecycles as unquestionable correct designs.
- Revisit each feature's **player-facing need** before choosing its implementation.

## 2. Production software from the start — not disposable-proof architecture

The owner explicitly rejects the failure mode where an agent builds a temporary demo/vertical slice and then extends it forever, declaring it the game's foundation.

That does **not** demand a huge grand architecture before playable functionality. It means:

1. Implement a **small genuine production system** for a real player action.
2. Give it reasonable ownership, maintainability and clean boundaries.
3. Expand through intentional extensions when the next player-facing need exists.
4. If a temporary experiment is made to answer an isolated question, clearly label it disposable and remove it before production work depends on it.
5. No agent may call a temporary scene, debug dashboard or one-route fixture "the production architecture" by default.
6. No mandatory fixed sample taxi ride becomes the canonical game loop.

**Small amount of functioning production code > large scaffolding > endless technical prototypes.**

## 3. The engine owns engine responsibilities

Use Unity-native systems where suitable:

- **World authoring:** 2D Isometric Tilemap for repeated ground/roads where sensible; authored scene objects/sprites for unique structures and curved/elevated areas.
- **Sprites/animation:** SpriteRenderer, Unity 2D Animation, sorting, Sprite Atlases and existing tools when appropriate; pick exact techniques only when needed.
- **Camera:** Unity 2D camera and ordinary follow tools, not a custom projection framework automatically ported from Godot.
- **Physics:** Physics2D for active nearby actors and relevant contact, not manual thousands-of-shape collision sweeps without proof.
- **UI/input:** Existing Unity systems; investigate what's already installed before adding a new framework.
- **Scene/editing workflow:** The level is authored in editor-managed assets. Neither a serialized Unity scene nor a C# file should become an excuse for a giant custom town renderer.

Do not treat a Tilemap as suitable for absolutely everything. Distinct props, characters, special bridges and curved roads can be GameObjects or authored assets; use the simplest maintainable tool.

**Important geometry boundary:** Tile sorting is not road connectivity. Elevated road segments and lower crossings may overlap on screen but must remain topologically distinct unless a real ramp/junction connects them.

## 4. Keep distinct responsibilities without framework theater

A useful conceptual separation—not an instruction to create one assembly/manager per noun—is:

- **Content/world description:** Authored roads, places, residents and dialogues.
- **Game state/rules:** Player money and tools, known people, actual ride states, persistent relationships, valid choices.
- **Local scene actors:** Taxi, active pedestrians and nearby vehicles moving/rendering through Unity.
- **Presentation/UI:** What the player sees and can select; no hidden rules or independently invented world truths.

Avoid:

- One enormous mutable GameManager/WorldGod responsible for every city, taxi, conversation and rendering decision.
- Parallel authoritative states for the same fact.
- Dozens of one-method files/interfaces for imaginary scale.
- Generic message-bus/event-sourcing/inversion-of-control structures built before any player-facing need.
- Deep inheritance hierarchies for trivial NPC differences.
- Separate incompatible "fake visible" and "real colliding" NPC worlds.

**Cohesion first.** A class/module should own a clear domain concern and be small enough to understand, but there is no arbitrary "ten-line file" rule.

## 5. World life is perceived; simulation is budgeted

The game should feel alive without running expensive complete AI on every distant resident.

- Nearby **active** pedestrians/cars: smooth native scene movement and enough avoidance to prevent obvious nonsense.
- Offscreen **important** residents: maintain necessary identity, plausible schedule/position and relationship state, not per-frame physics.
- **Ambient** passersby: presentation at an affordable budget; not every background individual deserves a permanent rich domain model.
- Reevaluate preferences/schedules on relevant events or coarse ticks when possible.
- Do not do broad event-history scans, long lists of collision queries or full relationship recomputations every rendered frame.
- Establish continuity when actors enter/leave active view so they don't teleport, duplicate or orphan real physical blockers.
- Use object pooling/culling only if actual measured need exists; don't build a generic streaming framework on speculation.

This is a **budget principle**, not an order to implement a three-tier NPC engine immediately.

## 6. Performance requires actual evidence

The historical Godot project's Windows test reported approximately **28 FPS falling to 14 FPS** over a longer session with very few nearby vehicles. The same report logged roughly 0.7ms physics time while main process time climbed; the team could not conclusively isolate the Windows cause. Deadlocked vehicles/pedestrians also occurred. Those facts justify caution, **not** a claim that Godot as an engine caused the regression or that Unity will automatically cure it.

Engineering rules:

1. Test in actual relevant **built player runtime**, not only Unity Editor.
2. Measure **frame time, spikes, memory allocations/GC, render, scripts and physics** over extended play, not just one startup screenshot.
3. Sample an authentic taxi ride with active pedestrians and ongoing time progression.
4. Investigate **root cause** before rewrites or adding DOTS/Jobs/Burst/native plugins.
5. Use appropriate background abstraction and culling; don't use heavyweight physics on the entire city.
6. Do not fabricate FPS/readiness claims or pretend green tests replace a real human playtest.
7. A target such as 60 FPS is reasonable to evaluate, but **no universal mandatory platform-specific FPS budget has been approved** yet.
8. Avoid unnecessary GitHub Actions minute expenditure; prefer sufficient lightweight checks when available.

Do not optimize by violating social/world consistency, deleting persistent people, or silently teletransporting blocked cars.

## 7. Test the player's actual game, not merely the author's utilities

- Player-facing interface should **default to playing**, not to debug dashboards.
- Developer inspection/profiling may be available separately; do not confuse developer counters and usability.
- Validate what a player can genuinely do: find transport, take an actual connected ride, read/talk, arrive, remember an NPC, walk in home/neighborhood, see coherent road/actor visuals.
- Tests must not rely on one canonical test passenger as universal gameplay.
- Test important invariants only when those systems actually exist. Examples: pickup/arrival coherence, no duplicate fare settlement, no ungrounded teleport, persistence of a known relationship.
- Label verification truthfully: code compiles; automated runtime passes; Windows/Web builds work; human visual/feel acceptance—all different.

## 8. Do not pre-commit unknown design choices

Unknown design details belong in [Decision Register](DECISIONS_AND_OPEN_QUESTIONS.md). An agent must not select and ship, without approval, decisions such as:

- combat, quests, story progression;
- a complete procedural content pipeline;
- all-city foot exploration;
- exact manual driving mechanics;
- NPC count/AI tech stack and simulation cadence;
- police pursuit/enforcement system;
- full economy/maintenance failure state;
- portrait art pipeline, pixel style, UI layout;
- Ink or another narrative runtime;
- multiplayer/modding/online AI integrations;
- target platform/release packaging;
- specific savings of legacy Godot data.

**Technology convenience is not product approval.**

## 9. How to structure future work

Every nontrivial change must state:

- **Intent:** Which already approved player experience does it improve?
- **Scope:** What is included and explicitly excluded?
- **Design status:** Approved, proposal, open or technical test?
- **Native approach:** Which Unity tool is used and why?
- **State ownership:** Where are authoritative data, visuals and persistence stored?
- **Risk:** Does it introduce dependencies, CPU load, deep coupling, or uncontrolled scope?
- **Verification:** Which tests actually ran, on what runtime, with what result?
- **User signoff:** Whether anyone actually played/approved the change.

If a feature is not authorized, stop and discuss the proposal instead of quietly shipping it into the foundation.

## 10. Repository conventions and safe maintenance

- Before modifying source, inspect current Unity project version, package manifest, root AGENTS.md, relevant docs, main/PR status and code. Never trust a stale handoff.
- Don't mass-reformat generated Unity assets, scene YAML or package lock files unnecessarily.
- Maintain Unity .meta/GUID integrity when adding/moving assets.
- Keep C# code readable and compact, with modules defined by actual responsibility.
- No blanket custom editor/tool pipeline to avoid working in Unity's own scene/editor interface.
- No old Godot/Babylon code or incompatible save migration brought into this repo.
- Use source control intentionally; no competing edits on the same scene/branch.
- Update the decision register when an actual **owner** decision resolves an OPEN item; don't invent authorization through documentation alone.

## 11. Historical context that must not become authority

Previous repositories:

- [outsider-driver](https://github.com/MohamedXIV/outsider-driver) — Babylon.js/TypeScript 3D-first era and valuable game/art discussions.
- [outsider-driver-2d](https://github.com/MohamedXIV/outsider-driver-2d) — Godot 2D, living sandbox iterations, performance and architecture cautionary evidence.

They may be consulted for **concepts**, prior user feedback, visual concepts or real bug histories. They are **not dependency repositories**, nor an issue backlog to blindly reimplement.

**Final rule: The game's simplicity is a design commitment. Don't create an elaborate simulation-engine project and call it Outsider Driver merely because it supports taxi rides.**
