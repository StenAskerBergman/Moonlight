# Moonlight

Moonlight is a Unity RTS and island-economy prototype built around procedural
archipelagos, settlement construction, road logistics, shared island storage,
and multi-domain navigation.

## Current playable goal

The first vertical slice is a 10–15 minute loop:

1. Generate a deterministic island map.
2. Establish a settlement and warehouse/depot influence area.
3. Place roads and one producer with no input requirement.
4. Let the producer accumulate one resource in local output storage.
5. Watch a warehouse-owned road drone collect the output.
6. Verify that the cargo reaches the island's shared storage.

The shared stockpile is currently inspectable through `IslandResourceStorage`.
A player-facing stockpile UI is deliberately reserved for a later slice.

See [Playable Loop](Documentation/PlayableLoop.md) for rules and acceptance criteria.

## Opening the project

- Unity: **2022.3.62f3**
- Render pipeline: Universal Render Pipeline 14
- Start scene: `Assets/Scenes/Launcher/MainMenu.unity`
- Direct gameplay scene: `Assets/Scenes/Match.unity`
- Terrain development scene: `Assets/Scenes/Grid Testing.unity`

Open the repository folder in Unity Hub. The normal flow is Main Menu → Lobby →
Loading → Match.

## Major modules

- `Assets/Scripts/Grid System` — map layout, procedural terrain, mesh/texture
  generation, roads, and stacked navigation.
- `Assets/Scripts/Main Game/Building Code` — placement, influence, construction,
  production, and local building output.
- `Assets/Scripts/Unit/Transport` — warehouse assignment, pickup jobs, logistics
  drones, and shared island resource delivery.
- `Assets/Scripts/Item System` — general item inventories and storage.
- `Assets/Scripts/Unit` — selection, movement, and navigation behaviors.

## Terrain acceptance gate

Before expanding the economy slice, validate at least ten deterministic seeds:

- no visible height, normal, or texture-splat seams at chunk edges or corners;
- coherent island, coast, river, mountain, and plateau forms;
- usable settlement terrain and shoreline access;
- identical results for identical seeds;
- total generation respects the Inspector-configured timeout (15 seconds by
  default), with partial objects removed after failure.

Visual quality must be checked in Unity; compilation alone does not prove that
generated islands look or behave correctly.

## Development status

Terrain generation and navigation are the most developed systems. Building and
inventory foundations exist, while the warehouse output-pickup slice is the
current integration target. Producer-owned input collectors, airborne priority
pickup, and the stockpile UI are follow-up slices.

Imported NavMesh Components documentation remains under `Documentation/` for
reference, but the project uses Unity's AI Navigation package for current work.
