# Outsider Driver — Canonical Game Design & Game DNA

**Status:** Canonical product-intent specification for the clean Unity project.  
**Decision cutoff:** 2026-10-28.  
**Authority:** The owner's latest explicit decisions in the October 2026 project discussion supersede conflicting design or implementation choices in both prior repositories.  
**Companions:** [Decision Register](DECISIONS_AND_OPEN_QUESTIONS.md), [Visual Direction](VISUAL_DIRECTION.md), [Engineering Principles](ENGINEERING_PRINCIPLES.md), and root [AGENTS.md](../AGENTS.md).

> **Read this first:** This file describes product intent, not an implementation report. The Unity repository now contains production city, vehicle, road, social and ride-system foundations; a systemic playable shift is not yet verified. Never infer completed systems from a design paragraph, earlier prototype screenshots, or historical test results.
>
> **Design philosophy:** Outsider Driver is a small-in-mechanics, rich-in-experience, freeform life-in-an-alien-city game. Driving people, talking to people, possibly getting to know them, and inhabiting a beautiful, living place are enough. It is not a combat RPG, scripted mission campaign, vehicle-physics simulator, city-builder, or population-research platform. **Procedural/systemic taxi demand and freeform open-world gameplay ARE core pillars (P-034/P-035).**

## 1. One-sentence premise and player fantasy

You are a human living illegally under a cover identity in a non-human city. You make a living with an old taxi, move through unfamiliar communities, carry people to places they actually want to go, learn their languages and customs imperfectly, and decide how close to let anyone get.

**Core fantasy:** *Survive and perhaps belong somewhere you are not supposed to belong.*

The city is not organized around the protagonist. People have lives before and after a taxi ride. The player is neither a chosen savior nor the center of an overarching quest. Personal discoveries and relationships are possible, not compulsory.

### The intended experience

- A pleasant-to-inhabit, vividly designed city with its own history, social contrasts, alien residents, local sounds, street activity, and everyday conveniences and difficulties.
- Long enough journeys to look out at scenery, observe passengers, listen to a radio, and speak comfortably, without a dialogue speedrun.
- Quiet moments and silence treated as legitimate choices, not failed interaction.
- Familiarity developing organically: recognize streets; understand a local custom; encounter someone a second time; decide whether to help, trust, lie, reveal, or simply drive.
- Freedom to continue an ordinary taxi life indefinitely instead of finishing a campaign.

### Explicit non-goals

No combat. No combat progression. No quest chains or mandatory missions. No main quest or required scripted plot progression. **Procedural/systemic ride and ordinary-job opportunities ARE core gameplay (P-034); no scripted playlist.** Exact city-*geometry* generation versus authored/hybrid remains OPEN (O-030). No forced story sequence of handcrafted passengers. No full-city on-foot open world in the first scope. No dedicated hardcore manual driving simulation. No grand-strategy economics, politics, or per-citizen cognition. No giant generic AI framework, one LLM per NPC, or reliance on always-online generation. No base building or compulsory dating game.

These exclusions are product constraints, not deferred promises.

## 2. Platform and presentation decisions

The new project is **Unity 6, C#, native 2D isometric/axonometric**. Its current project version is **6000.6.0f1** as recorded in ProjectSettings/ProjectVersion.txt, and it starts from Unity's 2D/URP template. The city should be authored with appropriate Unity-native 2D tools: Isometric Tilemaps where they fit, authored static objects, SpriteRenderers, animation, and ordinary scene tooling. A 3D engine world disguised as isometric, a custom draw-everything renderer, or an imported Godot/Babylon architecture is **not** our default approach.

**Mandatory representation clarification (owner, 2026-10-24):** **This game is 2D ISOMETRIC.** Attractive images resembling 3D dioramas or perspective city renders are art direction, not authorization to create 3D game worlds or a 3D-first asset/renderer architecture. Unity-native 2D scene assets remain the basis for the city, vehicles, walking, interiors and visual layering. The exact art technique and camera constants remain undecided.

**Mandatory data-driven rule (owner, 2026-10-24):** Authored districts, locations, roads, buildings, residents and content should be **data-driven wherever practical**. Preserve stable IDs such as `district_01` separately from editable human-facing names, candidate names, metadata, lore and spatial information. District positions, boundaries, landmarks and links may be data or scene-authored spatial references with a single authority; do not hardcode provisional names as identities. The **current** spatial world is authored; procedural city-*geometry* design is not yet decided (O-030). **Runtime ride opportunities must be generated systemically** rather than being hardcoded episodes. See [World & Content Data Contract](WORLD_CONTENT_DATA_CONTRACT.md).

**Camera:** A close, readable, cinematic isometric view of the taxi and city. Smooth camera behavior should prioritize appreciating the road and understanding travel, not a distant city-builder view. The exact angle, zoom levels, framing, and UI presentation are **not finalized**.

**On foot:** The latest owner decision adds actual free walking outside the taxi. The first authorized playable area is the protagonist's **home and their own neighborhood/local area**. This does **not** authorize walkable access to every city street, arbitrary interiors everywhere, or conversion into a giant open-world action game. The home is a real place, not just a menu. A garage and selected other bounded meeting places are established concepts, but their exact first-release implementation is unresolved.

**Passenger presentation:** Distinctive, attractive, expressive characters matter. Simpler in-world small sprites, potentially pixel-art-like, are acceptable as a production direction to explore; the style is explicitly **not chosen**. More detailed portraits or conversation close-ups can coexist with small in-world character representations. The historic upper-right passenger portrait and lower-left response arrangement is a precedent, **not** a newly ratified UI lock.

## 3. The core player activities

There are four mutually reinforcing activities, rather than four giant software subsystems.

### A. Be a taxi driver

- Make yourself available for work, find someone needing transport, and choose whether to accept.
- Travel to the actual pickup when necessary; pick up a passenger; travel along a coherent route; drop them off; receive an appropriate fare and/or a social consequence.
- Drive without a moment-to-moment steering requirement. **Semi-autonomous driving** means the vehicle travels along legal connected streets, obeying a basic road and traffic model.
- The player can specify a destination and use map/route choices, waypoints, and reasonable mid-trip changes when the roads allow them. The game can offer a default route without treating GPS ownership as an absolute gate.
- A passenger may ask to avoid an area, change destination, make a stop, or express a preference. It is a **situation between people**, not a mission objective or quest checklist.
- Ride length comes from a credible, adequately scaled authored city, **not** artificially stretching a five-point graph's timer.

**Important distinction (P-034):** Passenger ride requests are **procedurally generated everyday work**, **not authored missions or a fixed sequence**. World time, passenger availability, viable pickup/dropoff sites and player access create eligible offers. Authored reusable character/dialogue content can contribute without becoming a scripted mission ladder. See [Systemic Sandbox Contract](SYSTEMIC_OPEN_WORLD_AND_RIDE_DEMAND.md).

### B. Converse — or choose not to

- Speak with passengers through context-aware authored dialogue and choices, ask questions, answer honestly, bluff or lie, ignore, change the subject, or remain quiet when supported by the conversation.
- Most conversations should reflect **who** is speaking, **what** that person knows, their mood, location, language, experience with the driver, and what just happened.
- A player is not required to exhaust dialogue trees on every trip. There is room for silence, radio, scenery, and ordinary travel.
- Travel and local life normally continue during conversation. Reaching the destination should produce a comfortable narrative transition, not silently cut off an active sentence or force rushed reading. The precise time/input comfort behavior still needs production design.
- Ink was explored extensively in both historical projects for authored branching/conditional dialogue. **Dialogue authoring in reusable, grounded form is a design requirement; adopting Ink and a particular runtime in Unity is a separate technical decision.** No old Ink/GDScript/TypeScript bridge is authoritative.

### C. Meet people and optionally form bonds

- People encountered during rides or in the neighborhood may become familiar, remember meaningful previous encounters, and appear again for reasons grounded in their ordinary lives.
- Trust, liking, affection, suspicion, and views about humans are **not one number**. Someone can appreciate the driver while disliking humans, or oppose anti-human rules while distrusting the driver personally.
- Familiarity, friendship, help, recurring acquaintance, betrayal, and optional romance are valid **authored possibilities**, not mandatory progression modes.
- Meaningful relationships can lead to ordinary off-duty interactions in realistically reachable places, initially bounded by the walkable home/neighborhood scope.
- Residents have agency. They can be unavailable, decline, react to forgotten promises, maintain other ties, or stop interacting.
- Not every customer deserves a fully authored biography. A smaller set of significant characters can be richer; ordinary residents may have modest memory and recurring identity where useful.
- **No compulsory romance, harem mechanic, social skill tree, or relationship grinding is implied** by the presence of social systems.

### D. Live in and discover the city

- Spend time at home and in the local neighborhood; observe life; optionally interact with people and a few useful places.
- Learn where you are, how local communities work, what different people believe, and which parts of the city are safe or unfamiliar.
- Enjoy the atmosphere of districts, street activity, sounds, architecture, radio and changing time of day.
- The driver makes money and spends it on ordinary living, taxi upkeep, and a small selection of useful goods. These support everyday life rather than turning the game into a management spreadsheet.
- The world does not require player intervention to continue.

## 4. The world: alien, lived-in, beautiful, not generic cyberpunk

This is **a non-human civilization**, not a contemporary human city populated by humans with alien ears. Architecture, vehicles, interfaces, language, local customs, signage, streets, domestic objects and social assumptions should suggest the civilization developed on its own terms.

The retained product identity is **colorful, warm, inviting alien retro-futurism**:

- Cheerful, expressive color and attractive artistic composition, with rich but readable environments and characters.
- Old and new technologies side by side: recognizable wear, repairs, local manufacturing generations, tactile materials, practical infrastructure and accumulated history.
- Distinct neighborhoods and wealth levels rather than a single monochrome or perpetual-night visual mood.
- Rounded, curved, layered, ringed and sometimes monumental architectural silhouettes, but **not** a universal same-shaped dome city.
- Practical **ground-contact** alien vehicles. Ordinary hovercars/flying-car traffic are not the premise.
- Authored curves, junctions, supported bridges, elevated roads and ramps. Roads that visually cross on two levels are not automatically connected.
- Surfaces can be painted or manufactured in subdued teal, ochre, terracotta, blue-gray, pale mineral colors, etc.; all-asphalt gray is not compulsory.

The user-provided references from 2026-10-23 show (1) a warm, cozy, detailed domestic isometric scene with small character sprites and expressive dialogue portraits, and (2) a production-style alien city concept with modular curved architecture, colored roads, ground vehicles, rich signage and bright district identities. These references communicate **mood, composition and design vocabulary**, not a finalized pixel-art shader, character sprite specification, camera coordinate system, exact city geometry, or requirement to create 3D assets. The reference images are **not yet versioned as assets in this repository**. See [Visual Direction](VISUAL_DIRECTION.md).

This world has serious political/social constraints on humans, but the art does not have to look grim, dusty, post-apocalyptic, or constantly threatening. Beauty and warmth can coexist with danger and inequality.

## 5. People, societies and languages

### Distinctions that must remain true

**Species is not language. Language is not culture. Culture is not community. Community is not faction.** Individuals are not stereotypes computed directly from species. Several species can speak the same language, share a neighborhood or history, disagree within one community, or have rivalries despite cultural similarity.

There can be shared official/common speech alongside other local or minority languages. **Not everyone knows or is willing to use the common language.** Social access, who speaks which language, and who is willing to accommodate an outsider are gameplay texture, not a collectible taxonomy simulator.

### Translation and learning

The latest implemented historical language design (October 16, 2026, prior Godot repository) established:

- A **basic translator with Common/Trade language already installed**.
- One additional opening equipment choice: **GPS / a Local-language module / Radio**.
- Initially three shared city languages in the historical content model: Common/Trade, Local, and Cant; their names and exact alien scripts are **working historical labels, not final lore names**.
- Language modules operate **per complete supported language**, installed vs not installed; no tedious per-word grind or universal magical translation.
- Untranslated language is represented coherently through unfamiliar alien writing/speech, with gestures or contextual clues when appropriate.
- Asking someone to speak Common is a social action that can work, fail, be refused, or cause discomfort depending on the person.
- Translation displays in the player's chosen UI language. English is not a universal in-world translation target; support for Arabic/RTL is a genuine product consideration.
- Language hardware can be acquired through actual local commerce; purchasing and installing may be separate actions.

These rules are **retained design decisions to evaluate as game content in the new Unity project**, not a requirement to port the old market, wallet implementation, event ledger, or exact prices.

### Radio and information

- Radio can provide strange/beautiful local music, talk, culture, news and traffic information.
- Many broadcasts are naturally in alien languages, and a listener may hear content without understanding it.
- A forbidden human broadcast picked up through restricted antennas/frequencies is a **possible optional future interaction**, not a mandatory launch feature.
- Playing restricted signals near passengers may create recognizable social/legal exposure if that content is ever implemented; do not turn this illustrative possibility into an entire espionage campaign.
- Radio is allowed to be enjoyable as atmosphere even when it gives no gameplay advantage.

### Optional contextual assistance

A small GPS, translator, radio and possibly optional information-assistance device can make city life easier. These are useful objects, **not technology trees**. Former projects explored AR eyewear; its reimplementation is **not automatically authorized**.

## 6. Human identity and ordinary social stealth

The player is human in a city where human presence is illegal or heavily prohibited. They survive under a cover identity and may avoid discussing origin, paperwork or other risky personal facts.

This should affect believable ordinary interactions:

- A passenger may ask a casual question the protagonist is not prepared for.
- A person might notice an inconsistent story, remember what the driver said earlier, or simply mind their own business.
- Paperwork or licensing can influence which transport work is available.
- A close friend may know the driver well without knowing their biological origin or legal status.
- A passenger's personal attitude toward humans differs from their attitude toward the specific driver.

**Keep knowledge distinctions clear:** what is actually true, what an NPC knows or believes, and what the player knows can differ. A lie is not globally known the instant it is told; rumors require plausible sources.

This is **social texture and possible consequence**, not a mandate for wanted-level meters, armed pursuits, police-combat AI, detection minigames or constant artificial pressure.

**Unresolved design:** what the protagonist looks like; how they conceal their humanity; how documents, voice, disguise or biology contribute; how serious exposure consequences become in ordinary play. Do not invent the answer.

## 7. A living city without a giant simulation

**World feeling is the objective, not simulation completeness.**

Residents should have plausible destinations, work/rest rhythms and social presence; vehicles have streets to drive on; shops and places have local context. The player's decisions may affect particular people. The city keeps going while the player sits still.

However, **there is no requirement that every visible NPC has a lifelong detailed internal economy, pathfinding brain, personal diary and independently updated behavior every frame**.

Recommended economical representation:

1. **Ambient activity:** passersby and ordinary traffic mainly exist to make nearby streets believable. Do not endow everyone with heavyweight persistence by default.
2. **Persistent residents:** a manageable number of recognizable people with location, availability, a modest routine and relevant memories.
3. **Significant people:** richer authored material and relationship continuity when the player's interactions justify it.

Those are **levels of detail**, not three unrelated NPC architectures to build. Spawn/presentation choices must be consistent with real identity and continuity where players might notice them.

### Behavior and timing

- Update major routines and availability when time changes or relevant events occur; not every cognitive rule every rendered frame.
- Nearby active actors need smooth visuals and limited real collision/avoidance.
- Distant actors can retain rough destination and arrival timing without running fully physical movement.
- **Maintain coherence across representation changes**: do not create two different-looking incompatible populations where ghost pedestrians walk through roads while real pedestrians block traffic.
- At street level, traffic must avoid obvious permanent deadlocks; right-of-way logic should be understandable and appropriately simple for the game.
- Relationship changes should derive from observable interactions, not from undisclosed random hidden rolls that force a plot.

These are proposed engineering principles, **not a demand to implement a new simulation LOD framework before ordinary gameplay works**. Favor Unity-native tools and the smallest actual feature set that satisfies the player's experience.

## 8. Driving and map design boundaries

The current city **geometry is authored**, with meaningful connected streets; future procedurally authored/generated map geometry versus hybrid remains an open design question (O-030), independent of the **approved procedural job system**. Roads can be long, curved, multi-level, and interrupted by safe junctions or service stops. Building a believable setting is part of pacing: a journey must feel long enough to converse and enjoy the view.

- **Semi-autonomous taxi:** the driver gives direction, route preference or waypoint, and the car follows a valid route.
- **Route choice:** alternative connected streets have real distance/time/context. The map need not be a full-fledged Google Maps clone.
- **Optional reroute:** a route change takes effect at a legal point (e.g., the next road junction), not through teleportation.
- **Road elevations:** overpasses only connect by authored intersections/ramps; drawing overlap is not navigation.
- **Traffic:** believable cars, legal lanes, intersections, crossings, pedestrians and local avoidance, without pretending we need a full GTA vehicle/traffic simulation.
- **Rider comfort:** don't make players race long dialogue against a ten-second journey; use genuine world scale and sensible interaction timing.
- **Exploration:** on-foot freedom at home and within the initial neighborhood does not change semi-autonomous driving into mandatory manual steering.

How exactly the player requests a pickup, chooses a route or switches from vehicle to foot is **not yet decided at the UI/control level**.

## 9. Fares, equipment, everyday constraints

The protagonist needs an income and has limited access to official work because of their residency/identity.

- Street pickups may be more accessible than formal work networks. App-like official dispatch systems may provide better or faster work but can require licenses, cover credentials or registration.
- Informal or underground services may offer opportunities unavailable through regulated channels.
- Different residents have different resources and circumstances; not every customer pays equally or belongs to a special mission category.
- Fares, some taxi upkeep and purchases help ground the daily-life fantasy.
- The player can equip useful tools (translator cartridges, GPS, radio); purchases should feel like objects/services from the city rather than menus of arbitrary power upgrades.

**Not decided:** exact prices, maintenance frequency, debt, homelessness, failure states, legal enforcement mechanics, upgrade inventories, game time ratios or economy balance. No agent may invent these and describe them as approved.

## 10. Narrative/content structure: authored quality, systemic opportunity

A systemic sandbox does **not** mean procedurally generated dialogue or no authors.

- Designers can handwrite dialogue, expressions, situations, local history, recurring character content and contextual options.
- Those words should be reusable where appropriate, with the actual character/location/time/knowledge shaping what is valid.
- Taxi demand ordinarily comes from resident needs and presence, **not a mandatory prearranged chain of passenger episodes**.
- A memorable recurring character may have bespoke dialogue and special interactions without turning into a questline.
- Important events are allowed as emergent, optional consequences of ordinary life, but **do not invent a mission system** for them.
- LLM-authored live conversations are **not a requirement**. Any future optional generation must never fabricate canonical world facts or make the game depend on a cloud service.

Concrete illustrations are **examples, not mandatory initial features**:

- A passenger is quiet on one journey but recognizes the driver weeks later.
- Someone asks for a longer path because of a personal history with a neighborhood.
- The driver hears an incomprehensible local radio program and later hears a familiar phrase with a language module.
- The driver meets a former passenger outside a neighborhood shop.
- An apparently friendly resident dislikes humans; the driver may or may not discover that.

## 11. Visual and audio experience as gameplay

A pleasant city and expressive characters are not optional garnish. They are a major reason to play a game whose systems are deliberately uncomplicated.

- Readable cinematic close isometric camera and handsome multi-elevation streets.
- Distinctive alien silhouettes, warm colors, visible daily life and grounded street details.
- Aged, useful, memorable taxi; cabin presentation appropriate to conversations, not a busy dashboard.
- Clear readable UI, with passenger/scenery first and optional diagnostics excluded from ordinary player mode.
- Character dialogue presentation with expressions and personality; in-world sprites may be simpler.
- Natural ambient sounds, music/radio, occasional traffic/people and quiet.
- Accessibility considerations include text readability, UI scaling, reduced motion, input comfort and localization/RTL where supported.

**Open:** final in-world art technique (pixel art or not), character portrait rendering technology, exact camera and animation design, final UI composition, soundtrack style, day/night/weather scope. References are direction, not asset specifications.

## 12. Product scope contract and hard boundaries

**Approved now**
- Unity 6 / C# / genuine 2D isometric project; authored world and Unity-native 2D authoring.
- Human illegal resident and cover identity in a city built by non-humans.
- Freeform taxi life with semi-autonomous road travel, conversations, optional recurring relationships, a living city.
- No combat, procedural world generation, mandatory quests or main-quest progression.
- Limited on-foot free movement initially at home and in the protagonist's neighborhood.
- Bright, beautiful, warm alien retro-futurist art identity; attractive characters; in-world character art style unfinalized.
- Light economic reality, legal/informal work differences, multilingual alien residents, radio and useful travel/translation equipment at product-design level.
- Design for duration and comfort in real journeys, not fictitious timers and debugging dashboards.

**Explicitly not authorized**
- Rebuilding the old simulation architecture, event-sourcing framework, test fixtures, Godot or Babylon renderer, or old save format as the foundation.
- Deep physical simulation for every world resident/vehicle, a full procedural city, an all-purpose dynamic AI engine, an always-online narrator, automated full-game generation, or an expensive faction strategy simulation.
- City-wide on-foot open-world exploration, manual driving overhaul, police chases, combat, resource-crafting/base building, mandatory dating/romance or forced story chapters.
- An agent-generated set of permanent scope commitments not requested by the owner.

**Open decisions and conflict resolution**
The [Decision Register](DECISIONS_AND_OPEN_QUESTIONS.md) enumerates approvals, historical ideas and unresolved items. A past repository's shipped feature is not automatically approved in this clean project. A later owner correction takes precedence over earlier docs or plans. Unknown details remain **TBD** rather than guessed.

## 13. How to use this document when planning work

Before implementing any feature, an agent must be able to answer:

1. **Which approved player activity does this improve?** Driving, conversations, voluntary social connection, walkable local life, or perceived world vitality.
2. **What exact existing decision authorizes it?** Cite this specification or a newer explicit owner decision.
3. **Is the proposal a new feature?** If yes, mark it PROPOSED and obtain owner approval before making it canon.
4. **Can Unity already do the job?** Use an engine-native tool rather than a custom technical framework when practical.
5. **Does it quietly reintroduce old assumptions?** Examples: a scripted first clinic passenger, a simulation dashboard as the game, five-node trips, forced quest steps, comprehensive police mechanics, or a monolithic world owner.
6. **Will performance remain stable as time passes?** Measure real runtime and long sessions on actual targets; not just synthetic headless/test assertions.
7. **Can a player enjoy the current game even without the new idea?** Optional systems are not mandatory scope.
8. **Is a technical proof being promoted into permanent production architecture?** If so, stop and build the correct production implementation rather than accumulating features on a throwaway foundation.

The default answer to a missing requirement is **not implemented and not assumed**, not 'build something plausible.'

## 14. Historical source map (reference only)

These documents preserve rationale and content ideas, **not code-transfer obligations**:

- [Original high concept / social stealth / taxi / visual direction](https://github.com/MohamedXIV/outsider-driver/blob/main/docs/GAME_DESIGN.md)
- [Original scope and explicit exclusions](https://github.com/MohamedXIV/outsider-driver/blob/main/docs/SCOPE.md)
- [Original retro-futurist style bible](https://github.com/MohamedXIV/outsider-driver/blob/main/docs/STYLE_BIBLE.md)
- [Later systemic sandbox DNA](https://github.com/MohamedXIV/outsider-driver-2d/blob/main/docs/SANDBOX_GAME_DNA.md)
- [Later living city / language / identity / relationship / route discussion, including agreed vs proposed](https://github.com/MohamedXIV/outsider-driver-2d/blob/main/docs/LIVING_CITY_DESIGN_CONCLUSIONS.md)
- [Historical supersession register](https://github.com/MohamedXIV/outsider-driver-2d/blob/main/docs/SUPERSESSION_REGISTER.md)
- [Human playability feedback](https://github.com/MohamedXIV/outsider-driver-2d/blob/main/docs/PLAYABILITY_FEEDBACK.md)

**Crucial October 23 override:** The owner re-established an intentionally simple game—drive people, talk, perhaps know them, freely exist—and added limited on-foot exploration at home/the neighborhood. These take precedence over historical "no foot exploration" or "no open world walking at all" formulations, without authorizing city-wide free roaming. The owner explicitly wants **no code or architecture migration** from previous projects and **no temporary proof casually becoming the production foundation**.
