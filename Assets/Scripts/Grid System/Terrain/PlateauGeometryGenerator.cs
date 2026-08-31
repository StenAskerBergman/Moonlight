using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pure generated geometry returned by <see cref="PlateauGeometryGenerator"/>.
/// Unity mesh allocation stays at the MapGrid upload seam; the generator itself
/// only produces deterministic arrays.
/// </summary>
internal sealed class PlateauGeneratedMeshData
{
    public PlateauGeneratedMeshData(Vector3[] vertices, int[] triangles, Vector2[] uvs)
    {
        Vertices = vertices ?? Array.Empty<Vector3>();
        Triangles = triangles ?? Array.Empty<int>();
        Uvs = uvs ?? Array.Empty<Vector2>();
    }

    public Vector3[] Vertices { get; }
    public int[] Triangles { get; }
    public Vector2[] Uvs { get; }
    public bool HasGeometry => Vertices.Length >= 3 && Triangles.Length >= 3;

    public Mesh CreateMesh(string name)
    {
        if (!HasGeometry) return null;

        Mesh mesh = new Mesh { name = name };
        if (Vertices.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = Vertices;
        mesh.triangles = Triangles;
        mesh.uv = Uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }
}

internal sealed class PlateauGeometryResult
{
    public PlateauGeometryResult(
        PlateauGeneratedMeshData escarpment,
        PlateauGeneratedMeshData formations)
    {
        Escarpment = escarpment;
        Formations = formations;
    }

    public PlateauGeneratedMeshData Escarpment { get; }
    public PlateauGeneratedMeshData Formations { get; }
    public bool HasGeometry =>
        (Escarpment != null && Escarpment.HasGeometry)
        || (Formations != null && Formations.HasGeometry);
}

/// <summary>
/// Deep visual-geometry module for standalone plateaus. Its single interface turns
/// the authoritative sampled plateau field into volumetric render layers. Gameplay,
/// splat classification, and construction remain owned by TerrainSampleCache.
/// </summary>
internal static class PlateauGeometryGenerator
{
    private enum RockArchetype
    {
        FoundationBoulder,
        ShelfSlab,
        FracturedButtress,
        RoundedShoulder,
        NarrowSpire,
    }

    private readonly struct ContourPoint
    {
        public ContourPoint(Vector3 position, Vector3 outward, Vector3 profileOutward)
        {
            Position = position;
            Outward = outward;
            ProfileOutward = profileOutward;
        }

        public Vector3 Position { get; }
        public Vector3 Outward { get; }
        public Vector3 ProfileOutward { get; }
    }

    public static PlateauGeometryResult Generate(
        TerrainSampleCache cache,
        StandalonePlateauSettings settings,
        int chunkSeed,
        int worldSeed)
    {
        if (cache == null) throw new ArgumentNullException(nameof(cache));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (!settings.generateVolumetricRockGeometry)
        {
            return new PlateauGeometryResult(EmptyMesh(), EmptyMesh());
        }

        settings.Validate();
        if (!TryFindTabletopCentroid(cache, out Vector2 center))
        {
            return new PlateauGeometryResult(EmptyMesh(), EmptyMesh());
        }

        ContourPoint[] contour = ExtractContour(
            cache,
            center,
            settings.escarpmentContourSegments);
        if (contour.Length < 3)
        {
            return new PlateauGeometryResult(EmptyMesh(), EmptyMesh());
        }

        MeshAccumulator escarpment = new MeshAccumulator();
        BuildEscarpment(
            escarpment,
            cache,
            contour,
            settings,
            chunkSeed,
            worldSeed);

        MeshAccumulator formations = new MeshAccumulator();
        BuildPerimeterFormations(
            formations,
            cache,
            contour,
            center,
            settings,
            chunkSeed,
            worldSeed);

        return new PlateauGeometryResult(
            escarpment.ToMeshData(),
            formations.ToMeshData());
    }

    private static PlateauGeneratedMeshData EmptyMesh()
    {
        return new PlateauGeneratedMeshData(
            Array.Empty<Vector3>(),
            Array.Empty<int>(),
            Array.Empty<Vector2>());
    }

    private static bool TryFindTabletopCentroid(TerrainSampleCache cache, out Vector2 center)
    {
        double sumX = 0d;
        double sumZ = 0d;
        int count = 0;
        int resolution = cache.Resolution;
        float step = cache.Step;

        for (int z = 0; z < resolution; z++)
        {
            int row = z * resolution;
            for (int x = 0; x < resolution; x++)
            {
                if (cache.TerrainTypes[row + x] != Cell.TerrainType.Plateau) continue;
                sumX += x * step;
                sumZ += z * step;
                count++;
            }
        }

        if (count == 0)
        {
            center = default;
            return false;
        }

        center = new Vector2((float)(sumX / count), (float)(sumZ / count));
        return true;
    }

    private static ContourPoint[] ExtractContour(
        TerrainSampleCache cache,
        Vector2 center,
        int segmentCount)
    {
        Vector3[] positions = new Vector3[segmentCount];
        float rayStep = Mathf.Max(0.12f, cache.Step * 0.72f);
        float maximumRadius = cache.GridSize * 1.45f;

        for (int index = 0; index < segmentCount; index++)
        {
            float angle = index / (float)segmentCount * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            bool foundInside = false;
            float lastInsideRadius = 0f;

            for (float radius = 0f; radius <= maximumRadius; radius += rayStep)
            {
                Vector2 point = center + direction * radius;
                bool inside = IsTabletop(cache, point.x, point.y);
                if (inside)
                {
                    foundInside = true;
                    lastInsideRadius = radius;
                }
                else if (foundInside)
                {
                    break;
                }
            }

            if (!foundInside)
            {
                positions[index] = new Vector3(center.x, SampleHeight(cache, center.x, center.y), center.y);
                continue;
            }

            // Seat the visual lip on the tabletop side of the semantic transition.
            // Sampling beyond lastInsideRadius put some formation bases onto the
            // descending heightfield even though their anchor belonged to the rim.
            Vector2 boundary = center + direction * Mathf.Max(0f, lastInsideRadius - rayStep * 0.30f);
            positions[index] = new Vector3(
                boundary.x,
                SampleHeight(cache, boundary.x, boundary.y),
                boundary.y);
        }

        ContourPoint[] contour = new ContourPoint[segmentCount];
        for (int index = 0; index < segmentCount; index++)
        {
            Vector3 previous = positions[(index - 1 + segmentCount) % segmentCount];
            Vector3 next = positions[(index + 1) % segmentCount];
            Vector3 tangent = next - previous;
            tangent.y = 0f;
            tangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
            Vector3 outward = new Vector3(tangent.z, 0f, -tangent.x);
            Vector3 profileOutward = positions[index]
                - new Vector3(center.x, positions[index].y, center.y);
            profileOutward.y = 0f;
            profileOutward = profileOutward.sqrMagnitude > 0.0001f
                ? profileOutward.normalized
                : outward;
            if (Vector3.Dot(outward, profileOutward) < 0f) outward = -outward;
            contour[index] = new ContourPoint(positions[index], outward, profileOutward);
        }

        return contour;
    }

    private static void BuildEscarpment(
        MeshAccumulator mesh,
        TerrainSampleCache cache,
        ContourPoint[] contour,
        StandalonePlateauSettings settings,
        int chunkSeed,
        int worldSeed)
    {
        int layerCount = settings.escarpmentStrata;
        float[] contourDistance = BuildContourDistances(contour, out float perimeter);
        Vector3[,] wallVertices = new Vector3[contour.Length, layerCount + 1];

        // Resolve each shell vertex once. The former per-quad evaluation repeated
        // every heightfield lookup up to four times and made the visual-only mesh
        // disproportionately expensive compared with the island itself.
        for (int point = 0; point < contour.Length; point++)
        {
            for (int layer = 0; layer <= layerCount; layer++)
            {
                wallVertices[point, layer] = EvaluateWallVertex(
                    cache,
                    contour[point],
                    point,
                    contour.Length,
                    layer,
                    layerCount,
                    settings,
                    chunkSeed,
                    worldSeed);
            }
        }

        for (int segment = 0; segment < contour.Length; segment++)
        {
            int next = (segment + 1) % contour.Length;
            float u0 = contourDistance[segment] / Mathf.Max(0.001f, perimeter);
            float u1 = next == 0 ? 1f : contourDistance[next] / Mathf.Max(0.001f, perimeter);

            for (int layer = 0; layer < layerCount; layer++)
            {
                Vector3 topA = wallVertices[segment, layer];
                Vector3 topB = wallVertices[next, layer];
                Vector3 bottomB = wallVertices[next, layer + 1];
                Vector3 bottomA = wallVertices[segment, layer + 1];

                float v0 = layer / (float)layerCount;
                float v1 = (layer + 1f) / layerCount;
                mesh.AddQuad(
                    topA,
                    topB,
                    bottomB,
                    bottomA,
                    new Vector2(u0 * 8f, v0 * 4f),
                    new Vector2(u1 * 8f, v0 * 4f),
                    new Vector2(u1 * 8f, v1 * 4f),
                    new Vector2(u0 * 8f, v1 * 4f));
            }
        }
    }

    private static Vector3 EvaluateWallVertex(
        TerrainSampleCache cache,
        ContourPoint point,
        int pointIndex,
        int pointCount,
        int layer,
        int layerCount,
        StandalonePlateauSettings settings,
        int chunkSeed,
        int worldSeed)
    {
        float t = layer / (float)layerCount;
        int block = pointIndex / 4;
        float blockOffset = SignedSeed(chunkSeed, worldSeed, 1103 + block * 17) * 0.11f;
        float fracture = SignedSeed(chunkSeed, worldSeed, 1709 + pointIndex * 23 + layer * 101)
            * Mathf.Min(0.14f, settings.cliffFractureStrength * 0.10f);
        float contourPhase = pointIndex / (float)Mathf.Max(1, pointCount) * Mathf.PI * 2f;
        float layerPhase = SeedUnit(chunkSeed, worldSeed, 1301) * Mathf.PI * 2f + contourPhase * 1.35f;
        float stratumEnvelope = Mathf.Sin(t * Mathf.PI);

        float drop = EvaluateRockProfileDrop(layer, layerCount, settings);

        // The heightfield profile is evaluated along a radial ray and its width is
        // shortened and noise-varied locally. Resolve the actual sampled crossing
        // instead of moving along the smoothed contour normal using a global maximum
        // width. That keeps this shell just outside the authoritative surface around
        // lobes and notches rather than exposing it as white curtains between panels.
        float profileDistance = ResolveProfileDistance(cache, point, drop, settings);

        // A small positive skin keeps the render shell outside the heightfield.
        // Alternating offsets then cut shallow shelves and undercuts around that
        // profile without returning to the old near-vertical hanging ribbon.
        float surfaceVariation = (Mathf.Sin(t * Mathf.PI * 2.5f + layerPhase) * 0.13f
                + blockOffset
                + fracture) * stratumEnvelope;
        float outset = profileDistance + Mathf.Max(0.08f, 0.24f + surfaceVariation);
        float stratumBreak = SignedSeed(chunkSeed, worldSeed, 1901 + layer * 37) * 0.10f;
        float y = point.Position.y + 0.04f - drop + stratumBreak * stratumEnvelope;

        Vector3 tangent = Vector3.Cross(point.ProfileOutward, Vector3.up);
        float shear = SignedSeed(chunkSeed, worldSeed, 2203 + block * 41) * stratumEnvelope * 0.10f;
        return point.Position
            + point.ProfileOutward * outset
            + tangent * shear
            + Vector3.up * (y - point.Position.y);
    }

    private static float ResolveProfileDistance(
        TerrainSampleCache cache,
        ContourPoint point,
        float drop,
        StandalonePlateauSettings settings)
    {
        if (drop <= 0.0001f) return 0f;

        float targetHeight = point.Position.y - drop;
        float analyticMaximum = settings.upperEscarpmentWidth
                * (1f + settings.profileAsymmetry)
            + settings.lowerApronWidth
                * (1f + settings.profileAsymmetry * 0.55f);
        float distanceToEdge = DistanceToChunkEdge(
            point.Position,
            point.ProfileOutward,
            cache.GridSize);
        float scanLimit = Mathf.Min(
            distanceToEdge,
            analyticMaximum + Mathf.Max(0.5f, cache.Step * 2f));
        float scanStep = Mathf.Max(0.10f, cache.Step * 0.72f);
        float previousDistance = 0f;
        float previousHeight = point.Position.y;

        for (float distance = scanStep; distance <= scanLimit; distance += scanStep)
        {
            Vector3 samplePoint = point.Position + point.ProfileOutward * distance;
            float height = SampleHeight(cache, samplePoint.x, samplePoint.z);
            if (height <= targetHeight)
            {
                float denominator = previousHeight - height;
                float crossing = denominator > 0.0001f
                    ? Mathf.Clamp01((previousHeight - targetHeight) / denominator)
                    : 1f;
                return Mathf.Lerp(previousDistance, distance, crossing);
            }

            previousDistance = distance;
            previousHeight = height;
        }

        // The profile can terminate very close to the chunk seam, where the last
        // bilinear sample may sit fractionally above the requested band. Preserve a
        // deterministic, bounded fallback instead of extending beyond the chunk.
        return Mathf.Min(
            distanceToEdge,
            InverseRockProfileDistance(drop, settings));
    }

    private static float DistanceToChunkEdge(
        Vector3 position,
        Vector3 direction,
        float gridSize)
    {
        const float epsilon = 0.0001f;
        float xDistance = Mathf.Infinity;
        if (direction.x > epsilon)
        {
            xDistance = (gridSize - position.x) / direction.x;
        }
        else if (direction.x < -epsilon)
        {
            xDistance = -position.x / direction.x;
        }

        float zDistance = Mathf.Infinity;
        if (direction.z > epsilon)
        {
            zDistance = (gridSize - position.z) / direction.z;
        }
        else if (direction.z < -epsilon)
        {
            zDistance = -position.z / direction.z;
        }

        return Mathf.Max(0f, Mathf.Min(xDistance, zDistance));
    }

    private static float EvaluateRockProfileDrop(
        int layer,
        int layerCount,
        StandalonePlateauSettings settings)
    {
        if (layer <= 0) return 0f;
        if (layer >= layerCount)
        {
            return settings.cliffDropDepth + settings.lowerApronDrop;
        }

        // Allocate most bands to the steep upper escarpment while reserving at
        // least one for the apron. Within each region, advance in profile-distance
        // space and evaluate the forward heightfield curve. Its inverse below then
        // returns evenly distributed radial strata instead of one oversized last
        // apron quad.
        int upperLayerCount = Mathf.Clamp(
            Mathf.RoundToInt(layerCount * 0.60f),
            2,
            layerCount - 1);
        if (layer <= upperLayerCount)
        {
            float upperProgress = layer / (float)upperLayerCount;
            return settings.cliffDropDepth * Mathf.Pow(upperProgress, 0.42f);
        }

        int lowerLayerCount = layerCount - upperLayerCount;
        float lowerProgress = (layer - upperLayerCount) / (float)lowerLayerCount;
        float softeningDrop = 1f - Mathf.Pow(1f - lowerProgress, 2.6f);
        return settings.cliffDropDepth + settings.lowerApronDrop * softeningDrop;
    }

    private static float InverseRockProfileDistance(
        float drop,
        StandalonePlateauSettings settings)
    {
        float upperDrop = Mathf.Max(0.001f, settings.cliffDropDepth);
        float maximumUpperWidth = settings.upperEscarpmentWidth
            * (1f + settings.profileAsymmetry);
        if (drop <= upperDrop)
        {
            float normalizedDrop = Mathf.Clamp01(drop / upperDrop);
            float upperProgress = Mathf.Pow(normalizedDrop, 1f / 0.42f);
            return maximumUpperWidth * upperProgress;
        }

        float lowerDrop = Mathf.Max(0.001f, settings.lowerApronDrop);
        float normalizedLowerDrop = Mathf.Clamp01((drop - upperDrop) / lowerDrop);
        float lowerProgress = 1f - Mathf.Pow(1f - normalizedLowerDrop, 1f / 2.6f);
        float maximumLowerWidth = settings.lowerApronWidth
            * (1f + settings.profileAsymmetry * 0.55f);
        return maximumUpperWidth + maximumLowerWidth * lowerProgress;
    }

    private static float[] BuildContourDistances(ContourPoint[] contour, out float perimeter)
    {
        float[] distances = new float[contour.Length];
        float running = 0f;
        for (int index = 1; index < contour.Length; index++)
        {
            running += Vector3.Distance(contour[index - 1].Position, contour[index].Position);
            distances[index] = running;
        }

        perimeter = running + Vector3.Distance(
            contour[contour.Length - 1].Position,
            contour[0].Position);
        return distances;
    }

    private static void BuildPerimeterFormations(
        MeshAccumulator mesh,
        TerrainSampleCache cache,
        ContourPoint[] contour,
        Vector2 center,
        StandalonePlateauSettings settings,
        int chunkSeed,
        int worldSeed)
    {
        int clusterCount = settings.perimeterClusterCount;
        if (clusterCount > 0 && settings.perimeterClusterHeight > 0f)
        {
            float fullCircle = Mathf.PI * 2f;
            float spacing = fullCircle / clusterCount;
            float rotation = SeedUnit(chunkSeed, worldSeed, 503) * fullCircle;

            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                float angle = rotation
                    + (cluster + 0.5f) * spacing
                    + (SeedUnit(chunkSeed, worldSeed, 521 + cluster * 31) - 0.5f) * spacing * 0.58f;
                int contourIndex = FindContourIndex(contour, center, angle);
                float width = settings.perimeterClusterWidth
                    * Mathf.Lerp(0.68f, 1.32f, SeedUnit(chunkSeed, worldSeed, 541 + cluster * 31));
                float depth = settings.perimeterClusterDepth
                    * Mathf.Lerp(0.72f, 1.30f, SeedUnit(chunkSeed, worldSeed, 563 + cluster * 31));
                float height = settings.perimeterClusterHeight
                    * Mathf.Lerp(0.62f, 1.18f, SeedUnit(chunkSeed, worldSeed, 577 + cluster * 31));

                AddRockCluster(
                    mesh,
                    cache,
                    contour[contourIndex],
                    width,
                    depth,
                    height,
                    settings,
                    settings.rocksPerCluster,
                    CombineSeed(chunkSeed, worldSeed, 3001 + cluster * 97));
            }
        }

        for (int spire = 0; spire < settings.perimeterSpireCount; spire++)
        {
            if (settings.occasionalSpireHeight <= 0f) break;
            float angle = SeedUnit(chunkSeed, worldSeed, 701 + spire * 43) * Mathf.PI * 2f;
            int contourIndex = FindContourIndex(contour, center, angle);
            ContourPoint anchor = contour[contourIndex];
            // outward x up completes a right-handed horizontal frame. The previous
            // up x outward basis reversed every rock ring and made all faces inward.
            Vector3 tangent = Vector3.Cross(anchor.Outward, Vector3.up);
            float width = settings.spireBaseWidth
                * Mathf.Lerp(0.72f, 1.28f, SeedUnit(chunkSeed, worldSeed, 719 + spire * 43));
            float height = settings.occasionalSpireHeight
                * Mathf.Lerp(0.58f, 1f, SeedUnit(chunkSeed, worldSeed, 751 + spire * 43));
            float inset = Mathf.Max(settings.rockyRimWidth * 0.48f, width * 0.46f);
            Vector3 basePosition = anchor.Position
                - anchor.Outward * inset
                + tangent * SignedSeed(chunkSeed, worldSeed, 3301 + spire * 61) * width * 0.22f;
            basePosition = MoveInsideTabletop(cache, basePosition, -anchor.Outward);
            basePosition.y = Mathf.Max(
                anchor.Position.y,
                SampleHeight(cache, basePosition.x, basePosition.z)) - 0.18f;

            AddRockVolume(
                mesh,
                basePosition,
                tangent,
                anchor.Outward,
                width * 1.15f,
                width * 0.86f,
                height * 0.34f,
                RockArchetype.FoundationBoulder,
                CombineSeed(chunkSeed, worldSeed, 3407 + spire * 73));
            AddRockVolume(
                mesh,
                basePosition + Vector3.up * height * 0.12f,
                tangent,
                anchor.Outward,
                width * 0.58f,
                width * 0.50f,
                height,
                RockArchetype.NarrowSpire,
                CombineSeed(chunkSeed, worldSeed, 3511 + spire * 73));
        }
    }

    private static void AddRockCluster(
        MeshAccumulator mesh,
        TerrainSampleCache cache,
        ContourPoint anchor,
        float clusterWidth,
        float clusterDepth,
        float clusterHeight,
        StandalonePlateauSettings settings,
        int rockCount,
        int seed)
    {
        Vector3 tangent = Vector3.Cross(anchor.Outward, Vector3.up);

        // Each perimeter cluster is one overlapping geological mass. A broad,
        // embedded foundation supplies the common base; medium slabs, shoulders,
        // and buttresses overlap it, and exactly one support may become the local
        // dominant peak.
        float foundationInset = Mathf.Max(
            settings.rockyRimWidth * 0.48f,
            clusterDepth * 0.42f);
        Vector3 foundationBase = anchor.Position - anchor.Outward * foundationInset;
        foundationBase = MoveInsideTabletop(cache, foundationBase, -anchor.Outward);
        foundationBase.y = Mathf.Max(
            anchor.Position.y,
            SampleHeight(cache, foundationBase.x, foundationBase.z)) - 0.34f;
        AddRockVolume(
            mesh,
            foundationBase,
            tangent,
            anchor.Outward,
            clusterWidth * 0.92f,
            clusterDepth * 1.12f,
            clusterHeight * Mathf.Lerp(0.36f, 0.46f, Hash01(seed, 379)),
            RockArchetype.FoundationBoulder,
            seed + 101);

        int supportCount = Mathf.Max(2, rockCount - 1);
        int dominantSupport = Mathf.Min(
            supportCount - 1,
            Mathf.FloorToInt(Hash01(seed, 397) * supportCount));
        for (int support = 0; support < supportCount; support++)
        {
            float normalized = supportCount <= 1 ? 0.5f : support / (float)(supportCount - 1);
            float lateral = (normalized - 0.5f) * clusterWidth * 0.62f
                + SignedHash(seed, 401 + support * 13) * clusterWidth * 0.09f;
            float centerBias = 1f - Mathf.Abs(normalized - 0.5f) * 1.45f;
            bool isDominant = support == dominantSupport;
            RockArchetype archetype;
            if (isDominant)
            {
                archetype = Hash01(seed, 421) < 0.58f
                    ? RockArchetype.FracturedButtress
                    : RockArchetype.RoundedShoulder;
            }
            else
            {
                switch (support % 3)
                {
                    case 0:
                        archetype = RockArchetype.ShelfSlab;
                        break;
                    case 1:
                        archetype = RockArchetype.RoundedShoulder;
                        break;
                    default:
                        archetype = RockArchetype.FracturedButtress;
                        break;
                }
            }

            float width;
            float depth;
            float height;
            switch (archetype)
            {
                case RockArchetype.ShelfSlab:
                    width = clusterWidth * Mathf.Lerp(0.42f, 0.60f, Hash01(seed, 467 + support * 23));
                    depth = clusterDepth * Mathf.Lerp(0.42f, 0.60f, Hash01(seed, 499 + support * 29));
                    height = clusterHeight * Mathf.Lerp(0.22f, 0.34f, Hash01(seed, 541 + support * 31));
                    break;
                case RockArchetype.FracturedButtress:
                    width = clusterWidth * Mathf.Lerp(0.28f, 0.42f, Hash01(seed, 467 + support * 23));
                    depth = clusterDepth * Mathf.Lerp(0.48f, 0.70f, Hash01(seed, 499 + support * 29));
                    height = clusterHeight * Mathf.Lerp(0.42f, 0.58f, Hash01(seed, 541 + support * 31));
                    break;
                default:
                    width = clusterWidth * Mathf.Lerp(0.36f, 0.54f, Hash01(seed, 467 + support * 23));
                    depth = clusterDepth * Mathf.Lerp(0.54f, 0.76f, Hash01(seed, 499 + support * 29));
                    height = clusterHeight * Mathf.Lerp(0.34f, 0.54f, Hash01(seed, 541 + support * 31));
                    break;
            }

            if (isDominant)
            {
                width = Mathf.Max(width, clusterWidth * 0.42f);
                depth = Mathf.Max(depth, clusterDepth * 0.60f);
                height = clusterHeight
                    * Mathf.Lerp(0.78f, 1f, Hash01(seed, 593))
                    * Mathf.Lerp(0.92f, 1.08f, Mathf.Clamp01(centerBias));
            }

            float semanticInset = settings.rockyRimWidth
                * Mathf.Lerp(0.38f, 0.64f, Hash01(seed, 433 + support * 19));
            float inward = Mathf.Max(semanticInset, depth * 0.42f + 0.12f);
            Vector3 basePosition = anchor.Position
                + tangent * lateral
                - anchor.Outward * inward;
            basePosition = MoveInsideTabletop(cache, basePosition, -anchor.Outward);
            basePosition.y = Mathf.Max(
                anchor.Position.y,
                SampleHeight(cache, basePosition.x, basePosition.z)) - 0.20f;

            AddRockVolume(
                mesh,
                basePosition,
                tangent,
                anchor.Outward,
                width,
                depth,
                height,
                archetype,
                seed + support * 1013);
        }

        // Most, but not every, mass grows a vertical support into the upper
        // escarpment. Its base follows the same inverse profile as the wall, so the
        // rim and cliff overlap as one structure instead of meeting at a clean seam.
        if (Hash01(seed, 613) > 0.24f)
        {
            float descent = settings.cliffDropDepth
                * Mathf.Lerp(0.46f, 0.72f, Hash01(seed, 619));
            Vector3 cliffTangent = Vector3.Cross(anchor.ProfileOutward, Vector3.up);
            float profileDistance = ResolveProfileDistance(cache, anchor, descent, settings) + 0.28f;
            Vector3 buttressBase = anchor.Position
                + anchor.ProfileOutward * profileDistance
                + cliffTangent * SignedHash(seed, 631) * clusterWidth * 0.16f
                + Vector3.down * (descent + 0.22f);
            AddRockVolume(
                mesh,
                buttressBase,
                cliffTangent,
                anchor.ProfileOutward,
                clusterWidth * Mathf.Lerp(0.28f, 0.38f, Hash01(seed, 641)),
                clusterDepth * Mathf.Lerp(0.54f, 0.78f, Hash01(seed, 653)),
                descent + clusterHeight * Mathf.Lerp(0.24f, 0.36f, Hash01(seed, 661)),
                RockArchetype.FracturedButtress,
                seed + 1709);
        }
    }

    private static void AddRockVolume(
        MeshAccumulator mesh,
        Vector3 basePosition,
        Vector3 tangent,
        Vector3 outward,
        float width,
        float depth,
        float height,
        RockArchetype archetype,
        int seed)
    {
        int sideCount = archetype == RockArchetype.FoundationBoulder
            || archetype == RockArchetype.ShelfSlab
                ? 8
                : 7;
        const int ringCount = 6;
        float[] ringHeight;
        float[] ringRadius;
        float leanScale;
        float rotationJitter;
        switch (archetype)
        {
            case RockArchetype.FoundationBoulder:
                ringHeight = new[] { 0f, 0.12f, 0.36f, 0.63f, 0.84f, 1f };
                ringRadius = new[] { 0.72f, 1f, 1.02f, 0.96f, 0.82f, 0.58f };
                leanScale = 0.035f;
                rotationJitter = 0.10f;
                break;
            case RockArchetype.ShelfSlab:
                ringHeight = new[] { 0f, 0.10f, 0.30f, 0.62f, 0.86f, 1f };
                ringRadius = new[] { 0.78f, 1f, 1.03f, 1f, 0.94f, 0.78f };
                leanScale = 0.045f;
                rotationJitter = 0.07f;
                break;
            case RockArchetype.FracturedButtress:
                ringHeight = new[] { 0f, 0.08f, 0.32f, 0.62f, 0.86f, 1f };
                ringRadius = new[] { 0.82f, 1f, 0.95f, 0.87f, 0.75f, 0.54f };
                leanScale = 0.075f;
                rotationJitter = 0.16f;
                break;
            case RockArchetype.RoundedShoulder:
                ringHeight = new[] { 0f, 0.14f, 0.38f, 0.64f, 0.84f, 1f };
                ringRadius = new[] { 0.62f, 0.88f, 1f, 0.92f, 0.70f, 0.32f };
                leanScale = 0.065f;
                rotationJitter = 0.18f;
                break;
            default:
                ringHeight = new[] { 0f, 0.10f, 0.34f, 0.65f, 0.88f, 1f };
                ringRadius = new[] { 0.64f, 1f, 0.84f, 0.58f, 0.31f, 0.06f };
                leanScale = 0.12f;
                rotationJitter = 0.22f;
                break;
        }

        Vector3[,] points = new Vector3[ringCount, sideCount];
        Vector2 lean = new Vector2(SignedHash(seed, 17), SignedHash(seed, 29)) * leanScale;
        float baseRotation = archetype == RockArchetype.ShelfSlab ? Mathf.PI * 0.125f : 0f;
        float noiseAmplitude = archetype == RockArchetype.ShelfSlab ? 0.08f : 0.16f;

        for (int ring = 0; ring < ringCount; ring++)
        {
            float t = ringHeight[ring];
            float rotation = baseRotation
                + SignedHash(seed, 101 + ring * 17) * rotationJitter;
            for (int side = 0; side < sideCount; side++)
            {
                float angle = side / (float)sideCount * Mathf.PI * 2f + rotation;
                float radiusNoise = 1f
                    + SignedHash(seed, 211 + ring * 67 + side * 19) * noiseAmplitude;
                float depthNoise = 1f
                    + SignedHash(seed, 307 + ring * 71 + side * 23) * noiseAmplitude;
                float x = Mathf.Cos(angle) * width * 0.5f * ringRadius[ring] * radiusNoise;
                float z = Mathf.Sin(angle) * depth * 0.5f * ringRadius[ring]
                    * depthNoise;
                Vector3 drift = tangent * (lean.x * width * t) + outward * (lean.y * depth * t);
                points[ring, side] = basePosition
                    + tangent * x
                    + outward * z
                    + drift
                    + Vector3.up * (height * t);
            }
        }

        for (int ring = 0; ring < ringCount - 1; ring++)
        {
            for (int side = 0; side < sideCount; side++)
            {
                int next = (side + 1) % sideCount;
                mesh.AddQuadWorldUv(
                    points[ring, side],
                    points[ring, next],
                    points[ring + 1, next],
                    points[ring + 1, side]);
            }
        }

        Vector3 bottomCenter = basePosition + Vector3.down * 0.02f;
        Vector3 topCenter = Vector3.zero;
        for (int side = 0; side < sideCount; side++) topCenter += points[ringCount - 1, side];
        topCenter /= sideCount;

        for (int side = 0; side < sideCount; side++)
        {
            int next = (side + 1) % sideCount;
            mesh.AddTriangleWorldUv(bottomCenter, points[0, next], points[0, side]);
            mesh.AddTriangleWorldUv(topCenter, points[ringCount - 1, side], points[ringCount - 1, next]);
        }
    }

    private static int FindContourIndex(ContourPoint[] contour, Vector2 center, float angle)
    {
        int bestIndex = 0;
        float bestDelta = float.MaxValue;
        for (int index = 0; index < contour.Length; index++)
        {
            Vector3 point = contour[index].Position;
            float pointAngle = Mathf.Atan2(point.z - center.y, point.x - center.x);
            float delta = Mathf.Abs(Mathf.DeltaAngle(
                pointAngle * Mathf.Rad2Deg,
                angle * Mathf.Rad2Deg));
            if (delta >= bestDelta) continue;
            bestDelta = delta;
            bestIndex = index;
        }

        return bestIndex;
    }

    private static Vector3 MoveInsideTabletop(
        TerrainSampleCache cache,
        Vector3 position,
        Vector3 inward)
    {
        inward.y = 0f;
        if (inward.sqrMagnitude <= 0.0001f) return position;
        inward.Normalize();

        const int maximumAttempts = 12;
        float step = Mathf.Max(0.25f, cache.Step * 1.5f);
        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            if (IsTabletop(cache, position.x, position.z)) break;
            position += inward * step;
        }

        return position;
    }

    private static bool IsTabletop(TerrainSampleCache cache, float x, float z)
    {
        int sampleX = Mathf.Clamp(Mathf.RoundToInt(x / cache.Step), 0, cache.Resolution - 1);
        int sampleZ = Mathf.Clamp(Mathf.RoundToInt(z / cache.Step), 0, cache.Resolution - 1);
        return cache.TerrainTypes[cache.GetIndex(sampleX, sampleZ)] == Cell.TerrainType.Plateau;
    }

    private static float SampleHeight(TerrainSampleCache cache, float x, float z)
    {
        float sampleX = Mathf.Clamp(x / cache.Step, 0f, cache.Resolution - 1f);
        float sampleZ = Mathf.Clamp(z / cache.Step, 0f, cache.Resolution - 1f);
        int x0 = Mathf.FloorToInt(sampleX);
        int z0 = Mathf.FloorToInt(sampleZ);
        int x1 = Mathf.Min(cache.Resolution - 1, x0 + 1);
        int z1 = Mathf.Min(cache.Resolution - 1, z0 + 1);
        float tx = sampleX - x0;
        float tz = sampleZ - z0;
        float lower = Mathf.Lerp(cache.GetHeight(x0, z0), cache.GetHeight(x1, z0), tx);
        float upper = Mathf.Lerp(cache.GetHeight(x0, z1), cache.GetHeight(x1, z1), tx);
        return Mathf.Lerp(lower, upper, tz);
    }

    private static int CombineSeed(int chunkSeed, int worldSeed, int salt)
    {
        unchecked
        {
            return chunkSeed * 486187739 ^ worldSeed * 16777619 ^ salt * 374761393;
        }
    }

    private static float SeedUnit(int chunkSeed, int worldSeed, int salt)
    {
        unchecked
        {
            uint value = (uint)(chunkSeed * 73856093 ^ worldSeed * 19349663 ^ salt * 83492791);
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static float SignedSeed(int chunkSeed, int worldSeed, int salt)
    {
        return SeedUnit(chunkSeed, worldSeed, salt) * 2f - 1f;
    }

    private static float Hash01(int seed, int salt)
    {
        unchecked
        {
            uint value = (uint)(seed ^ salt * 0x45d9f3b);
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static float SignedHash(int seed, int salt)
    {
        return Hash01(seed, salt) * 2f - 1f;
    }

    private sealed class MeshAccumulator
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Vector2> uvs = new List<Vector2>();

        public void AddQuad(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector2 uvA,
            Vector2 uvB,
            Vector2 uvC,
            Vector2 uvD)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            uvs.Add(uvA);
            uvs.Add(uvB);
            uvs.Add(uvC);
            uvs.Add(uvD);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        public void AddQuadWorldUv(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddQuad(a, b, c, d, WorldUv(a), WorldUv(b), WorldUv(c), WorldUv(d));
        }

        public void AddTriangleWorldUv(Vector3 a, Vector3 b, Vector3 c)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            uvs.Add(WorldUv(a));
            uvs.Add(WorldUv(b));
            uvs.Add(WorldUv(c));
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        public PlateauGeneratedMeshData ToMeshData()
        {
            return new PlateauGeneratedMeshData(
                vertices.ToArray(),
                triangles.ToArray(),
                uvs.ToArray());
        }

        private static Vector2 WorldUv(Vector3 point)
        {
            return new Vector2(point.x, point.z) * 0.18f;
        }
    }
}
