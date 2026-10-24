# Outsider Driver

**A living, colorful alien city. An old taxi. A human who is not supposed to be here.**

You are an illegal human resident living under a cover identity in a non-human civilization. Make a living carrying passengers, talk to them or enjoy the silence, learn the city and its languages, become familiar with people if you wish, and spend ordinary time at home or walking around your neighborhood.

**This is an intentionally simple, freeform social/taxi-life sandbox** — not a combat RPG, quest campaign, procedural city generator, giant economy simulator or all-citizen AI research project.

## Current project

- **Engine:** Unity 6 (repository currently targets **6000.6.0f1**).
- **Language:** C#.
- **Non-negotiable view:** genuine **2D ISOMETRIC** game using authored Unity 2D sprites, Tilemaps and 2D scene conventions. Not a Unity 3D city seen through an angled camera.
- **Driving:** semi-autonomous connected-road travel; route/destination decisions rather than a mandatory hardcore manual driving game.
- **Walking:** real on-foot freedom, **initially limited to the player's home and own neighborhood**.
- **Presentation:** beautiful, bright, cozy, rich **alien retro-futurism** and expressive attractive characters. The exact in-game sprite/pixel-art treatment is **not finalized**.
- **Narrative:** voluntary contextual conversations, recurring individuals and possible relationships; no compulsory plot/quests.
- **City:** ordinary activity and believable local life without needing a detailed simulator running for every resident.
- **Content:** **data-driven by default** where practical: stable IDs (`district_01`, etc.) independent of editable names, candidate names, lore, world positions/bounds, relevant references and authored metadata.

**Repository review (October 24, 2026):** The Unity project now has authored C# runtime foundations for vehicle movement/traffic, road routing, ride handling, home/on-foot, dialogue, localization, audio, portraits and related EditMode tests. This is evidence of code presence, **not independent verification of Gemini's reported 46/46 test result or a finished playable city**. Code from earlier Godot and Babylon projects is not migrated.

## Canonical documentation

| Document | What it answers |
| --- | --- |
| **[Game Design & DNA](docs/GAME_DESIGN.md)** | Detailed premise, activities, passengers, road travel, walking, residents, relationships, language, radio, economy, world, scope |
| **[Decisions & Open Questions](docs/DECISIONS_AND_OPEN_QUESTIONS.md)** | What was approved, historically intended, superseded, merely illustrative or still undecided; IDs for every agent |
| **[World & Content Data Contract](docs/WORLD_CONTENT_DATA_CONTRACT.md)** | Non-negotiable real 2D isometric representation; stable IDs and editable authored world metadata; distinction from procedural generation |
| **[Systems Architecture](docs/SYSTEMS_ARCHITECTURE.md)** | Canonical technical blueprint: VContainer DI, TimeService ticks, GTA-style traffic bubble, Ink narrative engine, UI Toolkit MVP, and localization |
| **[Visual Direction](docs/VISUAL_DIRECTION.md)** | Bright alien retro-futurism, architecture, vehicles, people, cozy home, user-supplied reference interpretations and art-production unknowns |
| **[Engineering Principles](docs/ENGINEERING_PRINCIPLES.md)** | Unity-native 2D approach, clean production systems, performance validation, and previous mistakes to avoid |
| **[AGENTS.md](AGENTS.md)** | Mandatory instructions and reading order for contributors and AI agents |

> **Contributor instruction:** Read [AGENTS.md](AGENTS.md) before making decisions or changing the project. If the design does not actually settle a detail, **mark it open instead of guessing**.

## Production scene and authoring boundaries

- The old **`TaxiSandboxBuilder.cs` and its unused Editor assembly have been removed** at the owner's request. Do not recreate, run or extend a replacement mega-builder.
- `Assets/_Project/Scenes/TaxiSandbox.unity` remains a **legacy inspection/test fixture only**. It is not the city layout or production scene, and is not in Editor Build Settings. `TaxiTestDriver` also remains a test harness.
- Real city construction is **authored, data-driven 2D isometric content**: Unity native Tilemaps/Tile Palette, scene and prefab editing, referenced stable IDs, visible road links. Official **Unity CLI + `com.unity.pipeline`** may automate local Editor actions (`unity command`, `unity command eval`) instead of retaining a huge scene builder. The CLI requires an accessible running Editor and a configured connection; the manifest already includes the experimental Pipeline package.
- No road, landmark or district name from the legacy test scene is game canon. Validate and save real Unity assets before describing them as production.

## Clear product boundaries

- **No combat. No quests. No mandatory main story. No procedural generation pillar.**
- **No city-wide on-foot open world promised now**; home and local neighborhood are the approved initial walking scope.
- **No old code migration** from Babylon.js/TypeScript or Godot/GDScript.
- **No technical demo or temporary test scene promoted as production architecture.**
- **No giant simulation engine** built in advance of real player-facing need.
- **Use native Unity 2D isometric authoring**, not a 3D world, for environment, sprites, animation and UI rather than a custom world renderer.
- **Data-first authored content:** stable content identity, mutable display names/candidate names/lore/geography as data, not hardcoded canon in source.

The game is meant to be **rich in people, atmosphere and possibility**, not rich in compulsory subsystems.

## Historical design references (not dependencies)

- [Outsider Driver — first repository](https://github.com/MohamedXIV/outsider-driver): original alien social-stealth taxi premise, early authored ride and 3D-era visual exploration.
- [Outsider Driver 2D — second repository](https://github.com/MohamedXIV/outsider-driver-2d): Godot systemic sandbox and isometric evolution, living residents, social/language design, playability and performance lessons.

Both contain useful **design discussions**, but they are **not source-code or implementation backlogs for this clean Unity project**.

## Art reference availability

The owner provided contemporary reference images showing a cozy illustrated/isometric room with expressive dialogue portraits, and several colorful alien city/asset/district concept boards. Their **design intent is transcribed in [Visual Direction](docs/VISUAL_DIRECTION.md)**; the binary images themselves have not been committed to this repository yet. Do not pretend they are available for inspection or final art specifications.
