# Visual Direction — Outsider Driver

**State:** Approved art **identity and intent**, but not a locked asset-production specification.  
**Last reviewed:** 2026-10-23.  
**Audience:** art designers, environment artists, UI designers, animators, asset-generation agents, Unity implementers and art reviewers.

This document supplements the canonical [Game Design](GAME_DESIGN.md). When the art references and the historical 3D production sheets disagree with the newer **Unity 2D Isometric** product decision, **Unity 2D wins**. Do not infer that an appealing reference illustration is a usable spritesheet, an in-game screenshot, or a strict material/shader requirement.

## 1. Art north star

**A beautiful, warm, colorful, tactile, alien retro-futurist city built for daily life.** The world should be memorable and pleasant to watch while making long taxi journeys. Alien civilization is the source of architecture, signs, proportions, objects and customs; retro-futurism communicates inherited technology, repairs, layers of history and regional variation.

The overall feeling is:

- **Beautiful and inviting**, not bleak, oppressive or generic neon dystopia.
- **Lived in** and practical, not an immaculate empty showroom.
- **Alien in construction and culture**, not a human downtown with alien extras.
- **Whimsical enough to enjoy, grounded enough to believe** people live, commute and work here.
- **Expressive character appeal**, with a realistic production budget for small 2D actor representations.
- **Coherent across home, neighborhood, streets, passengers, taxi and larger city landmarks**, even if detail level changes by distance.

The city's treatment can be visually rich while the gameplay models remain lightweight.

## 2. The owner-provided reference set (2026-10-23)

**Important:** The actual image files were supplied in the conversation, and are **not committed into this repository**. Future agents cannot pretend they have visually inspected them by reading this Markdown alone. These written observations are only a grounded verbal reference until the actual source assets are versioned.

### Reference group A — cozy interior + expressive conversation art

The first two user images depict:

- A welcoming isometric/pixel-like domestic living room with a large city-at-sunset window, sofa, plants, bookshelves, lamps, floor clutter and intimate warm lighting.
- Small, simple, readable in-world characters seated together, with symbols above heads suggesting casual social interaction.
- Detailed illustrated/anime-like character portraits displayed above or alongside the gameplay, paired with compact dialogue boxes and readable names.
- Warm dusk exteriors contrasted with warm interior lamplight.
- A menu of mundane social actions such as talk, spend time and make tea.

**Approved takeaway:** It is attractive to have **simple in-world character representation plus beautiful, expressive portraits during dialogue**, and personal spaces should actually be inhabitable. An intimate, readable social scene is a valid reference for the home/neighborhood feel.

**NOT approved by this reference:** the exact anime design, romance UI, dialogue box positions, affection symbols, scene composition, character hairstyles, a specific three-person relationship, pixel-art pixel density or Japanese pixel-art as final production art.

### Reference group B — alien city production sheet

The third user image is a proposed **Outsider Driver production asset sheet** exploring:

- Rounded alien civic, industrial, residential and transit buildings.
- Modular arches, curved corners, columns, balconies, railings, skylinks and overhead structural parts.
- Curved and straight street pieces, raised roads, bridge supports, ramps and colored district road treatments.
- Distinct, **ground-contact** practical retro-futurist road vehicle silhouettes: old taxi, smaller personal vehicles, cargo vehicles and shuttles.
- Expressive illustrated signage, alien writing and tactile urban props.
- Hand-painted/aged surfaces and district-specific materials.

**Approved takeaway:** Modular visual design, believable road engineering, friendly retro-futurism, practical vehicle variety, warm stylization and district palettes.

**NOT approved:** producing those objects as low-poly 3D meshes, importing that sheet as atlas without asset extraction/quality work, identical building counts, exact city blueprint or all illustrated visual options at once.

### Reference group C — district visual exploration boards

The fourth and fifth user images compare **The Docks**, **Market Junction** and **Upper Route** visual identities:

- **Docks:** industrial working waterfront, cranes and cargo terminals, teal/blue mixed with warm amber and worn structures.
- **Market Junction:** social and commercial warmth, stalls, broad curved buildings, signs, colored street surfaces, lively traffic and varied pedestrians.
- **Upper Route:** elevated roads and supported interchanges, brighter spacious civic structures and views over the city.
- Some panels show a first-person 3D taxi dashboard because they originated in an **older project era**. This is now **visual composition/history**, not permission to restore a 3D cockpit or first-person travel mode.

**Approved takeaway:** The same world should have locally distinctive identities, bright palettes, architecture suited to purpose, supported roads, visible pedestrian/vehicle life and memorable travel vistas.

**Not approved:** mandatory use of these three particular districts or names, permanent dusk lighting, infinite elevated highway density, or 3D-first development.

**Non-negotiable clarification (2026-10-24):** The actual game is **2D ISOMETRIC**, including city exteriors, home/garage interiors, ground vehicles and people. Perspective concept illustrations, diorama-style planning boards and 3D-looking asset sheets guide **visual shape and mood only**. They do not authorize a 3D city, first-person camera or a 3D asset-production pipeline. For authored geography and city/character metadata, follow the [World & Content Data Contract](WORLD_CONTENT_DATA_CONTRACT.md); editable candidate names and descriptions are data, not painted-in canonical labels.

## 3. Built environment principles

### Alien architecture

- Use broad, recognizable silhouettes and different historic construction families.
- Rounded towers, stepped cylinders, monumental arches and curved balconies can appear without each building being a pod or mushroom.
- Include ordinary service structures, shops, housing, workshops, infrastructure and neighborhood-scale objects.
- Street frontage should communicate function and social class without requiring a label above every building.
- Make structures physically plausible: bridges need supports, roads need believable widths, ramps must land somewhere, stairs/entrances should meet the intended users' physical scales.
- Do not force all architecture into human dimensions, but preserve gameplay readability and approachable navigation.
- Modular kits are encouraged; the city is **hand-authored**, not procedurally generated.

### Spatial realism and escaping the flat planning-board look (P-031 / P-032 / P-033)

**Approved city authoring scale:** **1 logical ground cell = 1m × 1m**, used consistently for building footprints, garage, cars, lanes, sidewalks and curbs. A regular taxi starts around **2m wide by 4–5m long**; typical travel lanes **3m**, sidewalks **2m each side**. Basic two-way local street **10m total**, four-lane (two each way) arterial **16m total** without parking or medians. See [City Scale & Street Standards](CITY_SCALE_AND_STREET_STANDARDS.md); final art sizes/PPU/camera remain open.

**The October 24 Unity Scene View screenshot** shows a functioning-looking isometric-diamond city *diagram*: narrow 1-cell roads, over-regular flat lots and tiny isolated buildings. It is **not visually accepted as the production city's look**, but it is **not evidence that the fundamental 2D isometric projection is wrong**. Make the same 2D isometric city feel lived in via full-width roads, taller articulated facades, relationship between entrances/curbs/garages, intersections, local setback variation and carefully chosen props. Keep stylization rather than copying a real city as photoreal 3D.

The native **Isometric Z as Y Grid + Tile Palette** is the preferred construction/inspection view. The grey general Scene View background lattice may remain orthogonal while the actual selected Tilemap is isometric. Do not introduce custom perspective or a custom editor-grid renderer for this reason.

### Streets and vehicles

- Streets may use teal, faded ochre, mineral cream, blue-gray, terracotta and other reasoned district colors. Grime, repairs and paint wear distinguish materials.
- Provide grounded curves and actual turns; do not make an entire city of perfectly straight thin diagonal lines because an isometric grid is convenient.
- Roads can pass below or above others. Occlusion, barriers, visible underside and traffic continuity must reflect elevation.
- Main taxi and background vehicles have attractive distinct silhouettes; ground contact matters. No ordinary hovering traffic.
- Keep close streets interesting to watch, without making them overdecorated obstacle courses or cluttering driving readability.

### Home and neighborhood

- Comfortable room geometry, modest personal possessions, deliberate lighting and enough spatial readability for actual walking.
- A home is a physical scene with character, not a menu placeholder.
- The neighborhood should have recognizable entryways, people, streets and daily routines, even if only a bounded playable area.
- Where possible, make the neighborhood's culture visible through domestic objects, street furniture, signs, shopfronts and small acts.

## 4. Character design and visual hierarchy

**Two scales can coexist:**

1. **Small in-world actors:** readable silhouettes, efficient sprite representation, enough animation to communicate movement and state. Pixel-art-like design is one possible candidate, not a commitment.
2. **Close expressive portrayal:** more detailed portraits or illustrations that convey emotion, alien physical identity and unique appeal, particularly in conversation.

Desirable qualities:

- Attractive, appealing character art without requiring every individual to be glamorous or identical.
- Distinct species/body shapes and expressive faces or equivalent non-human visual communication.
- Clothes, tools, ornaments, gestures and personal objects should reflect real social context and culture.
- A beautiful portrait must correspond to the persistent character actually present; no random portrait/personality swapping for reused ambient NPCs.
- The same person should remain recognizable across dialogue, world sprite and later encounters.

**Open:** character animation system, exact art style, avatar placement, portrait shader/runtime, levels of detail, resolution, body size conventions, line art treatment and portrait expression count.

## 5. Color, lighting, weather and emotional range

- **Bright scenes are first-class.** Daylight, gold/peach sunsets, cozy interiors, colorful market streets and clear elevated views can all be beautiful.
- Night/dark districts exist if context justifies them, but permanent purple neon, rain and puddles should not define the whole project.
- Emphasize painterly/lightly textured surfaces over excessive tiny geometry; avoid visual noise that hurts readability.
- Soft lighting and atmospheric separation should make buildings and people legible in an isometric frame.
- Lighting is an art goal, not a mandate for performance-heavy realtime GI or many overlapping 2D lights.
- The world can feel hospitable visually while the player's human status remains precarious socially.

## 6. Composition and UI

- Ordinary player view must communicate **the place, taxi, route, passenger and actionable choices** first.
- Debug labels, event logs and simulation counters must not dominate player mode.
- Text should be legible and not obscure beautiful street scenery or characters.
- Expressions and dialogue choices should be comfortable to read while traveling.
- Space for quiet/scenery is intentional; do not cover most of the viewport with panels whenever a passenger speaks.
- The exact portrait positions, icons, windowing and HUD are **not locked** by older Godot or Babylon screens.
- Arabic/RTL capable type layout is an important consideration; typography and language readability take precedence over decorative alien lettering in player controls.

## 7. Production technique boundaries

**Current engine decision:** Unity 6, C#, 2D Isometric. Build authored ground/street modules using Unity's real 2D environment/Tilemap workflow where suitable. Use authored static sprites/GameObjects for unique architecture and elevated elements. Characters and cars should use native SpriteRenderer/animation workflows where practical.

**Do not:**

- Recreate a complete city renderer from hand-drawn polygons every frame just because the technical prototype once did so.
- Decide a 3D asset pipeline is mandatory from a historic 3D-oriented visual board.
- Demand an animation/puppet framework for every NPC before the core game can function.
- Treat one generated concept sheet as optimized game-ready assets.
- Render an entire complex offscreen city's worth of actors just to suggest a living world.
- Declare the visual style final merely because one screenshot looks appealing.

**Expected art review:** Compare alternatives at the same view angle, scale, lighting and representative scene before choosing production sprite/texture/layout specifications. Observe actual Unity builds, not conceptual mockups alone.

## 8. Open art decisions and what not to infer

| Question | State |
| --- | --- |
| In-world character sprite style, pixel density, resolution | OPEN |
| Portrait art direction, animation and pipeline | OPEN |
| Exact 2D isometric camera angle / zoom / occlusion technique | OPEN |
| Final map shape, district names, density and **extent** | OPEN; physical ground cell and baseline road widths are approved by P-031 |
| Day/night and weather depth | OPEN |
| Final in-game UI layout and typography | OPEN |
| Whether the original reference images can be versioned in the repository | OPEN |
| Audio soundscape, broadcast music and alien linguistic sound design | OPEN |

**Rule for art agents:** If a specific pixel ratio, palette, character style, building kit size or animation system isn't stated as APPROVED here or in a newer owner decision, propose options labeled as proposals. Do not silently lock the project.
