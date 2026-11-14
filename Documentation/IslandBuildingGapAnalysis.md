# Island building gap analysis

How a player's construction on an island connects to the generated ground today, where
that connection breaks, and what it would take to make island building matter.

Status: **analysis, 2026-11-14.** Every finding below was read from code on `GridSystemPatch`.
Nothing was run in Unity. Claims about runtime behaviour follow from the code paths cited and
should be confirmed in the editor before being acted on.

---

## 1. The question

The terrain generator produces rich, semantically meaningful ground: mountains, terraces,
beaches, bays and headlands, connected river courses with waterfalls and estuaries, deep-sea
plateaus with tabletops, rims and sand openings, plus deposit sites for mines, fisheries,
vents, seabed ore and crude oil.

The gameplay question is whether **where a player builds changes what happens**. Today the
answer is mostly no. The generator's meaning stops at the `Cell`. Placement reads only part
of it, production reads none of it, and logistics can't reach most of what gets placed.

---

## 2. The chain as it stands

```text
WorldTerrainField            world-positioned landforms (islands, plateaus)
  └─ IslandTerrainProvider   one sampling authority: height, zones, weights, rivers
       └─ MapGrid (per chunk) TerrainSample[,] → Cell[,]      + MarkDepositCells
            └─ GridSystem     snapping, footprint maths, cell lookup
                 └─ PlacementRules / BuildingChecker  "can this stand here?"
                      └─ BuildingPlacer   occupies cells, deducts cost, quay foundation
                           └─ Building + BuildingProductionController  → BuildingOutput
                                └─ WarehouseLogisticsScheduler → Truck over RoadNetwork
                                     └─ IslandResourceStorage (per Island component)
```

| Layer | Key files |
|---|---|
| World field | `Assets/Scripts/Grid System/Terrain/WorldTerrainField.cs`, `IslandTerrainProvider*.cs` |
| Chunk → cells | `Assets/Scripts/Grid System/MapGrid.cs`, `Cell.cs` |
| Grid access | `Assets/Scripts/Grid System/GridSystem.cs` |
| Placement rules | `Assets/Scripts/Main Game/Building Code/Build Placement/PlacementRules.cs`, `BuildingChecker.cs`, `PlacementInfluenceEvaluator.cs`, `PlacementValidityOverlay.cs` |
| Influence | `Assets/Scripts/Main Game/Building Code/Build Construction/InfluenceManager.cs`, `InfluenceZone.cs` |
| Roads | `Assets/Scripts/Grid System/RoadNetwork.cs`, `Build Placement/RoadPlacer.cs` |
| Production | `Build Components/B Maintance/BuildingProductionController.cs`, `BuildingOutput.cs`, `BuildingSupply.cs` |
| Logistics | `Assets/Scripts/Unit/Transport/*` |
| Catalogue | `Assets/Editor/TycoonBuildingCatalogGenerator.cs` → `Assets/Data/Construction/Building Placeholders/Tycoon/*.asset` |

---

## 3. Gap register

Severity:

- **Blocker**: breaks the current playable loop or makes the rules lie to the player.
- **Structural**: a missing seam that later depth will depend on.
- **Depth**: the ground exists but means nothing yet.

### A. "Island" means chunk, not landform (Structural)

- `MapManager.AddIsland` creates one `Island`, one `GridSystem`/`MapGrid` and one
  `InfluenceManager` per **chunk slot** (`Grid System/MapManager.cs:1429-1433`).
- The world field overhaul lets landforms span chunks (`Documentation/TerrainWorldFieldOverhaul.md`).
  The shared field also picks the highest landform per world sample.
- `TerrainSample` carries no landform identity (`Grid System/Terrain/IslandTerrainProvider.cs:63`).
  `Cell` has no landform or island id either.
- `Cell.neighbors` is built inside one chunk array only (`Grid System/Cell.cs`, `UpdateNeighbors`).

**Consequence:** one visible island split across two chunks becomes two
settlements, with:

- two influence managers, so the player must found it twice;
- two `IslandResourceStorage` pools;
- two population and fertility records;
- road graphs that cannot connect across the seam.

A landform from a neighbouring slot that pokes into this chunk is also
silently owned by this chunk's `Island`.

**Needed:**

- A landform id on every `TerrainSample` and `Cell`.
- A gameplay **Island** keyed by landform, not by chunk.
- Cross-chunk neighbour stitching for cells, at least for roads and footprints.

### B. Placement rules and cell semantics disagree (Blocker)

`PlacementRules.EvaluateCell` (`Build Placement/PlacementRules.cs:83`) is documented as *the single
definition of "can this building stand on this cell"*, but:

1. **No slope or mountain check.** `Cell.IsSlopeSuitableForBuilding` and
   `IsBlockedByMountainOrCliff` are never consulted. The overlay's evaluator *does* check them
   (`PlacementInfluenceEvaluator.cs:162-165`). As a result the player sees red on a mountain,
   clicks, and the building is accepted.
2. **Water list is incomplete.** `IsWater` (`PlacementRules.cs:~195-210`) omits `Lake`,
   `WaterFall` and `Plateau`. Land buildings are therefore legal on alpine lakes, on
   waterfalls, and on **underwater plateau tabletops**.
3. **No road check.** `isRoad` is never tested. A building can be placed over a road. The road
   stays registered in `RoadNetwork` under the building's footprint.
4. **DeepSea isn't tied to the plateau.** `AllowsWater` admits `DeepSea` on any water cell.
   `Cell.IsBuildableUnderwaterPlateau`, the one predicate that encodes the plateau design, is
   only reachable through an optional `GridRequirement`.
5. **Dead flags.** `Cell.isBlocked` (`Cell.cs:15`) is only ever set by the constructor
   default (`false`). Every `isBlocked` check in the codebase is therefore a no-op.
6. **Unused helpers.** `GridSystem.IsValidSurfaceConstructionCell` / `IsValidUnderwaterPlateauCell`
   already express the right rule and are unused by placement.

**Needed:** one predicate per *building layer* (Surface, Shore/Quay, Offshore, Plateau),
owned by `Cell`/`GridSystem`. `PlacementRules` and the overlay should both call it.

### C. The building catalogue is not wired to the ground (Blocker for meaning)

- The generator writes `buildingType` as `"Production" | "Civic" | "Field" | "Power" | "Monument" | "Road"`
  (`Assets/Editor/TycoonBuildingCatalogGenerator.cs:835`, `BuildingTypeFor`).
- `PlacementRules` and `InfluenceManager.IsHarborBuilding` compare `buildingType` against
  `BuildingEnums.BuildingType` names (`OnShore`, `OffShore`, `DeepSea`). **No catalogue building
  can ever match** those, because the string field is overloaded with two unrelated vocabularies:
  *category* and *terrain layer*.
- Every catalogue asset has `requiredNodeType: 0` and `BuildingRequirements: []`. Coal Mine, Iron
  Ore Mine, Oil Driller, Fishery and Lobster Farm can all be placed on any dry cell with no deposit,
  coast or water.
- **Footprint vs deposit scale.** Deposits are single cells (except crude oil, 3×3). The
  footprint rule requires *every* footprint cell to be a matching deposit (`PlacementRules.cs:105`
  plus the `requiredNodeType` block). Setting `requiredNodeType` on a 4×4 mine today would make
  it unplaceable. Meanwhile the deposit cell itself is reserved against everything else, so it
  ends up as a dead tile.

**Needed:**

- Split `buildingType` into `Category` (economy/UI) and `PlacementLayer` (terrain).
- Express resource needs as **"footprint must contain / touch / be within N of a site"**
  rather than "every cell is a deposit".
- Have the generator emit these per row.

### D. Roads and access (Blocker for logistics)

1. **Big buildings can never be served.** `WarehouseLogisticsScheduler.TryBuildRoadRoute`
   resolves the producer by `producer.transform.position` (`Unit/Transport/WarehouseLogisticsScheduler.cs:259`).
   That is the footprint *centre* cell. `RoadNetwork.GetAdjacentRoadCell` checks only that
   cell's neighbours. For any footprint of 3 or more cells on both axes, all eight neighbours
   are inside the footprint, so no route can ever be found. Most Tycoon producers are 3×3,
   4×3 or 4×4.
2. **Diagonal access, orthogonal graph.** Access accepts diagonal neighbours (`RoadNetwork.cs:162`)
   while the road graph is orthogonal only (`AreOrthogonal`, `:58`). A building touching a road
   only at a corner is "connected".
3. **Per-cell `requiresRoadAccess`.** `GridRequirement` runs once per footprint cell
   (`PlacementRules.cs:177`, `GridRequirement.cs:69`). With road access required, every interior
   cell must also touch a road, which is impossible for 3×3 and larger.
4. **Road terrain is narrow.** Roads are legal only on `Land` and `Beach`, plus bridges
   (`RoadPlacer.cs:105`). That excludes `Plain`, terraces classed otherwise, and the whole
   underwater plateau. Because drones are road-only, **deep-sea production can't join the
   logistics loop at all**.
5. **Roads ignore slope.** Nothing makes cliffs or steep terraces matter to roads, so the
   terrain's verticality has no routing cost.
6. **No cross-chunk roads.** This follows from gap A.

**Needed:**

- A building **access perimeter**: the cells orthogonally adjacent to its footprint, plus
  optional authored entrance cells.
- Logistics should route to any perimeter road cell.
- A road layer for plateau tabletops (seabed rails or pipelines) so the underwater economy
  can connect.

### E. Deposits are tokens, not resources (Depth)

`MapGrid.MarkDepositCells` (`Grid System/MapGrid.cs:~795-985`) has these problems:

- **Fixed per-chunk quotas.** It allows ≤3 mines, 4 fisheries, 2 vents, 3 ore and 2 groves per
  chunk, independent of landform size or climate. A two-chunk island gets double; a small
  island gets the same as a continent.
- **Generic mines.** Mines are always `ResourceNodeType.Mine`, although `MineAnchor` already
  carries a `resourceType` (`Terrain/FeatureReservationMap.cs:200`). Coal, iron, gold and
  uranium are not distinguished.
- **Corner-biased groves.** Forest groves come from a raster scan starting at (5,5) with step 3
  (`MapGrid.cs:965`), so they cluster in the chunk's −X/−Z corner rather than in real forest.
  (`ForestryFrameworkPlan.md` also notes that no `Forest` samples are produced.)
- **Unwired river nodes.** River nodes (`RiverBank`, `RiverSource`, `Waterfall`, `LakeShore`,
  `RiverMouth`) are written by `RiverArea.cs`, but no building requires them.
- **No yield model.** There is no richness, no capacity, no depletion, and no survey or
  discovery. A deposit is a boolean.

**Needed:** a `ResourceSite` record that is generated per landform rather than per chunk, with:

- type and sub-type;
- the cells it covers;
- richness;
- an optional depletion/regeneration curve;
- the terrain feature it came from (mountain anchor, river reach, plateau zone).

### F. Production ignores place (Depth, plus one bug)

- `BuildingProductionController` produces at a flat interval and never reads its cells,
  deposit, fertility, neighbours or ecology. The fallback output is `RawGravel` from nowhere
  (`:12`).
- **Bug:** `SetProductionRate` / `SetProductionCapacity` write into the shared
  `BuildingProductionData` ScriptableObject (`:66`). Changing one building changes every
  building that uses that asset, and the change persists in the editor.
- **Fertility is island-wide and not generated.** `IslandFertility.nativeFertilities` is a
  serialized list (`Crop Farm/IslandFertility.cs:14`), with no link to climate, river
  proximity or soil. Fields don't care which cell they sit on.
- **Unused condition types.** `Buildings_Within_Range`, `Ecology_*` and `Seed_Type` are declared,
  but `ConditionEvaluator` implements only `Has_Road` and returns `true` for everything else
  (`Build Construction/ConditionEvaluator.cs:9`).

### G. Ownership and territory (Structural)

- `Cell.InfluenceOwner` / `SetInfluenceOwner` (`Cell.cs:123`) exist but are **never written**.
  Territory is not materialised on cells.
- `InfluenceManager` has no notion of player. Any warehouse anywhere on the chunk makes it
  "settled" for everyone (`HasWarehouse`), and `Building.owner` is not consulted by placement.
- Influence zones are pure circles (`InfluenceZone.ContainsPoint`). Mountains, water and cliffs
  shade red in the overlay but do not bound the territory, so territory can jump a mountain
  ridge or a bay.
- The settlement spec's **support/orphan recomputation** isn't implemented. Shutdown reasons
  `UnsupportedAnchor` and `NoRoadAccess` exist in `BuildingEnums` and have status-badge icons,
  but nothing ever sets them. Only `CropFarmCore` drives real shutdown reasons.

### H. Building ↔ cell bookkeeping (Structural)

- `Building` doesn't store its footprint, its cells, or its grid. Release works by
  re-deriving the footprint and scanning (`Prefabs/Building Prefabs/House Components/Building.cs:164`),
  and logistics re-derives "where is this building" from the transform.
- `Building.cs` lives under `Assets/Prefabs/`, outside the scripts tree, which makes it easy to miss.
- **Quays are a separate occupancy layer.** The deck cells (`QuaySystem`) stay `Water`/`Beach`
  in the `Cell` grid, so a quay creates no buildable or road-legal ground. Harbor-side
  production and roads onto the dock can't exist.
- Road removal calls `SetRoad(false)`, which does not fire `Cell.PlacementChanged`
  (`Cell.cs:311`). Foliage and any other listener never learn that the cell freed up.

---

## 4. What "meaningful" could look like

The aim is a single idea: **the generated landform is the economy's input sheet.** The
player's reading of terrain should matter as much as reading a production chain.

Below are concrete mechanics, each built only on data the generator already produces.

| Mechanic | Ground data it uses | Player decision it creates |
|---|---|---|
| **Site-bound extraction**: mines, quarries, wells, fisheries and drillers must cover or touch a `ResourceSite`; output scales with richness. | Mine anchors, coastal bands, plateau zones, crude-oil footprints | Which island to settle first; fighting over rich sites |
| **Depletion and survey**: sites have finite or renewable stock, and new sites are revealed by survey buildings or ship abilities. | Same, plus fog of war | Expansion pressure, and a reason to leave the home island |
| **Terrain-graded efficiency**: fields read the soil under their cells (river-bank and floodplain bonus, terrace bonus, slope penalty). | `riverStatus`, terrace/region masks, slope metrics | Farms belong in valleys, industry on terraces |
| **Water-adjacent industry**: mills, distilleries and cooling-heavy plants need a river reach or lake shore in reach. | River reaches, `LakeShore`, `RiverMouth` | Rivers become valuable real estate |
| **Territory follows terrain**: influence fills through passable ground and stops at cliffs, open water and ridges. | `IsBlockedByMountainOrCliff`, water types | Mountains become natural borders; bridges and tunnels gain value |
| **Access as a cost**: road cost rises with slope; bridges and switchbacks become decisions; plateau layouts need seabed links. | Height and slope per cell | Logistics layout is a puzzle shaped by the island |
| **Per-cell ecology**: industry writes pollution into nearby cells, which lowers fertility and fish yield. An Eco faction counters by restoring cells. | Cell grid as a field | Faction identity tied to place, not just to recipes |
| **Adjacency/cluster bonuses**: producers in range of their supplier or service building run faster (the `Buildings_Within_Range` condition already exists). | Footprints and positions | Districts and planning, not scatter |
| **Shaping the island**: quays create buildable ground; terraforming and land reclamation are late-tier powers. | Quay deck cells, terrain types | The player's mark on the island persists and compounds |

These are candidates, not commitments. The point is that every row hangs off an existing
generator output.

---

## 5. Suggested order of work

**Phase 0: make the current rules truthful** (small, fixes blockers)

1. Unify placement onto per-layer cell predicates (slope, mountain, complete water list, road,
   plateau). The overlay and the click must share one predicate (gap B).
2. Building access perimeter. Route logistics to perimeter road cells, use orthogonal-only
   access, and evaluate `requiresRoadAccess` per building rather than per cell (gaps D1–D3).
3. Stop `SetProductionRate` writing into the shared asset (gap F).
4. Fire `PlacementChanged` on road removal (gap H).

**Phase 1: identity and data seams**

5. Landform id on samples and cells. A gameplay Island per landform owns influence, storage,
   population and fertility (gap A).
6. Split `buildingType` into `Category` + `PlacementLayer`, and teach the catalogue generator
   to emit layers and site requirements (gap C).
7. Give `Building` its footprint, cells and grid as stored state (gap H).

**Phase 2: ground drives the economy**

8. `ResourceSite` generated per landform. Mines, fisheries and oil require sites, and output
   scales with richness (gaps C, E).
9. Fertility derived from terrain and climate per cell. Fields read the cells they sit on (gap F).
10. Write `InfluenceOwner` to cells, with terrain-bounded territory and player ownership.
    Implement support recomputation and the `NoRoadAccess`/`UnsupportedAnchor` shutdowns (gap G).

**Phase 3: depth**

11. Plateau road/pipeline layer. Quays become road-legal ground.
12. Depletion, survey, per-cell ecology and adjacency conditions (section 4).

---

## 6. Open questions

These need a design decision before Phase 1–2 work:

- **Island identity.** Should one gameplay island always equal one landform, even when two
  landforms touch? Or can merged coasts form one settlement?
- **Multiplayer territory.** Can two players share an island, as in a classic island-builder?
  Or is the first harbor exclusive?
- **Deposit scale.** Should extraction buildings *contain* the site, *touch* it, or *work it
  from range* (as a fishery works water)? Different families may want different rules.
- **Depletion.** Should sites be finite, renewable, or tiered (renewable fish, finite ore)?
- **Underwater logistics.** Should plateau goods travel by seabed road, by pipeline, or only by
  submarine/ship to a surface depot?
- **Territory shape.** Should territory stay a circle with blocked cells shaded red, or become
  a terrain-bounded flood fill?
