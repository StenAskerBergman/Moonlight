# Forestry framework plan

Design for turning island foliage from generated decoration into mutable game state that the
player, the building system, and production buildings all share.

Status: **steps 1-4 of §5 are implemented** (store, species assets, clearance re-hosted, player plant/clear
tools). Steps 5-7 - bushes and clutter, ForestryCore felling, regrowth - are still proposal, as is the
persistence delta in §3.3. `FoliageState.Stump`/`Log` are supported by the model and the renderer but no
stump or log prefabs exist yet, so a state with no authored prefab currently draws nothing.

## 1. Where things stand

`IslandFoliagePlacer` owns every tree on an island as a `Dictionary<Cell, TreeInstance>` and renders
them itself through `Graphics.DrawMeshInstanced` from `RenderFoliage`. There are no tree
GameObjects, no colliders, and no per-tree components.

Facts that shape the design:

- **Trees are derived state, not game state.** `ScatterFoliage` rebuilds the dictionary from
  `(seed, terrain cache, cell flags)` on every generation. Nothing about a tree survives a
  regeneration except as a function of the seed.
- **One tree per cell, maximum.** The dictionary is keyed by `Cell`. A bush next to a tree, or a
  log lying across two cells, cannot be represented.
- **A tree occupies no cell state.** `ScatterLand` reads `cell.isOccupied` but never sets it, so
  the building system does not know a tree is there. This is why houses can be placed on trees.
- **Removal is already wired, and is one-way.** `ScatterLand` subscribes each tree's cell:
  `cell.PlacementChanged += InvalidateCell`. `OccupyCellWithBuilding` fires that event, so
  placing a building deletes the tree. `ReleaseCell` does *not* fire it, so demolishing a
  building never brings the tree back.
- **Deposit cells are never forested.** `ScatterLand` skips any cell with `cell.isDeposit`, and
  `MapGrid` plants 1-2 `ResourceNodeType.ForestGrove` markers on plain `Land` cells. The result is
  that the "forest grove" resource marker is guaranteed to have no trees on it.
- **`forestDensity` is currently inert.** On a sampled island the terrain cache contained zero
  `Forest` and zero `Plain` samples out of 292,681 - only `Land`, water, cliff, mountain and beach.
  Every tree therefore came from `plainsTreeDensity` (0.012), and `forestDensity` (0.85) never
  applied. That is why islands read as parkland rather than forest.

### What already works

Requirement *"delete trees as a by-product of placing houses on top of them"* is **already
implemented** end to end:

```
BuildingPlacer.cs:505
  -> GridSystem.MarkCellAsOccupied
    -> Cell.OccupyCellWithBuilding      (fires PlacementChanged)
      -> IslandFoliagePlacer.InvalidateCell   (removes tree, marks patch dirty)
```

`StampPlacementController.cs:527` takes the same path. Nothing needs building here; it needs
*re-hosting* on the model below so that harvesting and player edits go through the same door.

## 2. The core problem

Everything requested - player planting, individual deletion, logger harvesting, stumps that used
to be trees - requires foliage to be **addressable, mutable, and authored**. Today it is anonymous,
rebuilt-from-seed, and removable only by cell.

The framework is therefore mostly one change with a lot of consequences:

> Split the *authority over what foliage exists* away from the *renderer that draws it*.

`IslandFoliagePlacer` becomes a view. A new `FoliageStore` becomes the model.

## 3. Proposed model

### 3.1 FoliageInstance

```
struct FoliageInstance
{
    int              Id;        // stable, unique within the island
    Vector2Int       Cell;      // owning cell coordinate
    short            SpeciesId; // index into the island's species table
    FoliageState     State;     // Sapling | Living | Dead | Stump | Log
    Vector3          LocalPos;  // MapGrid-local, not snapped to cell centre
    float            Yaw, Scale;
}
```

Position is stored per instance rather than derived, because a player-planted tree will not sit at
a generator-chosen offset, and a felled tree's stump must stay exactly where the trunk was.

`FoliageState` is what makes stumps and logs cheap: felling a tree does not delete an instance and
create another, it **transitions state on the same instance**. The stump is the tree, so it is
automatically in the right place, and regrowth is a transition back.

### 3.2 FoliageStore

One per island, living beside `IslandFoliagePlacer` on the `MapGrid`.

```
List<FoliageInstance>            instances;      // dense
Dictionary<int, int>             idToIndex;
Dictionary<Vector2Int, List<int>> cellIndex;     // spatial lookup, and building clearance
```

Public surface - deliberately small:

```
int    Add(FoliageSpecies s, Vector3 localPos, float yaw, float scale, FoliageOrigin origin);
bool   Remove(int id, FoliageRemovalCause cause);
bool   SetState(int id, FoliageState state);
int    QueryCell(Vector2Int cell, List<int> results);
int    QueryRadius(Vector3 localCentre, float radius, FoliageFilter filter, List<int> results);

event Action<FoliageInstance, FoliageOrigin>        Added;
event Action<FoliageInstance, FoliageRemovalCause>  Removed;
event Action<FoliageInstance, FoliageState>         StateChanged;
```

`FoliageRemovalCause` = `Construction | PlayerClear | Harvest | Decay | Regeneration`.

**This is the "unison" the request asks for.** Every actor - the building system, the player's
clear tool, a logger, a future storm event - goes through `Remove`. Yield accounting, VFX, audio,
and the renderer all hang off `Removed` rather than off each caller. Adding a sixth way to destroy
a tree later costs one enum value, not six integrations.

### 3.3 Generated base + authored delta

Do not promote every generated tree to authored state; 127 trees today, but the cap is 600 per
island and the intent is denser forest.

- The generator stays deterministic and produces instances with `Id = hash(seed, cellX, cellZ, slot)`.
- The store keeps a small **delta**: a set of removed generated ids, plus a list of player-added
  instances.
- Rehydrating an island = run the generator, subtract removals, add player additions.

A save file then costs a few hundred bytes per island instead of serialising every tree, and
regeneration with an unchanged seed preserves player edits. There is no save system in the project
yet (`ISaveable`/`SaveData` return nothing outside the item system), so this is forward-looking -
but the delta shape is what makes a save cheap later, and it is much harder to retrofit.

### 3.4 Species as data

One `FoliageSpecies` ScriptableObject per kind, replacing the raw `GameObject[] treePrefabs` on
`ClimateProfile`:

| Field | Purpose |
|---|---|
| `prefab`, `stumpPrefab`, `logPrefab` | visuals per state |
| `kind` | `Tree \| Bush \| DeadTree \| Stump \| Log \| Rock` |
| `scatterWeight`, `terrainAffinity`, `slopeMax`, `shoreDistanceMin` | generation rules, currently hardcoded in `ScatterLand` |
| `harvestable`, `woodYield`, `fellSeconds` | forestry |
| `regrowSeconds`, `regrowsInto` | Stump -> Sapling -> Living |
| `playerPlantable`, `plantCost` | build menu integration |

This is what makes "bushes and dead trees and random stumps and logs and whatnot" a data task
rather than a code task. It also moves the scatter rules currently baked into `ScatterLand`
(`slope > 0.4`, `shoreDistance < 3`, the Forest/Land/Plain density switch) into per-species data,
which is what you want once bushes should grow where oaks should not.

## 4. How each requirement lands

### 4.1 Player places a tree

New `ToolMode.FoliagePlacement` in `ToolModeManager` (currently
`None | BuildingPlacement | RoadPlacement | StampCapture | StampPlacement`).

The closest existing precedent is **not** the building placer - it is `CropFieldPlacer`, which
places 1x1 things on cells with a validity check and a `CanPlaceField(... out string failureReason)`
contract. Mirror it as `FoliagePlacer.CanPlant(cell, species, out reason)`, then `store.Add(...)`.

Reuse `PlacementValidityOverlay` for the red/green preview so it feels like the rest of the game.

### 4.2 Player deletes a tree individually

The hard part is hit testing 600 collider-less instanced meshes.

**Recommendation: do not add colliders.** Raycast the terrain as the game already does
(`GridSystem.GetCellAtWorldPosition`), then ask the store for instances in that cell and its 8
neighbours and take the nearest to the hit point within a species-defined pick radius.

This costs nothing per frame, needs no physics objects, and matches the grid granularity the rest
of the game already thinks in. The alternative - pooling a few capsule colliders onto instances
near the cursor - is more precise but adds a moving part for a case where "click roughly at the
trunk" is the actual interaction.

### 4.3 Buildings clear trees

Already works (§1). Re-host it: `InvalidateCell` becomes
`store.RemoveAll(cell, FoliageRemovalCause.Construction)`.

Two defects to fix while re-hosting:

1. `ReleaseCell` does not fire `PlacementChanged`, so demolition never notifies foliage. Harmless
   today, but it means "regrow on cleared land" can never work. Fire the event there too.
2. Clearance should be able to *yield* wood, since it goes through `Removed` with a cause. Anno
   pays you for clearing forest; whether Moonlight should is a design call, but the hook is free.

### 4.4 Loggers and millers

Follow `CropFarmCore`, not the deposit system.

A `ForestryCore` component on the logger building:

- Owns a work area - either a radius or player-painted cells, exactly as `CropFieldPlacer` paints
  field tiles.
- On a tick, calls `store.QueryRadius(centre, radius, filter: Living && harvestable)`.
- Picks one, runs `fellSeconds`, then `store.SetState(id, Stump)` and credits `woodYield`.
- A regrowth pass flips `Stump -> Sapling -> Living` after `regrowSeconds`.

Because felling is a *state transition*, the forest visibly thins into stumps and grows back
rather than popping out of existence - which is also where "random stumps and logs" come from for
free, as the by-product of a working logger rather than as scatter decoration.

Sustainability falls out of the numbers: if `regrowSeconds` beats the fell rate over the work area,
the logger is renewable; if not, it deforests and stalls. That is the interesting economic decision
and it needs no extra machinery.

**Note on `ForestGrove`:** the existing marker is a treeless single cell (§1) and should either be
repurposed as a *placement hint* for logger huts ("this is where forest is dense") or dropped. As
it stands it cannot mean what its name implies.

### 4.5 More foliage variety

Falls out of §3.4 once species are data. The generator gains a second scatter pass for
`Bush`/`DeadTree`/`Rock` kinds at higher density and lower cost (bushes can use the LOD2 mesh
throughout), keyed off the same terrain cache.

Worth fixing at the same time: the density problem in §1. Adding bushes will not make an island
feel forested while `forestDensity` never fires. Either the terrain generator needs to emit
`Forest` cells, or the species affinity table needs to drive density from something that actually
varies across the island.

## 5. Build order

Each step is independently shippable and leaves the game working.

1. **`FoliageStore` + `FoliageInstance`, renderer re-hosted onto it.** No gameplay change.
   `IslandFoliagePlacer` keeps drawing; it just no longer owns the data. Verifiable by regenerating
   and confirming an identical island.
2. **`FoliageSpecies` assets**, replacing `ClimateProfile.treePrefabs`; move scatter rules into
   data. Still no gameplay change.
3. **Building clearance re-hosted** through `Remove(..., Construction)`; fix `ReleaseCell`.
4. **Player plant + player clear tools** (`ToolMode.FoliagePlacement`, cell-based picking).
5. **Bushes, dead trees, rocks** as a second scatter pass.
6. **`ForestryCore`**: work area, felling, stumps, yield.
7. **Regrowth**, closing the loop.

Steps 1-3 are the framework; 4-7 are features that each cost little once it exists.

## 6. Open decisions

These are gameplay calls, not architecture, and the framework accommodates any answer:

- Does clearing trees for construction **pay** wood, or is it free?
- Is player planting **free decoration** or does it cost resources / take time?
- Do felled areas **regrow naturally**, only via a forester building, or never?
- Should dense foliage **slow or block** anything (pathing, unit movement), or stay purely visual?
- Do trees need to survive a **save/load**? The delta model assumes eventually yes.
