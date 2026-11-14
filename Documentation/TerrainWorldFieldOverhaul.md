# Terrain world field overhaul

Chunks now store views of world-positioned landforms. Each island is generated once, including its complete shoreline and a sampling margin covering surf, shelf, continental slope, handoff and curvature stencil. Those margins are data only: terrain meshes still cover exactly the original non-overlapping chunk extents. The shared field chooses the highest landform at each world sample, with stable source ordering for ties. Heights, semantics and coastal-band queries use the same source.

The central selected island receives `dominantLandformScale`; the remaining selected islands use `secondaryLandformScale`. `landformPositionJitter` moves their origins independently of the mesh lattice. These settings are authored in Terrain World Settings. Per-slot overrides still resolve on private JSON copies, including serialized migration stamps. All slots must share sample density, ocean surface and abyss datum.

Beaches use shoreline curvature relative to the complete island's mean. Classification no longer excludes constructed beach solely because slope exceeds 0.45. Headlands suppress the material's residual sand strip as well as geometry width.

Rivers may start on sufficiently inland lowland ground with enough freeboard for a channel. Waterfalls are optional; an accepted waterfall still needs supported geology and its receiving reach. The wet contour naturally reaches zero depth at the bank, so footprint validation rejects terrain penetrating the water surface; minimum channel depth remains checked at sections. Mouth extension can use the landform margin rather than stopping at a storage chunk border. River meshes and resource probes translate from landform coordinates.

Land foliage uses the cached terrain and per-cell random streams. The default cap is 600 trees per chunk. Spatial patches batch mesh/material draws, select authored near/far LOD renderers and cull by distance. Existing prefab meshes and materials determine visual quality; this change does not supply new tree art. Construction and roads invalidate only affected foliage cells. `InvalidateCells` supports other local placement flows. Underwater life retains its existing renderer.

## User validation after compilation

Regenerate in Unity, then inspect `[Terrain seam ...]` messages: every adjacent pair reports mean and maximum height difference. `[Terrain cells ...]` reports terrain histograms and summed cell heights. The MapManager context menu can repeat these measurements without regeneration. No runtime measurements were taken during implementation.

`MapGrid.LastGenerationProfile` includes the owning landform's reservation/sampling work as well as chunk extraction and rendering work. Margins increase sampling and retained memory; measure the complete map before accepting its performance. Generation progress remains active between landforms and between chunks.

Check islands spanning chunks, dominant construction surface, bay/headland beaches, river continuity (especially where landforms meet), source/mouth resources, foliage LOD transitions and construction clearing. Also verify regeneration and domain reload. Compilation does not establish any of these visual or functional outcomes.
