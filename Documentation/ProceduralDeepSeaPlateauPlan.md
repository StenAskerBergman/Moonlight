# Procedural Deep-Sea Plateau Plan

## Goal

Generate deterministic submerged plateaus that read correctly at player scale: a broad sandy tabletop, a rocky non-buildable rim, a sharp escarpment, sparse sediment openings, occasional tall formations, and an abyssal surround. The generator must support a catalogue of silhouettes without letting decorative noise fragment the buildable core.

## Current generation seam

`MapManager` resolves a pattern slot and spawns a terrain chunk. `MapGrid` owns the generation lifecycle and converts samples into gameplay `Cell` data. `IslandTerrainProvider` is the terrain module: one sampling interface produces geometry, semantic plateau zones, material weights, and buildability. `TerrainSampleCache` lets gameplay, the dense mesh, textures, and diagnostics consume the same samples.

The current plateau pass includes:

- deterministic `Rounded`, `Elongated`, `Crescent`, and `TwinLobed` catalogue shapes, plus `Auto` selection;
- a connected low-relief tabletop whose perimeter is deformed without allowing fine noise to punch holes through the core;
- rocky rim outcrops and sparse seed-driven spires;
- an upper escarpment, lower apron, optional sand openings, and abyss fade;
- authoritative `Tabletop`, `RockyRim`, `SandSlope`, `UpperEscarpment`, `LowerApron`, and `AbyssFade` zones;
- continuous sand, rock, reef, abyss, and buildability weights consumed by texture and gameplay code;
- a plateau-zone diagnostic view and a control-map capture that no longer classifies every submerged texel as water.

## Procedural field stack

Keep these fields ordered. Later fields may decorate or locally perturb earlier results, but may not redefine the footprint or buildability.

1. **World seed and chunk seed** — world seed keeps shared geology stable; chunk seed selects a repeatable catalogue realization.
2. **Catalogue shape** — ellipse, bend, or lobe deformation establishes the macro silhouette and elevation-profile family.
3. **Domain warp** — low-frequency bounded displacement removes geometric symmetry.
4. **Perimeter field** — broad and detail noise alter the outline while the central tabletop remains connected.
5. **Profile field** — signed distance from the perimeter drives tabletop, escarpment, apron, and abyss elevations.
6. **Rock field** — fracture noise, outcrop masks, and sparse spire anchors affect only the rocky rim and escarpment.
7. **Sediment field** — one or more widening sand openings interrupt the rim without cutting the tabletop into islands.
8. **Tabletop micro-relief** — centimetre-scale low-amplitude undulation breaks visual flatness while staying under the construction slope tolerance.
9. **Dressing field** — blue-noise/Poisson placement scatters grass, coral, rubble, vents, and decals after terrain semantics are final.

## Data and render channels

CPU terrain samples should continue to expose these independent channels:

| Channel | Consumer | Meaning |
|---|---|---|
| Height | mesh, physics, navigation | Authoritative surface elevation |
| Slope | placement, material detail | Local physical gradient |
| Zone | diagnostics, dressing rules | Discrete plateau region |
| Buildability | construction grid | Safe tabletop interior only |
| Sand weight | material, decals | Sandy tabletop and sediment openings |
| Rock weight | material, dressing | Rim, escarpment, and apron rock |
| Reef weight | dressing, tint, VFX | Sparse biologically suitable hard surface |
| Abyss fade | material, fog, dressing | Transition out of the authored formation |
| Vent influence | future audio/VFX/gameplay | Distance and strength around vent anchors |
| Edge danger | future feedback | Distance to the non-buildable rim/drop |

The existing reference control convention remains `R = grass/mainland`, `G = sand`, `B = rock`, and black RGB for water/abyss. Plateau captures therefore use green for tabletop/sand openings, blue for rock, and black for the abyss. This is diagnostic data; the active `Base.mat` path currently receives a baked albedo texture through `_BaseMap`.

## Key shader and texture work

Add a dedicated `UnderwaterPlateau` URP material rather than expanding the baked-color path indefinitely.

- Use one RGBA control texture: `R = sand`, `G = rock`, `B = reef/biogrowth`, `A = abyss/silt`.
- Use world-space triplanar mapping on steep escarpments so vertical faces do not smear UVs.
- Blend by semantic weights first and slope second; never infer the submerged tabletop from absolute height.
- Add a fine sand normal/roughness pair, stratified rock albedo/normal/height, darker abyssal silt, and a sparse reef/coral atlas.
- Apply underwater caustics in world space, with depth and normal rejection so the effect is strongest on the tabletop and upper rock faces.
- Add distance fog, suspended particulate, and subtle color absorption as camera-level underwater effects rather than baking them into terrain albedo.
- Reserve vertex colors or a second mask for wet sediment, fracture accent, vent heat, and construction feedback if the RGBA control texture becomes saturated.

## Dressing, VFX, and key scripts

Add these modules only when their assets exist; terrain generation should emit placement data without knowing prefab identities.

- `PlateauDressingProfile` (`ScriptableObject`) — prefab sets, densities, scale ranges, exclusion radii, and zone eligibility.
- `PlateauDressingPlacer` — deterministic blue-noise placement for seagrass, coral, rubble, and loose rocks using zone/slope/buildability weights.
- `PlateauFormationPlacer` — optional authored rock/vent prefabs aligned to sparse spire and vent anchors when heightfield formations are not visually sufficient.
- `PlateauVentField` — emits vent anchors and continuous heat/influence values for resources, particles, light, and audio.
- `PlateauEdgeFeedback` — exposes edge distance to construction previews, unit path warnings, sonar pulses, and camera effects.
- `PlateauAudioZone` — mixes plateau ambience from semantic proximity rather than one trigger volume around the whole chunk.

Visual dressing order: embedded rock formations, sediment decals, reef/coral clusters, seagrass patches, rubble, vent VFX, then transient particles. Every pass must respect construction clearances and deposit footprints.

## Audio cues

Use continuous layers for location and short cues for decisions.

- **Approach feed-forward:** distant low-frequency rock resonance and sparse sonar returns rise as the player approaches plateau influence.
- **Tabletop confirmation:** soften deep-current rumble, add sand movement and light reef crackle when the camera crosses onto the tabletop.
- **Edge danger feed-forward:** increase current hiss, falling debris, and a directional low-pass drop before the camera or placement cursor reaches the escarpment.
- **Vent feedback:** positional bubbling, mineral crackle, intermittent pressure releases, particles, and local light communicate resource strength.
- **Construction feedback:** a restrained positive pulse for valid tabletop placement; a dry muted rejection cue for rim, slope, or abyss cells.
- **Depth transition:** crossfade ambience by camera depth and abyss fade; do not restart loops at terrain-chunk boundaries.

## Player feedback and visual cues

- Keep the sandy interior broad and quiet so the player reads it as usable space before enabling a build overlay.
- Use the rocky rim, coral density, debris direction, and stronger caustic breakup as natural feed-forward for the drop.
- During construction mode, outline the accepted tabletop boundary and tint invalid rim/escarpment cells without replacing the underlying material read.
- Use a short sonar sweep to reveal buildability, vent influence, channels, and edge danger when the terrain is visually obscured.
- Place tall spires mainly on the rim so they silhouette the plateau without consuming the buildable centre.
- Keep resource markers and vent particles visible from the tabletop camera height, not only from the catalogue/top-down view.

## Validation sequence

1. Compile after terrain code changes.
2. In Unity, regenerate at least one seed of every explicit catalogue shape and several `Auto` seeds.
3. Compare top view for connected footprint/buildable area, side view for sharp drop/profile, control map for sand-rock-abyss zoning, and gameplay overlay for rim exclusion.
4. Check the camera on or just above the tabletop, including water fog and caustics; an orthographic catalogue capture is not sufficient.
5. Confirm deposits, navigation, cursor raycasts, and construction placement on the tabletop and rejection on rim/escarpment cells.
6. Compare per-stage timings with `Temp/TerrainGenerationReferences/Latest/manifest.json`; keep visual sampling at the lowest density that preserves silhouette and spire quality.

Passing compilation is only the handoff gate. Visual quality, water interaction, and player-scale readability require the Unity checks above.
