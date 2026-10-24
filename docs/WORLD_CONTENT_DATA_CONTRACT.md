# Canonical 2D Isometric & Data-Driven World/Content Contract

**Status:** APPROVED product/engineering rules from the owner, 2026-10-24.  
**Audience:** Every implementation, art, level-design, content, dialogue, tooling and planning agent.  
**Read with:** [Game Design](GAME_DESIGN.md), [Decision Register](DECISIONS_AND_OPEN_QUESTIONS.md), [Systems Architecture](SYSTEMS_ARCHITECTURE.md), [Engineering Principles](ENGINEERING_PRINCIPLES.md).

> **Non-negotiable 1: Outsider Driver is a genuine 2D ISOMETRIC game.**  
> **Non-negotiable 2: Prefer DATA-DRIVEN authoring whenever it provides genuine flexibility, particularly for districts, locations, streets, residents and other game content.**
>
> This document is a **design contract**, not proof its complete data model has been coded. A conceptual example here must not be mistaken for an approved exact C# schema, coordinates or locked place names.

## A. Isometric means 2D, not a 3D world with an isometric camera

- **Actual target:** Unity 6 / C#, a **2D isometric world**, with Unity 2D sprites, authored isometric Tilemaps where appropriate, SpriteRenderers/Sorting, 2D animation and 2D interaction/collision.
- The word **isometric** describes the visual projection/perspective; **2D** describes the game/art/authoring foundation. Both are essential. Neither can be silently dropped.
- Buildings can *look* rounded, layered, multi-storey and volumetric in painted **2D artwork**. Roads can *look* elevated, supported and three-dimensional while still using 2D scene assets and explicit road connectivity/elevation semantics.
- Concept boards showing perspective, cutaway dioramas, 3D model sheets or first-person taxi views are **art/mood/world-design references only**. They do not approve a Unity 3D world, 3D-first assets, runtime mesh city, first-person cabin mode, or a hybrid 3D world.
- Native Unity 2D authoring (Tilemaps + authored SpriteRenderer-based scene objects) is the first choice. Never default to a custom draw-everything polygon system, mathematical fake projection framework, procedural geometry renderer, or 3D mesh simulation because of old project history.
- **Not yet locked:** exact camera angle, tile dimensions, pixel density, art style, Y-sorting settings, height/occlusion mechanism. Do not fabricate these and call them approved. Technical changes to the fundamental 2D representation need explicit owner consent.

### 2D isometric world checks for an agent

Before shipping work on city, people, cars, buildings or streets, confirm:

1. Are we still authoring an actual Unity **2D isometric** game, not a 3D scene presented diagonally?
2. Are sprites/2D scene assets the source of world visuals, not code-generated polygon drawings?
3. Do cars/pedestrians/buildings respect a coherent isometric ordering, walkability and collision?
4. If roads visually overlap at different heights, does the authored connectivity reflect *real ramps/junctions* rather than letting vehicles switch elevation at a visual crossing?
5. Are art-sheet appearances being treated as reference instead of proof of a runtime art implementation?

## B. Data-driven content is the default, not an optional afterthought

**Owner decision:** Content and world descriptions should be **data-driven whenever practical**, so the designer can edit, rename, add, remove, relocate and reorganize authored places and content without rewriting gameplay code.

A good guiding split is:

| Concern | Primary source of truth | Examples |
| --- | --- | --- |
| **Stable identity** | Data identity / reference | `district_01`, `district_02`, `location_001`, `resident_001` |
| **Author-facing content metadata** | Authored data | Working/candidate names, descriptions, local history/lore, cultural context, tags |
| **Player-facing wording** | Localizable authored text | Display name, public nickname, descriptions and dialogue text by language |
| **Spatial and navigation description** | Authored world/scene data | Position/anchor, footprint/boundary, connected roads, membership, landmark references |
| **Visual/audio configuration** | Referenced Unity assets or small content configs | Sprite/Tilemap/Prefab refs, palette, signage, local ambience |
| **Rules shared by all entities** | Small cohesive C# systems | Travel, fare settlement, NPC presence, dialogue condition checks |
| **Mutable game state** | Runtime/save state **keyed by stable ID** where needed | Relationships, current passenger trip, discoverability, player knowledge |
| **Implementation details** | Engine scene components / runtime | SpriteRenderer, Collider2D, scene Transform, camera behavior |

**Never use a display name as the primary key.** Names, nicknames and lore are meant to change without invalidating saved references or scene/road/content links.

### Stable IDs: identity must not encode flavor

- Use stable, opaque, developer-facing IDs such as **`district_01`**, **`district_02`** and so on for districts **when creating their real records**. These are exactly the kind of IDs the owner prefers.
- IDs are not automatic player-facing names and should not encode a current district's culture, economy or candidate label. **Do not use** a rename-sensitive key like `district_market_junction` as the foundational identity merely because today's working label is Market Junction.
- **ID order does not imply geographic adjacency, wealth, progression or implementation sequence.** `district_03` is not inherently next to `district_02`.
- Use sensible distinct stable IDs for roads, stops, locations, residents, buildings and other referenced content where stable references are useful. Their exact prefix and numbering scheme outside districts are *examples*, not newly approved schemas.
- Do not renumber IDs because a district is renamed, deleted from a concept sketch, or moved.
- Designers may use a more memorable **internal editor label** alongside the opaque key; the canonical key remains stable.
- Content records must be validated for duplicate IDs and broken references when implemented, rather than silently choosing the first conflicting item.

### Districts: data fields to make editable where needed

A district record should **be able to represent** the following categories as design calls for them:

1. **Identity:** stable district ID; optional content/version state (draft vs approved) for authoring.
2. **Names:** current working name, candidate names and their *approval status*, optional nickname(s), localized player-visible name key.
3. **Description:** short designer summary, player-facing copy if distinct, atmosphere/identity.
4. **Lore/background:** history, social context, distinctive architecture/materials, local practices or events. A district's story should be editable **without editing a C# class**.
5. **Spatial information:** authored location/anchor and, when needed, district **bounds/footprint**; space/layer/elevation distinctions where appropriate.
6. **Structure:** parent/child or member-area references; street/road sections, stops, landmarks, important buildings or POIs **by stable reference**.
7. **Visual and audio identity:** palette, art set/asset references, signage themes, ambience/radio where needed.
8. **Gameplay-relevant authored settings:** only values systems already actually consume (e.g., a local traffic budget or fare modifier), not speculative political-economy simulation parameters.
9. **Relationships between records:** adjacency/road access may be referenced when necessary, but the road graph remains the authoritative source of **where driving connections actually exist**.

**Keep descriptions optional and scoped to actual content needs.** A rich field catalog is guidance for authoring, **not** an instruction to implement every field, build a data editor, world registry or generic metadata framework now.

#### Illustrative authored record (NOT a committed exact schema)

```json
{
  "id": "district_01",
  "workingName": "Outer Margin",
  "nameCandidates": ["Outer Margin", "TBD — in-world name"],
  "displayNameKey": "world.district_01.name",
  "summary": "Dense, practical residential and small-workshop district",
  "background": "TBD — authored history, not invented by an agent",
  "mapAnchor": "TBD — authored Unity world coordinates",
  "boundary": "TBD — authored district footprint",
  "landmarkIds": ["location_001"],
  "artProfileId": "TBD"
}
```

**Everything above, including assigning district_01 to Outer Margin, is illustrative**. It does not commit the district order, content list, actual names, coordinates, JSON storage, field names, topology or lore. In production, one or a few Unity ScriptableObject types plus authored scene references may be more appropriate; do not replace the entire Unity authoring experience with JSON unless it solves a genuine need.

### Alien-only diegetic names, including everyday nicknames (owner decision P-029)

**Hard rule:** All names a resident actually uses for a city, district, neighborhood, street, landmark, business, institution or building—even colloquial descriptive slang—must belong to an **in-world alien language**. An English compound-word nickname is **not** alien simply because it sounds poetic.

- `The Docks`, `Market Junction`, `Upper Route`, `Outer Margin`, `Lantern Row`, `Patchworks`, etc. may remain **functional development descriptors** only. They are **not approved alien proper names**, official titles or resident slang.
- A meaningful name can have an authored spoken/written alien form, associated language/register, cultural or community provenance, historical origin, and the meaning/gloss for developers. **Use only fields needed by real content.** Do not invent a complete conlang or political history as filler.
- Names may eventually differ across official, local colloquial, historical and other-language usage. **All variants must still be alien-language names**. Multi-name complexity is not a requirement until actual writing uses it.
- For a localized player UI, preserve the approved alien proper name through suitable script/transliteration. A human-language gloss may explain its meaning in text, but must not be mistaken for a name the city's residents utter.
- **Species ≠ Language ≠ Culture ≠ Community ≠ Faction/Affiliation.** Never derive a citizen's language or regional nickname automatically from their species. Previous `Trade/Common`, `Local`, `Cant` are provisional language labels, not named fictional tongues; `kind:a` etc. were experimental species identifiers, not settled lore.
- **Currency still undecided:** Previous suggested names **Rin / Tal / Vara** are all unapproved. Internal `cr` and `credits` are temporary account/coding labels, **not** the in-world currency name or symbol. Do not invent canonical currency etymology or finalize it in asset text.

**Keep the world deep through deliberately written history, economics, languages and social memory; do not fabricate those elements automatically because a data schema has optional metadata fields.**

### Coordinates, area boundaries and streets

- Locations should have **real authored spatial meaning**, not exist only as labels and unexplained five-node teleport graphs.
- A Unity `Transform`, serialized coordinate, map-space anchor or authored footprint is appropriate depending on the record and how the level is edited. Do **not** duplicate authoritative coordinates in multiple places where they will drift.
- Distinguish **position** (anchor/representative point), **shape** (actual district boundary or lot footprint when needed) and **connectivity** (actual accessible roads/ramps). A point position does **not** define district limits or guarantee route access.
- Street hierarchy, regular blocks and standard-sized lots are **city-planning direction under discussion**, not an imposed procedural generator. When standardized footprints are chosen, store the parameters as authored data rather than scattering widths/sizes in unrelated hardcoded scripts.
- Same for candidate district divisions and nicknames: the owner has not yet locked the new four-district geography; do not make a beautiful brainstorm board into the immutable official city layout.
- In-world names should have cultural history and nuance; keep **candidate names and status** separate from the approved localized display name. A list of candidates is an **editorial aid**, not a runtime random-name pool.

### No confusion between data-driven and procedural generation

- **Data-driven = humans/designers author the city and its content as editable structured records/assets.**
- **Procedural generation = an algorithm creates a city/map/story from rules or seeds.** The latter is explicitly *not* the game's planned world-authoring model.
- The runtime may dynamically select a valid authored passenger, ride or dialogue based on time and state; that is **systemic gameplay over authored data**, not authorization to generate unapproved lore, arbitrary geometry, or random canonical names.

## C. Integrate with what the Unity repository already has

The new repo already includes:

- `Assets/_Project/Scripts/Runtime/Data/DistrictConfigSO.cs` with `DistrictId`, `DisplayName`, local language, atmosphere, radio, fares/traffic and landmark road-node IDs.
- `Assets/_Project/Scripts/Runtime/Data/PassengerDefinitionSO.cs` with stable passenger ID, identity/culture, portrait/Ink, and schedule.
- `Assets/_Project/Scripts/Runtime/Navigation/RoadNode.cs` with node IDs, position from a Transform and connected road nodes.
- The old `TaxiSandboxBuilder.cs` was **deleted by explicit owner instruction on 2026-10-24**, including its Editor assembly; it must not be recreated as a production authoring tool.
- `Assets/_Project/Scenes/TaxiSandbox.unity` is a **legacy, unshipped test fixture**, NOT the production district layout or road authoring source. Its hardcoded street/node names are placeholders. `TaxiTestDriver` is similarly test-only.

**Known delta vs this contract:** `DistrictConfigSO` still has example defaults `district_amber_terrace` / `Amber Terrace`; do not mistake them or existing legacy-scene nodes for official names. Future authored district/road assets must use stable independent IDs and reliable editable Unity scene/Tilemap/Prefab data. Prefer native Unity authoring and the official Unity CLI + `com.unity.pipeline` to drive local Editor changes when beneficial; do not write another monolithic scene generator. Changes must be saved and verified in the actual Unity Editor.

Prefer **extending and using the existing minimal domain-specific ScriptableObjects** where useful over creating duplicate district models, competing registries, a new database, a full-blown content-management platform, or another custom editor.

## D. Production checklist

For every world/content change, the agent should be able to answer:

1. What stable ID identifies this thing? Is it independent of its name?
2. Which fields are authored data versus runtime state?
3. Can the owner edit its displayed name, lore, relationship, artwork or approved metadata **without changing gameplay code**?
4. Does spatial data have one clear authority, and does road connectivity match what the scene shows?
5. Are candidate names and unapproved lore recorded as **draft**, not claimed to be in-world canon?
6. Do changes continue to respect **actual Unity 2D Isometric authoring**?
7. Does the record have a real player/content need, or is it speculative schema bloat?
8. Have references, localization keys, scene usage and missing optional fields been checked, without claiming unrun tests?

**Final invariant:** Make the **world authored, editable and richly describable as data**, and let the **game run as true 2D isometric Unity gameplay**. Data-driven is a flexibility principle—not a reason to build a second engine.
