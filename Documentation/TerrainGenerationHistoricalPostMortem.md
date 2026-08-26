# Historical terrain-generation post-mortem

> **Status:** Intermediate historical snapshot. This document describes failure modes and fixes before later terrain refinements. Its predicates and constants are not the current specification.
>
> Consult the current code, active serialized Unity settings, and `Temp/TerrainGenerationReferences/Latest/manifest.json` before making claims about present behavior.

Earlier terrain iterations showed conflicts between topology, physical elevation, feature masks, semantic classification, and texture classification.

The mechanisms below are evidence-supported explanations. Numeric outcomes identified as estimates require capture, profiler, or runtime confirmation.

## 1. Topological field versus physical elevation

### Historical failure mode

The two-dimensional field, $\Phi$, classified land when $\Phi \ge \text{waterUpper}$. A separate piecewise height ladder produced physical vertex elevation.

Earlier defaults placed `beachHeight` at $-0.50\text{m}$ and `surfaceFlatlandHeight` at $0.0\text{m}$. Land classification therefore did not ensure useful clearance above the rendered water surface.

Dynamic waves could visually submerge terrain near the nominal water plane. The exact drowned area, including the previously cited 90%, was not instrumented and should be treated as an observation-specific estimate.

### Intermediate resolution

The defaults were raised into a clearer elevation order: submerged seabed, shoreline transition, $+0.85\text{m}$ inland plateau, and elevated mountain features.

This increased dry-land clearance substantially. It did not mathematically guarantee clearance above every wave crest; that requires the active water level and maximum shader displacement.

## 2. Mountain land-mask denominator

### Historical failure mode

An intermediate mountain mask normalized the field across a broad interval:

$$
\text{landMask}=\operatorname{Clamp01}\left(
\frac{\Phi-\text{waterUpper}}
{\text{surfaceFlatlandUpper}-\text{waterUpper}}
\right)
$$

With `waterUpper = 0.40` and an upper bound near `0.695`, a sample at $\Phi=0.55$ produced a mask near $0.51$.

That example demonstrates the attenuation mechanism. Claims about typical interior field values, exact summit height, or every mountain being halved require recorded samples from that historical revision.

### Intermediate resolution

The denominator was narrowed to the shoreline crossing band:

$$
\text{landMask}=\operatorname{Clamp01}\left(
\frac{\Phi-\text{waterUpper}}
{\text{beachUpper}-\text{waterUpper}}
\right)
$$

For $\Phi \ge \text{beachUpper}$, this factor is 1.0. Final summit elevation still depends on base height, ridge elevation, envelope, mountain allowance, and ridged detail.

## 3. Slope-disqualified beaches

### Historical failure mode

An intermediate beach predicate required both a low elevation and slope below `0.60`. Shore ramps steeper than that threshold could fall through to grass or another channel.

This mechanism plausibly explains captures where grass met dark water with little or no visible sand. Exact ramp slopes and prevalence should be taken from the relevant historical capture, not assumed globally.

### Intermediate resolution

Beach selection was changed to emphasize post-deformation elevation rather than reject the shoreline solely for being steep.

Later refinements changed the exact predicate again. Therefore the historical $-0.05\text{m} \le h < 0.60\text{m}$ expression must not be quoted as the current classifier.

Current semantics and splat painting must be checked separately because both participate in the visible result.

## 4. Interior fractal troughs

### Historical failure mode

Low-frequency troughs in the local fractal field could reduce central field strength. Combined with radial falloff and thresholds, this could contribute to hollow or split silhouettes.

Island 28 was cited as a visual example in the original review. Without its retained indexed capture, that specific attribution remains historical testimony rather than reproducible evidence.

### Intermediate resolution

A radial core bias was introduced:

$$
\text{coreBias}=\operatorname{Clamp01}\left(1-\frac{r}{0.65}\right)\cdot0.18
$$

The bias reinforces central connectivity while leaving the outer region more strongly shaped by noise and domain warping.

It is not, by itself, a proof of a contiguous dry core. Such a proof would need bounds for fractal noise, warped radius, falloff, edge blending, thresholds, and later deformation.

## 5. Coastline search and watchdog pressure

### Historical failure mode

The committed coastline search used 48 radial rays with `0.5m` linear steps. Its cost was added to dense visual sampling and the remaining per-island generation work.

The previously cited 64 rays and 7,680 evaluations per chunk do not match the committed implementation. Evaluation totals also depend on island size and where each ray crosses water.

The Island 17 timeout identified the current watchdog checkpoint, not necessarily the operation that consumed all preceding time.

### Intermediate resolution

The coastline search changed to `3.0m` coarse steps followed by three binary refinements.

Under the illustrative 120-to-15 evaluation comparison, this is about 8 times fewer evaluations for that search, or an 87.5% reduction. It is not evidence of an 8-times-faster complete map generation.

`MapGrid` records the generation phase timings. `TerrainGenerationReferenceCapture` exports those measurements to `manifest.json` and `index.md` while excluding capture/export time.

## Historical comparison

| Aspect | Earlier failure | Intermediate improvement | Evidence boundary |
|---|---|---|---|
| Physical elevation | Semantic land could sit near the nominal water plane | Inland plateau default raised to $+0.85\text{m}$ | Wave clearance still requires water-shader bounds |
| Topology | Interior troughs could weaken or split silhouettes | Radial core bias reinforces the center | Contiguity is not mathematically proven |
| Mountain scaling | Broad normalization could attenuate ridge elevation | Shore-band normalization reaches 1.0 sooner | Final peaks depend on the complete ridge equation |
| Shoreline zoning | Slope gating could reject shoreline ramps | Elevation became the primary beach signal | Later predicates superseded the historical equation |
| Diagnostics | Timeout evidence lacked indexed visual context | Per-island images and phase timings are exported | Checkpoints do not prove which prior operation caused a timeout |

## Use of this document

Use this document to understand why earlier approaches were changed. Do not use it as the present terrain specification.

For a current review, follow `Documentation/TerrainGenerationAIReview.md`, inspect `Latest` and `LatestFailed`, then verify any architectural claim against current code and serialized settings.
