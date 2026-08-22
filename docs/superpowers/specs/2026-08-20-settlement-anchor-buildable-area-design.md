# Settlement Anchor & Buildable Area — Design

**Date:** 2026-08-20
**Branch:** `GridSystemPatch`
**Status:** Approved, ready for implementation planning

---

## 1. Purpose

Establish the first spatial building pattern in Moonlight: buildings are not
arbitrary independent objects. They are placed into an island build-area network
rooted at a coastal anchor.

This spec covers **the anchor and the buildable area only**.

### In scope

- A settlement anchor (Coastal Warehouse) placeable on virgin coast
- An influence zone projected by that anchor, defining where construction is legal
- Placement gating against terrain, occupancy, and area coverage
- Zone lifecycle: registration, unregistration, demolition, load-time restoration
- Support (orphan) recomputation when zones change
- Cell release so demolished footprints become buildable again
- Editor gizmos and debug output sufficient to verify all of the above

### Out of scope

Roads and connectivity · population and unlock thresholds · production chains and
goods routing · ship cargo unloading and the founding transaction · any broader
economy change.

`BuildingActive` carries supported/unsupported state only in this slice. It gates
nothing. Production shutdown is deliberately deferred.

Runtime player-facing rendering of the influence area is **optional** and not
required for this slice to be considered complete.

---

## 2. Existing state

Substantial work already exists in the `GridSystemPatch` working tree. This spec
hardens and completes it rather than designing from scratch.

### Already working

| Area | State |
|---|---|
| `GridSystem.GenerateGrid()` | Bridges `MapGrid.Grid`; cells are populated |
| `MapGrid` | Exposes `public Cell[,] Grid` and `public int Size` |
| `MapGrid.ApplyBeachEdges()` | Produces `TerrainType.Beach` for Land cells orthogonally adjacent to Water — this is the queryable "coast" |
| `BuildingChecker.InputCheck()` | Called from `Update()`; click-to-place is live |
| `BuildingPlacer` | Affordability checked before instantiation, reading `BuildingCost` off the prefab |
| `BuildingPlacer.DeductCosts` | Takes `currentBaseStorageManager` — correct wallet |
| `GridSystem.GetCellAtPosition` | No longer mutates `buildingChecker.canPlace` |
| `InfluenceZone` | Radius, zone type, `ContainsPoint` |
| `InfluenceManager` | `HasWarehouse`, `IsWithinBuildableArea`, `CanPlaceWarehouse`, register/unregister |
| `MapManager.AddIsland` | Attaches an `InfluenceManager` per island |

### Gaps this spec closes

1. **Anchor identity conflated with terrain.** `isWarehouse` is tested as
   `buildingType == OnShore`, so any future shore building (fishery, pier,
   shipyard) would count as a settlement anchor and could found a new area on
   untouched coast.
2. **Zones never unregister.** `UnregisterZone()` has zero callers.
   `BuildingDestroyer.OnDestroyBuilding()` only calls `Destroy()`. Demolishing an
   anchor leaves its influence permanently. Nothing restores zones on load.
3. **Area test checks a single point.** `IsWithinBuildableArea(newPos)` tests the
   building origin, not its footprint, so a large building can straddle the border.
4. **No inverse for cell occupancy.** `Cell.occupyingBuilding` is `private set`
   with only `OccupyCellWithBuilding()`. Demolished footprints stay occupied forever.
5. **No anchor prefab exists.** Tier 1 contains only `City Center`,
   `Gravel Extracter`, `Worker Resident`.
6. **Inconsistent manager access.** `BuildingChecker` resolves via
   `currentIsland.islandObject`; `BuildingPlacer` via `islandTransform`.

Gap 4 is not a spatial defect but is forced by the chosen orphan policy: players
will demolish an anchor and rebuild nearby to restore support, and that loop is
impossible if demolished footprints never free.

---

## 3. Responsibility boundary

`BuildingChecker` remains the **combiner** that reaches a verdict.
`InfluenceManager` is a **state owner** answering one narrow question.

```
BuildingChecker   (combiner → canPlace)
  ├── terrain legality              (buildingData.allowedTerrain)
  ├── footprint occupancy           (GridSystem / Cell)
  ├── area legality                 ──asks──▶ InfluenceManager
  └── building requirements         (BuildingRequirements.Verify)

InfluenceManager  (state owner)
  ├── anchor state                  (HasAnchor)
  ├── registered zones              (register / unregister)
  ├── containment                   (point + footprint)
  └── support recomputation         (on zone change)
```

`InfluenceManager` must not acquire terrain or occupancy concerns. Keeping it to
zone state is what makes it testable without a scene.

---

## 4. Components

| Component | Responsibility | Depends on | Status |
|---|---|---|---|
| `InfluenceZone` | Declares: I project a buildable area of radius R, of this zone type, and I may or may not found a settlement | `RequirementEnums` | Exists — add `canFoundSettlement`, `Register()`, `OnDestroy`, cached manager |
| `InfluenceManager` | Per-island zone registry, anchor state, containment queries, support recomputation | `InfluenceZone` | Exists — add footprint test, anchor rules, `RecomputeSupport`, load-time restore |
| `BuildingActive` | Holds one building's supported/unsupported state | — | Exists as empty stub — implement |
| `BuildingFootprint` | Records the exact cell indices a building claimed; releases them on destroy | `GridSystem`, `Cell` | New |
| `BuildingData.allowedTerrain` | Data-driven replacement for the hardcoded `OnShore → Beach` rule | `Cell.TerrainType` | New field |
| `Cell.ReleaseCell()` | Inverse of `OccupyCellWithBuilding` | — | New |
| `Island.GetInfluenceManager()` | Single access path, lazily creating if absent | `InfluenceManager` | New |

### 4.1 Anchor identity

```
emits a zone      ⟺ prefab has an InfluenceZone component
may found a town  ⟺ that zone has canFoundSettlement == true
```

`InfluenceManager.HasWarehouse` is renamed `HasAnchor` and returns true if any
registered zone has `canFoundSettlement`. The `buildingType == OnShore` test is
**deleted** from `BuildingChecker`. Nothing in placement reads
`BuildingEnums.BuildingType` afterwards.

Rationale for putting the flag on `InfluenceZone` rather than `BuildingData`:
zone emission and anchor capability are the same concern, and keeping them on one
component means there is a single source of truth on the prefab with nothing to
keep in sync.

### 4.2 Terrain rules move to data

`BuildingData` gains:

```csharp
public List<Cell.TerrainType> allowedTerrain = new List<Cell.TerrainType>();
```

An empty list means no terrain constraint. The Coastal Warehouse gets `[Beach]`.
This is what actually decouples *where a building sits* from *what it does*.

---

## 5. Placement flow

Per frame, in `BuildingChecker.UpdateBuildsite()`. Every step writes to a **local**
`bool place`, assigned to `canPlace` exactly once at the end. The current
multi-writer pattern on that field is the reason its behaviour was hard to follow.

```
1. Raycast mouse → groundLayer                          → miss: place = false, return
2. newPos = gridSystem.GetNearestPointOnGrid(hit.point)
3. origin = gridSystem.GetCellAtWorldPosition(newPos)    → null: place = false, return
4. footprint = cells from WorldToCell(newPos) over buildingSize.x × buildingSize.z

5. OCCUPANCY   every footprint cell: non-null && !isBlocked && !isOccupied
6. TERRAIN     if buildingData.allowedTerrain is non-empty:
                  every footprint cell's currentTerrainType ∈ allowedTerrain
7. AREA        mgr  = currentIsland.GetInfluenceManager()
               zone = previewPrefab.GetComponent<InfluenceZone>()

               if (zone != null && zone.canFoundSettlement && !mgr.HasAnchor)
                     → FOUNDING: skip area test
                       (steps 5–6 already proved legal, unoccupied coast)
               else  → mgr.ContainsFootprint(newPos, buildingSize, gridSystem)

8. place = 5 && 6 && 7
9. canPlace = place
```

`isVerified` (`BuildingRequirements.Verify()`) stays where the current patch puts
it — in `InputCheck()`, evaluated on click rather than per frame.

### 5.1 Anchor founding rules

| Situation | Rule |
|---|---|
| `!HasAnchor`, prefab zone has `canFoundSettlement` | Placeable on any cell satisfying `allowedTerrain` (Beach). No area constraint |
| `!HasAnchor`, prefab has no anchor zone | Nothing is placeable — non-anchor buildings require coverage and none exists |
| `HasAnchor`, any building including further anchors | Must satisfy `ContainsFootprint` |
| All anchors destroyed → `HasAnchor` false | Island returns to virgin state and may be re-founded on any beach |

The last row is the recovery path. It is deliberate: it is what makes "disabled
until re-covered" survivable rather than producing a permanently dead island.

---

## 6. Lifecycle

### 6.1 Registration

Registration is owned by the zone, not the placer, so every creation path behaves
identically:

```csharp
// InfluenceZone
public void Register(InfluenceManager mgr) {
    _mgr = mgr;              // cached for OnDestroy
    mgr.RegisterZone(this);
}
```

`BuildingPlacer.PlaceBuilding()` calls `zone.Register(mgr)` after
`MarkGridCells()`. `InfluenceManager.RegisterZone()` is **idempotent** (guards
against duplicates) and ends by calling `RecomputeSupport()`.

### 6.2 Demolition

```csharp
// InfluenceZone
void OnDestroy() {
    if (_isQuitting || _mgr == null) return;
    _mgr.UnregisterZone(this);      // → RecomputeSupport()
}
```

Hanging unregistration on `OnDestroy` rather than on `BuildingDestroyer` means
**every** destruction path unregisters — player demolition, scripted removal,
editor undo. `BuildingDestroyer.OnDestroyBuilding()` keeps its existing bank-return
logic unchanged and needs no knowledge of influence.

`_isQuitting` is a **static** flag on `InfluenceZone`, set from
`OnApplicationQuit()`. It must be static because every zone instance needs it
during the same teardown, and instance state is unreliable at that point. The
guard is required because Unity tears down components in arbitrary order; without
it, scene exit throws when zones outlive their manager.

### 6.3 Cell release

```csharp
// Cell
public void ReleaseCell() {
    occupyingBuilding = null;
    currentStatus = CellStatus.Empty;
}
```

plus `GridSystem.MarkCellAsFree(Vector3 worldPos)`.

`BuildingFootprint` is added to the building instance at placement, stores the
exact `List<Vector2Int>` cell indices claimed by `MarkGridCells()`, and releases
them in its own `OnDestroy`.

**Alternative considered and rejected:** scanning the grid at demolition for cells
whose `occupyingBuilding == this`. It needs no extra state, but it is
O(gridSize²) per demolition and — more importantly — recomputing a footprint from
position and size is fragile once rotation or any transform nudge is involved.
Storing what was actually claimed is exact and cheap.

### 6.4 Support recomputation

```csharp
// InfluenceManager — called only from RegisterZone / UnregisterZone
public void RecomputeSupport() {
    foreach (BuildingActive b in EnumerateIslandBuildings()) {
        BuildingFootprint fp = b.GetComponent<BuildingFootprint>();
        if (fp == null) continue;          // no recorded footprint → leave state untouched
        b.SetSupported(CoversPoints(fp.WorldPoints(_grid)));
    }
}
```

`EnumerateIslandBuildings()` returns `BuildingActive` components. A building
carrying `BuildingActive` but no `BuildingFootprint` is skipped rather than
defaulted, so a wiring mistake on a prefab cannot silently mark a building
unsupported.

Event-driven, never polled. Zones change on placement and demolition only, so
polling would be pure waste. Cost is O(buildings × zones), paid only at those two
moments.

An anchor is covered by its own zone — its footprint sits at the zone centre — so
it requires no special case.

### 6.5 Re-coverage

No separate code path. Placing a zone that covers orphaned buildings fires
`RegisterZone → RecomputeSupport → SetSupported(true)`. This symmetry is the main
reason to prefer full recomputation over incremental bookkeeping.

### 6.6 Load-time restoration and building ownership

```csharp
// InfluenceManager
void Start() {
    foreach (var z in EnumerateIslandZones()) z.Register(this);
    RecomputeSupport();
}
```

**Documented invariant.** `EnumerateIslandZones()` and
`EnumerateIslandBuildings()` are implemented with
`GetComponentsInChildren<T>(true)` on the island GameObject, and this is correct
**only under the following invariant**:

> All buildings belonging to an island are instantiated as descendants of that
> island's GameObject.

This invariant currently holds for the placement path and is verified:

- `MapManager.AddIsland()` instantiates `islandPrefab` as a single `islandGO`
  carrying `Island`, `GridSystem`, `MapGrid` and `InfluenceManager` on the **same**
  GameObject.
- `BuildingPreview` parents itself to `currentIsland.transform`.
- `BuildingPlacer.InstantiateBuilding()` passes that same transform as the parent,
  so placed buildings are direct children of `islandGO`.
- `Island.GetPlayerBaseInventory()` and `Island.GetBaseStorageManagerForID()`
  already iterate `foreach (Transform child in transform)`, so the codebase
  already relies on this ownership model.

It is **not** guaranteed by construction for hand-authored scene buildings placed
under a different parent.

**Therefore:** `InfluenceManager.RegisterZone()` asserts that the incoming zone's
transform is a descendant of the island and logs a warning otherwise. This
surfaces any violation immediately instead of silently dropping a zone.

**Future canonical path.** `Island.buildings` (a `List<Building>` assigned from
`IslandData` in `MapManager`) is the natural authoritative ownership registry, but
nothing currently appends to it at placement time. Populating it and switching
enumeration to use it is the correct long-term fix. It is **out of scope here**
because it touches building registration broadly; the invariant plus assertion is
the minimal correct approach for this slice.

### 6.7 Manager access unification

Add `Island.GetInfluenceManager()`, returning a cached reference and lazily adding
the component if absent. Both `BuildingChecker` and `BuildingPlacer` use it.

This is not cosmetic. Today `BuildingChecker` resolves via
`currentIsland.islandObject` and `BuildingPlacer` via `islandTransform` — two
paths to what is usually, but not always, the same object. `MapManager` calls
`Destroy(island.islandObject)` in its flat-land replacement branch, and it only
calls `AddComponent<InfluenceManager>()` inside `AddIsland()`, so a scene-authored
island has none at all. Resolving off the `Island` component with a lazy create
makes both callers correct in every path.

---

## 7. Prefab wiring

Create `Coastal Warehouse.prefab` in
`Assets/Prefabs/Building Prefabs/Faction Prefabs/Tycoon Faction/Universal Tier 1/`.

| Component | Configuration |
|---|---|
| `BuildingProperties` | References a new `Coastal Warehouse` `BuildingData` asset |
| `BuildingData` (asset) | `buildingSize` 3×3; `allowedTerrain = [Beach]`; requirements list empty |
| `BuildingCost` | References a new `CostData` asset (items + price) |
| `InfluenceZone` | `canFoundSettlement = true`, `zoneType = DepotZone`, `radius ≈ 20` |
| `BuildingActive` | Defaults to supported |
| `BuildingFootprint` | Populated at placement |

Plus a `BuildingButton` in the build menu bound to it.

Construction cost continues to come from `BaseStorageManager` exactly as it does
today. Ship unloading is out of scope.

**Second prefab for coverage testing:** reuse `Worker Resident`, adding
`BuildingActive` and `BuildingFootprint`, with **no** `InfluenceZone`.

---

## 8. Visualisation and debugging

### 8.1 Required — editor gizmos

- `InfluenceZone.OnDrawGizmosSelected()` — wire circle at `radius`, tinted by
  `canFoundSettlement`
- `InfluenceManager.OnDrawGizmos()` — all registered zones, plus a marker on each
  unsupported building

### 8.2 Required — debug output

`DebugConsole.cs` (already modified in the working tree) gains a command dumping:
zone count, `HasAnchor`, and each building's supported state.

### 8.3 Optional — runtime overlay

**Not required for this slice.** If pursued later, shown only while a preview is
active:

| Option | Trade-off |
|---|---|
| Projector/decal per zone | Simplest; overlapping zones double-darken |
| Terrain shader fed zone centres and radii | Correct union; more work |
| Per-cell tint on the cell mesh | Matches grid exactly; heaviest |

The projector approach is the natural first attempt, behind a toggle.

---

## 9. Testing

Edit-mode tests live in `Assets/Tests/Editor/` **without an asmdef**, so they
compile into `Assembly-CSharp-Editor` and can see game code. The existing
`NavMeshComponentsTestsEditmode.asmdef` references only `NavMeshComponents` and
cannot see `Assembly-CSharp`, so it must not be reused.

### 9.1 `InfluenceManager`

1. No zones → `IsWithinBuildableArea` false, `HasAnchor` false
2. Register non-anchor zone → `HasAnchor` still false
3. Register anchor zone → `HasAnchor` true
4. Point inside radius → true; outside → false
5. Footprint fully inside → true; partially outside → false *(gap 3 regression test)*
6. Unregister last anchor → `HasAnchor` false
7. `RecomputeSupport`: covered building supported; after unregister, unsupported
8. Re-coverage: registering a covering zone re-supports an orphan
9. `RegisterZone` called twice is idempotent

### 9.2 `Cell` / `GridSystem`

10. `MarkCellAsOccupied` → `MarkCellAsFree` → cell reports empty and is placeable again

### 9.3 Testability consequence

To keep tests 4–5 free of scene setup, zone-containment maths must be reachable
without a live `GridSystem`.

Zones are defined in world space (`Center`, `radius`), while footprints are stored
as cell indices, so containment cannot be evaluated from indices alone — the
index → world mapping needs the grid. The split is therefore drawn at the
world-space boundary:

- `CoversPoints(IEnumerable<Vector3> worldPoints)` — pure; depends only on the
  registered zones. Tested directly by passing world points, no grid required.
- `ContainsFootprint(Vector3 origin, Vector3 size, GridSystem grid)` — thin
  wrapper resolving origin + size → cell indices → world points, then delegating
  to `CoversPoints`.
- `BuildingFootprint.WorldPoints(GridSystem grid)` — converts its stored cell
  indices to world points using the same mapping.

`BuildingFootprint` stores cell **indices** (needed for exact release in §6.3) and
converts to world points on demand, so there is one mapping and no duplicated
state that could drift.

### 9.4 Manual verification

1. Island generates → no zones; every preview red, warehouse included, except on beach
2. Place warehouse on beach → area appears in gizmos
3. House inside area → green and placeable; outside → red
4. Demolish warehouse → house flips to unsupported; its cells free
5. Place a new warehouse covering the house → supported again
6. Re-place a building on the demolished warehouse's old cells → succeeds

---

## 10. Decision log

| Decision | Choice | Rationale |
|---|---|---|
| Spec scope | Anchor + buildable area only | Roads, population and production all query this area service; it must exist first |
| Cell substrate | `MapGrid.Grid` via `GridSystem.GenerateGrid()` | Already implemented and producing `Beach` cells |
| Spec aim | Harden and wire existing code | The spatial half is roughly 80% built; this is completion, not invention |
| Orphan policy | Disabled until re-covered | Makes the anchor genuinely load-bearing; fits the existing `BuildingActive` stub |
| Orphan detection | Event-driven on zone change | Zones change only on placement and demolition; polling is waste |
| Anchor identity | `canFoundSettlement` on `InfluenceZone` | Single source of truth on the prefab; no cross-asset sync |
| Terrain rules | `BuildingData.allowedTerrain` | Removes `BuildingType` from placement logic entirely |
| Area test | Whole footprint | Origin-only lets large buildings straddle the border |
| Runtime overlay | Optional | Gizmos and debug output are sufficient to verify this slice |
| Building enumeration | `GetComponentsInChildren` under a stated invariant + assertion | Verified true for the placement path; `Island.buildings` is the future canonical path |
