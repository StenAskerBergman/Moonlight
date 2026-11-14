// tldr: Converts the island field into a signed distance-to-shoreline in WORLD UNITS, so coastal bands can be
// authored as real widths instead of inheriting whatever width the noise gradient happens to give.
// Run over a complete padded landform domain; storage chunks consume the shared result.

using UnityEngine;

/// <summary>
/// Signed distance to the shoreline, in world units, over the sampled terrain grid.
///
/// WHY THIS EXISTS. Coastal bands used to be windows on the scalar island field - the shore ramp
/// was "field 0.40 to 0.46". How much GROUND a field window covers depends entirely on the field's
/// local gradient, and that gradient varies about 5:1 around a single island (measured at the
/// shoreline: 0.062 field-units per world unit at the shallowest decile, 0.182 median, 0.307 at
/// the steepest). The same authored window therefore produced 0.97 world units of beach in a bay
/// and 0.20 on a headland. No authored width can survive that, which is why beaches were
/// effectively absent.
///
/// WHY NOT JUST NORMALIZE THE GRADIENT. d = (f - threshold) / |grad f| is the cheap trick, and it
/// is genuinely accurate immediately either side of the zero crossing. It is not adequate here for
/// two reasons: it degrades badly with distance, and this profile needs distances out to the shelf
/// edge (surf + shelf, ~12 world units); and where |grad f| is small it divides by a small number,
/// so a flat-ish shore reads as being kilometres from the water.
///
/// WHAT THIS DOES INSTEAD. Gradient normalisation is used ONLY to place the sub-cell crossing
/// inside the cells that actually straddle the shoreline - where it is accurate, and where the
/// result is additionally clamped to a cell and a half so a vanishing gradient cannot produce a
/// wild seed. Those seeds are then propagated with a two-pass dead-reckoning transform, which
/// carries the nearest boundary POINT rather than an accumulated distance, so the distance stays
/// true Euclidean over the whole field rather than accumulating chamfer error.
/// </summary>
public static class ShorelineDistanceField
{
    private const float GradientFloor = 0.004f;
    private const float MaxSeedCells = 1.5f;
    private const float Unreached = 1e20f;

    /// <summary>Forward+backward sweep pairs. See the propagation note below.</summary>
    private const int PropagationSweeps = 2;

    /// <summary>
    /// Fills <paramref name="signedDistance"/> with world-unit distance to the shoreline,
    /// positive where <paramref name="field"/> is at or above <paramref name="shorelineThreshold"/>.
    /// </summary>
    public static void Build(
        float[] field,
        float[] signedDistance,
        int resolution,
        float step,
        float shorelineThreshold)
    {
        int count = resolution * resolution;
        float[] nearestX = new float[count];
        float[] nearestZ = new float[count];
        float[] distance = new float[count];

        for (int i = 0; i < count; i++)
        {
            distance[i] = Unreached;
            nearestX[i] = float.NaN;
        }

        // --- Seed: cells whose 4-neighbourhood straddles the shoreline ---
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;
                float centre = field[index] - shorelineThreshold;
                bool centreInside = centre >= 0f;

                bool straddles = false;
                if (x > 0) straddles |= (field[index - 1] - shorelineThreshold >= 0f) != centreInside;
                if (!straddles && x < resolution - 1) straddles |= (field[index + 1] - shorelineThreshold >= 0f) != centreInside;
                if (!straddles && z > 0) straddles |= (field[index - resolution] - shorelineThreshold >= 0f) != centreInside;
                if (!straddles && z < resolution - 1) straddles |= (field[index + resolution] - shorelineThreshold >= 0f) != centreInside;
                if (!straddles) continue;

                int xm = x > 0 ? x - 1 : x;
                int xp = x < resolution - 1 ? x + 1 : x;
                int zm = z > 0 ? z - 1 : z;
                int zp = z < resolution - 1 ? z + 1 : z;
                float spanX = (xp - xm) * step;
                float spanZ = (zp - zm) * step;

                float gx = spanX > 0f ? (field[z * resolution + xp] - field[z * resolution + xm]) / spanX : 0f;
                float gz = spanZ > 0f ? (field[zp * resolution + x] - field[zm * resolution + x]) / spanZ : 0f;
                float gradient = Mathf.Sqrt(gx * gx + gz * gz);

                // Sub-cell offset from this sample to the crossing, along the field's downhill
                // direction. Clamped to a cell and a half: past that the linear approximation is
                // meaningless anyway, and the propagation below will find a better answer.
                float offset = Mathf.Abs(centre) / Mathf.Max(GradientFloor, gradient);
                offset = Mathf.Min(offset, step * MaxSeedCells);

                float px = x * step;
                float pz = z * step;
                float boundaryX = px;
                float boundaryZ = pz;
                if (gradient > GradientFloor)
                {
                    float inverse = offset / gradient;
                    // Walk against the gradient when inside (field high), with it when outside.
                    boundaryX = px - gx * inverse * Mathf.Sign(centre == 0f ? 1f : centre);
                    boundaryZ = pz - gz * inverse * Mathf.Sign(centre == 0f ? 1f : centre);
                }

                distance[index] = offset;
                nearestX[index] = boundaryX;
                nearestZ[index] = boundaryZ;
            }
        }

        // --- Propagation ---
        //
        // Two sweeps (one forward, one backward) is the textbook dead-reckoning transform and is
        // accurate near the boundary, which is all the beach bands need. It does NOT fully converge
        // far from the shore: information has to travel one sample per sweep in the unfavourable
        // direction, so at 17 world units out (136 samples) adjacent rows could still hold nearest
        // points from different propagation fronts. Measured as a 1.1 unit step in the distance
        // field between neighbouring samples, which the continental slope then turned into a
        // gradient of 10.5 in the seabed.
        //
        // The slope band is 16 units wide, so the field has to be smooth to ~24 units. Repeating
        // the sweep pair converges those fronts; each pass is a linear scan of the grid and costs
        // a few milliseconds against the noise evaluation that dominates generation.
        for (int sweep = 0; sweep < PropagationSweeps; sweep++)
        {
        // --- Forward pass ---
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;
                if (x > 0) Relax(index, index - 1, x, z, step, distance, nearestX, nearestZ);
                if (z > 0) Relax(index, index - resolution, x, z, step, distance, nearestX, nearestZ);
                if (x > 0 && z > 0) Relax(index, index - resolution - 1, x, z, step, distance, nearestX, nearestZ);
                if (x < resolution - 1 && z > 0) Relax(index, index - resolution + 1, x, z, step, distance, nearestX, nearestZ);
            }
        }

        // --- Backward pass ---
        for (int z = resolution - 1; z >= 0; z--)
        {
            for (int x = resolution - 1; x >= 0; x--)
            {
                int index = z * resolution + x;
                if (x < resolution - 1) Relax(index, index + 1, x, z, step, distance, nearestX, nearestZ);
                if (z < resolution - 1) Relax(index, index + resolution, x, z, step, distance, nearestX, nearestZ);
                if (x < resolution - 1 && z < resolution - 1) Relax(index, index + resolution + 1, x, z, step, distance, nearestX, nearestZ);
                if (x > 0 && z < resolution - 1) Relax(index, index + resolution - 1, x, z, step, distance, nearestX, nearestZ);
            }
        }
        }

        // --- Sign ---
        // A chunk with no shoreline at all (open ocean, or land filling the grid) leaves every
        // cell unreached. Saturating instead of writing 1e20 keeps downstream smoothsteps finite.
        float saturated = resolution * step;
        for (int i = 0; i < count; i++)
        {
            float d = distance[i] >= Unreached ? saturated : distance[i];
            signedDistance[i] = field[i] >= shorelineThreshold ? d : -d;
        }
    }

    /// <summary>
    /// Shoreline curvature, as the Laplacian of the signed distance field.
    ///
    /// For a distance field the Laplacian is the mean curvature of the level set through that
    /// point, so this is positive where the coast wraps around the sea (a bay) and negative where
    /// land juts into it (a headland) - the two places real shorelines respectively collect and
    /// lose sand.
    ///
    /// Sampled over a WIDE stencil on purpose. At the raw sample spacing the second difference is
    /// divided by step squared (0.0156), which amplifies sampling noise into something with no
    /// relation to the shape of the coast. A stencil a couple of world units across measures the
    /// curvature of the coastline rather than of the triangulation.
    /// </summary>
    public static void BuildCurvature(
        float[] signedDistance,
        float[] curvature,
        int resolution,
        float step,
        float stencilWorldRadius)
    {
        int k = Mathf.Max(1, Mathf.RoundToInt(stencilWorldRadius / Mathf.Max(0.0001f, step)));
        float h = k * step;
        float inverseHSquared = 1f / (h * h);

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;
                int xm = Mathf.Max(0, x - k);
                int xp = Mathf.Min(resolution - 1, x + k);
                int zm = Mathf.Max(0, z - k);
                int zp = Mathf.Min(resolution - 1, z + k);

                float centre = signedDistance[index];
                float sum = signedDistance[z * resolution + xm]
                          + signedDistance[z * resolution + xp]
                          + signedDistance[zm * resolution + x]
                          + signedDistance[zp * resolution + x];
                curvature[index] = (sum - 4f * centre) * inverseHSquared;
            }
        }

        // Re-base against this island's own coastline, so 0 means "an ordinary stretch of THIS
        // coast" rather than "straight".
        //
        // The raw Laplacian is not zero on a normal island. For a distance field positive inland,
        // a convex coast of radius R has curvature about -1/R everywhere - roughly -0.06 on a
        // 16-unit island - so measured against absolute zero the whole shoreline reads as a
        // headland and every beach is penalised. Measured: the median beach width fell to 0 and
        // 38-80% of shoreline lost its beach entirely.
        //
        // Averaging over the samples that actually sit on the shoreline gives that baseline per
        // island, so only genuine inlets read positive and genuine points read negative.
        double total = 0.0;
        int counted = 0;
        for (int i = 0; i < signedDistance.Length; i++)
        {
            if (signedDistance[i] < -1f || signedDistance[i] > 1f) continue;
            total += curvature[i];
            counted++;
        }

        if (counted == 0) return;

        float baseline = (float)(total / counted);
        for (int i = 0; i < curvature.Length; i++)
        {
            curvature[i] -= baseline;
        }
    }

    private static void Relax(
        int index,
        int fromIndex,
        int x,
        int z,
        float step,
        float[] distance,
        float[] nearestX,
        float[] nearestZ)
    {
        if (float.IsNaN(nearestX[fromIndex])) return;

        float px = x * step;
        float pz = z * step;
        float dx = px - nearestX[fromIndex];
        float dz = pz - nearestZ[fromIndex];
        float candidate = Mathf.Sqrt(dx * dx + dz * dz);
        if (candidate >= distance[index]) return;

        distance[index] = candidate;
        nearestX[index] = nearestX[fromIndex];
        nearestZ[index] = nearestZ[fromIndex];
    }
}
