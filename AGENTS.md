# AGENTS.md — Mandatory Instructions for Every Outsider Driver Agent

**Repository:** MohamedXIV/OutsiderDriver  
**Engine:** Unity 6 / C# / 2D Isometric, currently **6000.6.0f1** in ProjectSettings/ProjectVersion.txt.  
**Product authority date:** 2026-10-24.

> **STOP AND READ BEFORE ANY DESIGN OR IMPLEMENTATION.** This is a **clean new game**, not a migration of either previous Outsider Driver repository. Do not infer scope, architecture, feature implementation or artist-approved specifications from old prototypes. The owner explicitly rejected systems fabricated by agents and temporary prototypes promoted into the permanent foundation.

## Required reading order

1. [docs/GAME_DESIGN.md](docs/GAME_DESIGN.md) — canonical full game design and boundaries.
2. [docs/DECISIONS_AND_OPEN_QUESTIONS.md](docs/DECISIONS_AND_OPEN_QUESTIONS.md) — exactly what's approved, historical, suggested, unresolved or rejected.
3. [docs/WORLD_CONTENT_DATA_CONTRACT.md](docs/WORLD_CONTENT_DATA_CONTRACT.md) — mandatory 2D isometric and data-driven world/content requirements.
4. [docs/CITY_SCALE_AND_STREET_STANDARDS.md](docs/CITY_SCALE_AND_STREET_STANDARDS.md) — approved physical scale, street classes, lane-aware navigation and road-authoring rules (P-031–P-033).
5. [docs/SYSTEMS_ARCHITECTURE.md](docs/SYSTEMS_ARCHITECTURE.md) — technical blueprint: VContainer DI, TimeService, GTA traffic bubble, Ink dialogue, UI Toolkit MVP.
6. [docs/VISUAL_DIRECTION.md](docs/VISUAL_DIRECTION.md) — art intent, user image-reference descriptions, non-decisions.
7. [docs/ENGINEERING_PRINCIPLES.md](docs/ENGINEERING_PRINCIPLES.md) — clean Unity approach, production rules and performance discipline.
8. This repository's **actual current project and source**, open issues and PRs, before changing anything.

If your task touches only one narrow topic, read its relevant sections fully and cross-check the decision status. Do not use this short entry file as a replacement for the full detailed docs.

## One-paragraph game identity

Outsider Driver is a **beautiful, colorful, cozy-but-socially-dangerous alien retro-futurist life sandbox**. The player is an illegal human under cover working as a taxi driver among alien societies. The point is to **carry passengers, talk to them or stay quiet, perhaps form friendships or romance, and live/explore freely within the current limited home/neighborhood walking scope while a believable city goes on around them**. Driving is semi-autonomous; the city is authored and 2D isometric. There is **no combat, no procedural generation requirement, no quests, no mandatory main storyline, no sprawling strategy or simulator gameplay**.

Everything implemented should strengthen that experience. If you cannot explain which part of the above a feature improves, it is likely out of scope.

## TWO NON-NEGOTIABLE PROJECT INVARIANTS (owner decision 2026-10-24)

**1. This is a genuine 2D ISOMETRIC game.** Unity 6/C# with 2D sprites, native isometric Tilemaps, authored 2D world objects and 2D scene conventions. Do not turn concept illustrations with depth into a 3D scene, a 3D mesh-first pipeline, a first-person taxi world, or custom projection/rendering infrastructure. Height and multi-level roads can be depicted in 2D with coherent authored navigation. Exact art scale and rendering settings remain OPEN; the **2D isometric requirement is not open**.

**2. Prefer DATA-DRIVEN content wherever practical.** Districts, streets, blocks, buildings, locations, residents and other editable world content should be represented through stable IDs plus authored metadata (location/coordinates/bounds when needed, names and candidate names, descriptions, history/lore, relevant asset references, etc.). **Stable IDs, e.g. `district_01`, are distinct from human-readable/localized names**; renaming a district or rewriting its history should not require editing gameplay rules. Keep identity separate from authored content and separate from runtime mutable state. Use existing simple ScriptableObjects and Unity scene references; no unnecessary generic CMS or invented schema.

**3. IN-WORLD NAMES MUST BE ALIEN (owner, 2026-10-24).** All actual city/district/neighborhood/street/landmark/building names and **colloquial nicknames** must originate within fictional alien languages/cultures. English names and English compound-word nicknames are not diegetic canon. English descriptions (e.g. "The Docks", "Outer Margin") are allowed as **development labels only**. Proper names may be transliterated for readable UI, without silently translating their names into English phrases. Names should have authored cultural and linguistic provenance where needed; provisional language IDs and species IDs are not settled lore.

**Currency is OPEN.** Earlier candidates **Rin, Tal, Vara** are suggestions only; `cr` / `credits` are temporary technical accounting labels, **not** an approved in-world name or symbol. Do not canonize any by inference.

**Data-driven != procedural generation.** Locations and world layout are authored. The prior four-district brainstorm and candidate place names are **NOT canon** merely because they appear in images or conversation. See [World & Content Data Contract](docs/WORLD_CONTENT_DATA_CONTRACT.md) for precise rules, examples and known existing-code divergences.

## Absolute behavior requirements for agents

### 1. Never hallucinate product decisions

- An owner's latest explicit approval outranks the previous repositories and previous assistant proposals.
- Use the labels in the Decision Register: **APPROVED, HISTORICALLY AGREED, REFERENCE, PROPOSED, OPEN, REJECTED, IMPLEMENTED**.
- An OPEN item is **TBD**. You are not permitted to mark it approved or implemented by arguing it is sensible, popular, more immersive or technically easier.
- If necessary product input is missing, seek the owner's decision or constrain your implementation to what is already approved.
- Don't silently convert a sample character, example taxi request, art screenshot or old issue into an exact feature requirement.
- Never present a source file that does not exist, unrun test, untested platform, unreviewed art asset or historical feature as if verified in this Unity repository.

### 2. The game has intentionally narrow player systems

- Core: passengers and rides; voluntary conversation; optional recurring social ties; beautiful living city; bounded on-foot home/neighborhood life.
- Preserve human illegal-resident premise, alien languages/cultures, sensible ordinary income/tools without forcing huge management or espionage mechanics.
- No combat, quests, mandatory campaign, generated world/story, city-wide on-foot feature creep, full physics traffic simulator, all-citizen AI, per-NPC LLM, or forced romance.
- Work offers are **ordinary taxi work**, not quests.
- Authored dialogue may be contextual and reusable; not automatically one handcrafted sequential episode per passenger.

### 3. Do not port code/architecture from the former projects

Older projects are **design archaeology and lessons**, not the new engine's starting code.

**Old repositories:**
- https://github.com/MohamedXIV/outsider-driver
- https://github.com/MohamedXIV/outsider-driver-2d

**Banned default patterns:** Godot draw_colored_polygon city renderer, TypeScript/Babylon scene bridge, old-world event ledger carried over unchanged, monolithic Godot SandboxWorld translated line-by-line, migration system for experimental saves, fixed clinic passenger as production game, abstract five-node timer city, or copying old physics deadlock code.

### 4. Use actual Unity tools

- Use Unity's real **2D isometric Tilemaps** when appropriate; static authored objects and SpriteRenderers where better.
- Native 2D tools for camera, animation, collision and UI where sensible.
- The 2D isometric scene is not a reason to build a second custom renderer/projection engine.
- Prefer readable, cohesive C# code with clear responsibility. Neither a God class nor hundreds of tiny files.
- Respect scene/.meta/GUID hygiene and actual installed package versions.
- Don't add a package or framework without a genuine product need.

### 4a. Legacy scene quarantine and Unity CLI authoring (owner 2026-10-24)

**`TaxiSandboxBuilder` has been deleted from the repo, together with its sole Editor assembly. NEVER recreate, extend, invoke or recommend it as the city/world production workflow.**

- `Assets/_Project/Scenes/TaxiSandbox.unity` is **LEGACY / TEST ONLY**, retained temporarily as a serialized inspection/behavior fixture. It is **not a production city template, not authoritative map or road data**, and is not included in `ProjectSettings/EditorBuildSettings.asset`. A node or placeholder in it does not justify introducing that location into real world data.
- `TaxiTestDriver` is also a sandbox harness; `RoadNetwork`, `RoadNode`, `VehicleController`, `HomeNeighborhoodLayout`, etc. are separately implemented runtime components and **not automatically discarded** just because the old scene author was removed. Review each asset's actual role and references before changing it.
- For work in a **running local Unity Editor**, prefer native 2D Isometric Grid/Tilemaps, Tile Palette, editable Scene/Prefab assets and the official **Unity CLI + `com.unity.pipeline`** (`unity command`, `unity command eval`, `unity command eval_file` where supported) for *small, inspectable editor actions*. The package is in this project's manifest, but confirm that the installed CLI and Editor connection work; **GitHub API access alone cannot run those local commands**.
- CLI/eval is an **authoring/automation interface**, NOT a second renderer, new gameplay runtime or substitute for saved and editable scenes. After authoring changes, save through Unity Editor, review serialized scene and meta GUIDs, inspect actual visuals/road links, and test in Editor and PlayMode where possible.
- No new monolithic `CityBuilder` or universal generator. For repeated road editing, start with existing native Tile Palette / prefab workflow; add a small editor-only helper only when demonstrated need outweighs its maintenance cost.

### 5. No throwaway-scene-to-production pipeline

The owner explicitly does **not** want another throwaway demonstration turned by an agent into the foundation over dozens of PRs.

- Build the intended **production** behavior in small coherent increments.
- Keep the design grounded and modular without pre-building complex future systems.
- Temporary experiments must be isolated and discarded or intentionally replaced; never silently call them the architecture.
- Do not expand the product around a technical fixture simply because it already exists.
- **No 'vertical slice' program offered as a substitute for proper production work.**

### 6. Optimize from measured reality

Previous Godot Windows human gameplay experienced vehicle/pedestrian stalls and falling FPS despite few active cars. The exact source of the FPS loss was not proven engine-specific. Unity alone does not solve poor update patterns.

- Test actual **built runtime** and extended sessions, not only Editor or short CI run.
- Don't update all distant actors at full fidelity every frame.
- Keep believable on-screen residents and traffic consistent with their backing state; no physics ghosts or duplicate inhabitants.
- Use cheaper state representation away from the player only as actually needed.
- No speculative full DOTS/ECS/Jobs/Burst rewrite, custom physics engine or speculative performance framework.
- Do not call a feature visually accepted, smoothly playable, or performant without evidence.
- Conserve GitHub Actions minutes unless a workflow is genuinely needed.

### 7. Keep art attractive and decisions open

The city should look bright, warm, varied and alien-retro-futuristic. Attractive residents, rich comfortable homes and expressive conversation portraits are important. Small character art **might** be pixel-art-like but is **not finalized**.

- Past illustrated 3D asset sheets are **visual references**, not approval to switch away from Unity 2D.
- No generic purple neon cyberpunk, always-dark city, universal gray roads, mandatory 3D cockpit or infinite flying cars.
- The owner supplied five reference images in chat on 2026-10-23; they are **not currently versioned in this repository**, so this docs-only source does not authorize claims of pixel-perfect visual matching.
- Review actual images once made available; use the grounded description in VISUAL_DIRECTION.md meanwhile.

## Approved vs implementation reality

- Approved = product intent.
- Documented = written reference.
- Implemented = code and assets **present in this Unity repo**.
- Verified = exact current implementation tested in the relevant actual environment.
- Human accepted = the owner actually assessed the relevant build/art experience.
- These states do not imply one another.

**Do not mark documentation changes as implementing a Unity game feature.**

## Before starting a nontrivial task

Record in the issue/PR or concise work notes:

1. Relevant owner-approved requirement(s) by ID from the Decision Register or exact section of Game Design.
2. Relevant still-OPEN decision(s) — must not be silently decided.
3. Actual repo main, overlapping issues/PRs, current Unity packages/scenes.
4. Smallest **real production** behavior to implement, including explicit exclusions.
5. Why Unity-native built-in tooling suffices or where its absence is proven.
6. What the change must show to be considered technically verified and which player-visible behavior to check.
7. Real checks performed; remaining gaps; whether human acceptance is pending.

An assistant-generated plan is never a new product decision. If user consent would be needed for an unrelated feature, do not build that feature merely to fill out a roadmap.

## When changing these documents

- Never erase prior decisions or secretly promote a proposal to approved.
- Explicitly record the owner decision, date and which ID/paragraph it supersedes.
- Keep the detailed documents consistent, and cite source links when available.
- Do not copy outdated summaries back into the canonical docs.
- If two approved decisions seem inconsistent, report the conflict rather than inventing a reconciliation.

**Final invariant:** Build a simple, beautiful, social taxi-life game. Let the city feel alive without building a second game engine to simulate it.
