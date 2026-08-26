# Terrain generation AI review references

The historical failure analysis is preserved in `Documentation/TerrainGenerationHistoricalPostMortem.md`. It describes an intermediate revision, not the current terrain specification.

Edit-mode map generation automatically creates disposable review artifacts under:

- `Temp/TerrainGenerationReferences/Latest` — the last successful generation and the performance baseline.
- `Temp/TerrainGenerationReferences/LatestFailed` — the last failed generation, captured synchronously before partial islands are cleaned up.

Both directories contain an `index.md` for fast human/AI review and a `manifest.json` for exact machine-readable timings and metadata. Each completed island is indexed consistently in filenames and inside its images:

- `island-017_top-left_side-right.png` — orthographic rendered geometry.
- `island-017_splat-control.png` — terrain control channels with alpha forced opaque and an in-image terrain-color legend.
- `island-017_gameplay-grid.png` — the splat map overlaid with the authoritative per-cell placement result, cyan buildable-region boundary, and typed resource-node markers.

## Instructions for future AI reviewers

1. Read `Latest/index.md` first to establish the last successful visual and timing baseline.
2. If `LatestFailed` exists, read its `index.md` and `manifest.json` next. It is a partial pre-cleanup snapshot, not a valid successful baseline.
3. Locate the reported failure chunk and phase, then inspect the last captured island and its per-phase timing. The phase is a watchdog checkpoint: expensive work may have happened in the preceding phase.
4. Compare total and per-island timings with the successful manifest. Capture/render/export time is excluded from terrain-generation timings.
5. A missing island image in `LatestFailed` means generation stopped before that island completed enough rendering state to capture; it does not mean the capture utility deleted it.
6. Splat channels are `R = grass`, `G = sand`, `B = rock`, and opaque black represents the alpha/seafloor-water channel.
7. In the gameplay-grid image, faint green means `GridSystem.IsValidSurfaceConstructionCell` accepted that cell at capture time; faint red means it rejected it. Cyan outlines the accepted region boundary.
8. Resource markers use the two-letter code and color shown in the image legend. They reflect each generated `Cell.depositNodeType`, not inferred texture color.
9. Treat image and timing evidence separately. A compile pass does not prove that generated terrain looks or behaves correctly.
10. If the selected `MapManager.PatternData.invertSelection` checkbox was true at capture time, each image shows an orange `INVERTED` flag and the manifest records `selectionInverted: true`. Treat that as generation-state evidence, not a terrain color.

Successful regeneration replaces only `Latest`. Failed regeneration replaces only `LatestFailed`, preserving the last known-good comparison point.
