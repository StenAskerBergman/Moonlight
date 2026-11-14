# Connected inland river — first inspection milestone

## Scope

One elevated spring course per eligible island: terrain-routed drainage, gorge banks,
geological drop(s), connected plunge pool(s), an incised lowland reach and an open estuary.
No source-lake variant, split/rejoin branches, bridge family work, or secondary asset polish
is enabled in this milestone. The old independent lake disks and sinusoidal river generator
have been removed from production rather than left active alongside RiverPlan.

## Ownership and ordering

`IslandTerrainProvider.GetOrCreateSampleCache` retains the completed coastal passes. After
coast/ridge geometry and mountain validation, `FinalizeRiverLandscape` asks `RiverPlanner`
for an admissible course against that full field. It then applies the accepted landform and
updates slopes and classification. There is no subsequent beach pass to refill its mouth.

`RiverPlan` owns ordered arc-length sections, bed/water levels, asymmetric bank constraints,
reach kinds, crest/toe/pool relationships, excavation/fill budgets, and the mouth contract.
Those stations are hydraulic guidance, not a lofted terrain strip. `RiverLandform` resolves them
against the high-resolution terrain into one irregular two-dimensional valley/channel/basin
field. Carving, water-mesh extraction, terrain material masks and gameplay slots consume that
same field.
Public terrain probes read the same final terrain cache, including the terrain mesh diagonal.

The shoreline contract is `settings.waterHeight == AuthoritativeWaterSurfaceHeight`, and
`cache.ShoreDistances == 0` at the shoreline (positive inland). `RiverPlan.Mouth` supplies
centre, direction, actual distance, water/bed elevation, channel width and both bank extents.
Its explicit estuary reach extends through the beach and onto the submerged shelf. Estuary
modifications are cut-only so they cannot raise artificial breakwaters from the shelf.

A reserved mouth transition — `min(9, 35% of the distance to the crossing)` — runs upstream from
that crossing. No waterfall or plunge basin may occupy it, and the channel and its banks taper
into the shoreline across it, so the coastal outlet is one continuous mouth rather than a widened
field joined by two shoulders. `ValidateMouthSilhouette` enforces both as an acceptance rule:
a course can satisfy every depth, budget, bounds and self-overlap check and still meet the sea as
an artificial basin, which the numeric checks alone could not express.

Cut-only estuary carving means the submerged shelf keeps its original height, so *depth* is true
across the whole stamped footprint there — banks and blend skirt included. The wet contour is
therefore bounded by the channel and its shallow margin, not by depth alone; without that bound
the mouth exported its entire valley as inland water, and since depth never crossed zero inside
the footprint the mesh clipper had no crossing to interpolate and snapped to the sample grid.
Rendered inland water hands off to the ocean within 2.5 units of the crossing. The plan still
routes to the shelf so the bed stays continuous — the ocean covers the rest.

Waterfall lips, separately tessellated near-vertical falling-water curtains and area-shaped
plunge basins use the same section elevations as their geological drop. The ordinary horizontal
river mesh stops across the fall instead of stretching a sloping quad down it. Distance UVs and
plan-authored impact/edge weights feed `Moonlight/Inland River`.
This is functional whitewater treatment, not the final reference-quality water/material pass.
An assigned inland material must use this shader; ocean materials with independent surface
displacement cannot be substituted for the plan's fixed elevations.

## Bounded generation

The search tries at most 12 separated elevated source candidates and five coastal routes per
source. It favours downhill terrain and inland drainage rather than coastal detours. Route
smoothing is rechecked against the actual terrain. The first milestone requires a drop and
a lower inland reach; it does not manufacture a disconnected waterfall to meet a quota.

Landform cuts are bounded by `max(2.5, valleyDepth * 2)` and fill by 1.25 world units. Inland
beds remain above the ocean plane; submerged beds are confined to the estuary/shelf handoff.
Invalid routes are rejected before changing the cache. Rejection reasons and accepted
length/drop counts are emitted as `[RiverPlan seed ...]`, also available through
`IslandTerrainProvider.RiverGenerationReport`. These admissibility checks do not establish
visual quality. A seed with no viable source/route reports that result instead of generating
unsupported water. Existing flat terrain is unchanged outside the plan's footprint.

The landform implementation intentionally does not expose repeated cross-section polygons.
Coherent world-space erosion noise, the existing slope/mountain field and independent left/right
constraints vary the valley boundary, rock pinches, shallow margins, gravel benches and wall
transition. Plunge pools override the guide as elongated basins: they open beneath the impact,
form a deeper bowl in two dimensions, and narrow back to the authored downstream outlet.

Gameplay slots are only emitted on matching generated features and valid unoccupied cells.
The `[RiverPlan slots]` report distinguishes a missing valid slot from a missing waterfall.
Waterfall cells are underwater/non-buildable, but are not boat-traversable waterfall ramps.

## Deferred until visual approval

- Contained source lakes with actual outlet spillways; the legacy lake chance/radius settings
  are retained for serialization, but are not used by this spring-source milestone.
- Split/rejoin courses around rocks and further cross-section/landform variants.
- Full bridge-crossing plans and tier/material-specific modular families. Existing road
  routing/art is preserved; its legacy river-cell bridge solver is not claimed as complete
  or safe for this new landscape. Inspect the river without placing bridges in this milestone.
- Mist particles, authored rock meshes, shoreline plants, detailed material/lighting polish.

## User inspection after compilation

Regenerate in Unity, locate an accepted `[RiverPlan seed ...]` entry and follow that course
from source to sea. Check containment, fall/terrain alignment, receiving pool/outlet, lower
river incision, riverbank material, mouth opening and preservation of surrounding buildable
land. Check both close side views and the whole island. Compilation does not validate those
properties. No generation runs or automated tests are part of this compile-only handoff.
