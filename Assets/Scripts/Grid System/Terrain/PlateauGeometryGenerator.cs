// tldr: Builds the VOLUMETRIC rock meshes for a plateau - escarpment strata, spires, perimeter clusters -
// which the scalar heightfield alone cannot represent.

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
        SteppedTerrace,
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
        // Build geological cliff-base talus scree & angular fallen rock aprons along
        // steep rock escarpment toes instead of an overlapping vertical quad ribbon.
        // The continuous heightfield owns the stratified wall itself, preventing all
        // sawtooth skirt artifacts and heightfield z-fighting.
        int stride = Mathf.Max(2, contour.Length / 36);
        float upperDrop = Mathf.Max(0.5f, settings.cliffDropDepth);

        for (int index = 0; index < contour.Length; index += stride)
        {
            ContourPoint anchor = contour[index];
            PlateauSampleData rimData = SamplePlateauData(cache, anchor.Position.x, anchor.Position.z);
            if (rimData.IsDefined && rimData.SandWeight > rimData.RockWeight + 0.08f)
            {
                continue;
            }

            Vector3 outward = anchor.ProfileOutward;
            Vector3 tangent = Vector3.Cross(outward, Vector3.up);
            float toeDrop = upperDrop * Mathf.Lerp(0.62f, 0.96f, SeedUnit(chunkSeed, worldSeed, 1103 + index * 17));
            float toeDistance = ResolveProfileDistance(cache, anchor, toeDrop, settings);
            float slopeDrop = upperDrop * Mathf.Lerp(0.38f, 0.65f, SeedUnit(chunkSeed, worldSeed, 1129 + index * 17));
            float midSlopeDistance = ResolveProfileDistance(cache, anchor, slopeDrop, settings);

            int moundSeed = CombineSeed(chunkSeed, worldSeed, 1409 + index * 53);
            int fragmentCount = 3 + Mathf.FloorToInt(Hash01(moundSeed, 13) * 3f);
            for (int fragment = 0; fragment < fragmentCount; fragment++)
            {
                int fragSeed = moundSeed + fragment * 197;
                bool isMidLedge = fragment == 0 && Hash01(fragSeed, 23) < 0.55f;
                float radialDist = isMidLedge
                    ? midSlopeDistance + SignedHash(fragSeed, 31) * 0.45f
                    : toeDistance + Hash01(fragSeed, 37) * 1.45f;
                float lateralOffset = SignedHash(fragSeed, 43) * 1.55f;
                Vector3 pos = anchor.Position + outward * radialDist + tangent * lateralOffset;
                PlateauSampleData localData = SamplePlateauData(cache, pos.x, pos.z);
                if (localData.IsDefined && localData.SandWeight > 0.68f)
                {
                    continue;
                }

                float groundY = SampleHeight(cache, pos.x, pos.z);
                float sizeScale = isMidLedge ? 1.12f : Mathf.Lerp(0.68f, 1.32f, Hash01(fragSeed, 59));
                float w = Mathf.Lerp(0.85f, 1.85f, Hash01(fragSeed, 61)) * sizeScale;
                float d = Mathf.Lerp(0.75f, 1.65f, Hash01(fragSeed, 67)) * sizeScale;
                float h = Mathf.Lerp(0.55f, 1.28f, Hash01(fragSeed, 71)) * sizeScale;
                // Sink well into the talus slope so undersides never hover above steep triangles.
                Vector3 basePos = new Vector3(pos.x, groundY - h * 0.42f, pos.z);
                Vector3 tiltDir = (outward * 0.32f + Vector3.up).normalized;

                AddAngularTalusBoulder(
                    mesh,
                    basePos,
                    tangent,
                    outward,
                    tiltDir,
                    w,
                    d,
                    h,
                    fragSeed);
            }
        }
    }

    private static float ResolveProfileDistance(
        TerrainSampleCache cache,
        ContourPoint point,
        float drop,
        StandalonePlateauSettings settings)
    {
        if (drop <= 0.0001f) return 0f;

        float targetHeight = point.Position.y - drop;
        float baseUpper = Mathf.Max(settings.upperEscarpmentWidth, 4.4f);
        float baseLower = Mathf.Max(settings.lowerApronWidth, 7.2f);
        float analyticMaximum = baseUpper * (1f + settings.profileAsymmetry + 0.14f)
            + baseLower * (1f + settings.profileAsymmetry * 0.42f + 0.14f);
        float distanceToEdge = DistanceToChunkEdge(
            point.Position,
            point.ProfileOutward,
            cache.GridSize);
        float scanLimit = Mathf.Min(
            distanceToEdge,
            analyticMaximum + Mathf.Max(0.8f, cache.Step * 2f));
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

        return Mathf.Min(distanceToEdge, scanLimit * 0.5f);
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
                ContourPoint anchor = contour[contourIndex];
                PlateauSampleData anchorData = SamplePlateauData(cache, anchor.Position.x, anchor.Position.z);
                if (anchorData.IsDefined && anchorData.SandWeight > 0.62f && anchorData.RockWeight < 0.34f)
                {
                    continue;
                }

                float width = settings.perimeterClusterWidth
                    * Mathf.Lerp(0.68f, 1.32f, SeedUnit(chunkSeed, worldSeed, 541 + cluster * 31));
                float depth = settings.perimeterClusterDepth
                    * Mathf.Lerp(0.72f, 1.30f, SeedUnit(chunkSeed, worldSeed, 563 + cluster * 31));
                float height = settings.perimeterClusterHeight
                    * Mathf.Lerp(0.62f, 1.18f, SeedUnit(chunkSeed, worldSeed, 577 + cluster * 31));

                AddRockCluster(
                    mesh,
                    cache,
                    anchor,
                    width,
                    depth,
                    height,
                    settings,
                    settings.rocksPerCluster,
                    CombineSeed(chunkSeed, worldSeed, 3001 + cluster * 97));
            }
        }

        for (int landmark = 0; landmark < settings.perimeterSpireCount; landmark++)
        {
            if (settings.occasionalSpireHeight <= 0f) break;
            float angle = SeedUnit(chunkSeed, worldSeed, 701 + landmark * 43) * Mathf.PI * 2f;
            int contourIndex = FindContourIndex(contour, center, angle);
            ContourPoint anchor = contour[contourIndex];
            PlateauSampleData anchorData = SamplePlateauData(cache, anchor.Position.x, anchor.Position.z);
            if (anchorData.IsDefined && anchorData.SandWeight > 0.58f && anchorData.RockWeight < 0.38f)
            {
                continue;
            }

            Vector3 tangent = Vector3.Cross(anchor.Outward, Vector3.up);
            float width = settings.spireBaseWidth
                * Mathf.Lerp(0.84f, 1.36f, SeedUnit(chunkSeed, worldSeed, 719 + landmark * 43));
            float height = settings.occasionalSpireHeight
                * Mathf.Lerp(0.62f, 1.02f, SeedUnit(chunkSeed, worldSeed, 751 + landmark * 43));
            float inset = Mathf.Min(settings.rockyRimWidth * 0.42f, width * 0.36f);
            Vector3 basePosition = anchor.Position
                - anchor.Outward * inset
                + tangent * SignedSeed(chunkSeed, worldSeed, 3301 + landmark * 61) * width * 0.22f;
            basePosition = EnsureNonBuildableRimPosition(cache, basePosition, anchor.Outward);
            basePosition.y = Mathf.Min(
                anchor.Position.y,
                SampleHeight(cache, basePosition.x, basePosition.z)) - 0.85f;

            // Replace pointed 7-sided cones with clustered hexagonal/pentagonal basalt columns
            // rising from a stepped bedrock shoulder.
            AddRockVolume(
                mesh,
                basePosition,
                tangent,
                anchor.Outward,
                width * 1.18f,
                width * 0.92f,
                height * 0.38f + 0.65f,
                RockArchetype.SteppedTerrace,
                CombineSeed(chunkSeed, worldSeed, 3407 + landmark * 73));
            AddColumnarBasaltBundle(
                mesh,
                basePosition + Vector3.up * 0.32f,
                tangent,
                anchor.Outward,
                width * 0.88f,
                height + 0.55f,
                CombineSeed(chunkSeed, worldSeed, 3511 + landmark * 73));
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

        // Each perimeter cluster is one overlapping geological headland massing: a deep
        // stratified foundation shelf, stepped rock buttresses along the non-buildable
        // rim collar, and a fractured cliff rib descending into the upper escarpment.
        float foundationInset = Mathf.Min(
            settings.rockyRimWidth * 0.44f,
            clusterDepth * 0.36f);
        Vector3 foundationBase = anchor.Position - anchor.Outward * foundationInset;
        foundationBase = EnsureNonBuildableRimPosition(cache, foundationBase, anchor.Outward);
        foundationBase.y = Mathf.Min(
            anchor.Position.y,
            SampleHeight(cache, foundationBase.x, foundationBase.z)) - 0.78f;
        AddRockVolume(
            mesh,
            foundationBase,
            tangent,
            anchor.Outward,
            clusterWidth * 0.94f,
            clusterDepth * 1.08f,
            clusterHeight * Mathf.Lerp(0.40f, 0.52f, Hash01(seed, 379)) + 0.55f,
            RockArchetype.SteppedTerrace,
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
                archetype = Hash01(seed, 421) < 0.62f
                    ? RockArchetype.FracturedButtress
                    : RockArchetype.SteppedTerrace;
            }
            else
            {
                switch (support % 3)
                {
                    case 0:
                        archetype = RockArchetype.ShelfSlab;
                        break;
                    case 1:
                        archetype = RockArchetype.SteppedTerrace;
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
                    height = clusterHeight * Mathf.Lerp(0.24f, 0.36f, Hash01(seed, 541 + support * 31));
                    break;
                case RockArchetype.FracturedButtress:
                    width = clusterWidth * Mathf.Lerp(0.30f, 0.44f, Hash01(seed, 467 + support * 23));
                    depth = clusterDepth * Mathf.Lerp(0.48f, 0.72f, Hash01(seed, 499 + support * 29));
                    height = clusterHeight * Mathf.Lerp(0.44f, 0.62f, Hash01(seed, 541 + support * 31));
                    break;
                default:
                    width = clusterWidth * Mathf.Lerp(0.38f, 0.56f, Hash01(seed, 467 + support * 23));
                    depth = clusterDepth * Mathf.Lerp(0.50f, 0.74f, Hash01(seed, 499 + support * 29));
                    height = clusterHeight * Mathf.Lerp(0.36f, 0.54f, Hash01(seed, 541 + support * 31));
                    break;
            }

            if (isDominant)
            {
                width = Mathf.Max(width, clusterWidth * 0.44f);
                depth = Mathf.Max(depth, clusterDepth * 0.58f);
                height = clusterHeight
                    * Mathf.Lerp(0.80f, 1.04f, Hash01(seed, 593))
                    * Mathf.Lerp(0.92f, 1.08f, Mathf.Clamp01(centerBias));
            }

            float semanticInset = settings.rockyRimWidth
                * Mathf.Lerp(0.28f, 0.52f, Hash01(seed, 433 + support * 19));
            float inward = Mathf.Min(semanticInset, depth * 0.42f + 0.10f);
            Vector3 basePosition = anchor.Position
                + tangent * lateral
                - anchor.Outward * inward;
            basePosition = EnsureNonBuildableRimPosition(cache, basePosition, anchor.Outward);
            PlateauSampleData localData = SamplePlateauData(cache, basePosition.x, basePosition.z);
            if (localData.IsDefined && (localData.BuildableWeight > 0.32f || localData.SandWeight > 0.64f))
            {
                continue;
            }

            basePosition.y = Mathf.Min(
                anchor.Position.y,
                SampleHeight(cache, basePosition.x, basePosition.z)) - 0.56f;

            if (isDominant && Hash01(seed, 607) > 0.48f)
            {
                AddColumnarBasaltBundle(
                    mesh,
                    basePosition + Vector3.up * 0.15f,
                    tangent,
                    anchor.Outward,
                    Mathf.Min(width, depth) * 0.64f,
                    height + 0.50f,
                    seed + support * 1013 + 79);
            }
            else
            {
                AddRockVolume(
                    mesh,
                    basePosition,
                    tangent,
                    anchor.Outward,
                    width,
                    depth,
                    height + 0.48f,
                    archetype,
                    seed + support * 1013);
            }
        }

        // Most headlands extend a buttress rib down into the upper escarpment so rim rock
        // and cliff wall read as one folded bedrock structure.
        if (Hash01(seed, 613) > 0.24f)
        {
            float descent = settings.cliffDropDepth
                * Mathf.Lerp(0.46f, 0.72f, Hash01(seed, 619));
            Vector3 cliffTangent = Vector3.Cross(anchor.ProfileOutward, Vector3.up);
            float profileDistance = ResolveProfileDistance(cache, anchor, descent, settings) + 0.24f;
            Vector3 buttressBase = anchor.Position
                + anchor.ProfileOutward * profileDistance
                + cliffTangent * SignedHash(seed, 631) * clusterWidth * 0.16f
                + Vector3.down * (descent + 0.45f);
            AddRockVolume(
                mesh,
                buttressBase,
                cliffTangent,
                anchor.ProfileOutward,
                clusterWidth * Mathf.Lerp(0.28f, 0.40f, Hash01(seed, 641)),
                clusterDepth * Mathf.Lerp(0.54f, 0.78f, Hash01(seed, 653)),
                descent + clusterHeight * Mathf.Lerp(0.26f, 0.40f, Hash01(seed, 661)),
                RockArchetype.FracturedButtress,
                seed + 1709);
        }
    }

    private static void AddColumnarBasaltBundle(
        MeshAccumulator mesh,
        Vector3 centerBase,
        Vector3 tangent,
        Vector3 outward,
        float radius,
        float maxHeight,
        int seed)
    {
        int columnCount = 6 + Mathf.FloorToInt(Hash01(seed, 17) * 4f);
        float goldenAngle = 2.39996323f;

        for (int index = 0; index < columnCount; index++)
        {
            int colSeed = seed + index * 313;
            float norm = index / (float)Mathf.Max(1, columnCount - 1);
            float radial = index == 0 ? 0f : Mathf.Sqrt(norm) * radius * Mathf.Lerp(0.42f, 0.86f, Hash01(colSeed, 23));
            float theta = index * goldenAngle + Hash01(colSeed, 29) * 0.45f;
            Vector3 offset = tangent * (Mathf.Cos(theta) * radial) + outward * (Mathf.Sin(theta) * radial);

            float radialFalloff = 1f - Mathf.Clamp01(radial / Mathf.Max(0.1f, radius));
            float columnHeight = maxHeight
                * Mathf.Lerp(0.36f, 1.0f, radialFalloff * 0.72f + Hash01(colSeed, 37) * 0.28f);
            float columnRadius = radius * Mathf.Lerp(0.22f, 0.36f, Hash01(colSeed, 41));
            int sides = Hash01(colSeed, 47) < 0.65f ? 6 : 5;
            Vector3 tiltAxis = (tangent * SignedHash(colSeed, 53) + outward * SignedHash(colSeed, 59) + Vector3.up * 6.5f).normalized;
            float topSlantX = SignedHash(colSeed, 61) * 0.16f;
            float topSlantZ = SignedHash(colSeed, 67) * 0.16f;

            AddBasaltPrismColumn(
                mesh,
                centerBase + offset,
                tangent,
                outward,
                tiltAxis,
                columnRadius,
                columnHeight,
                sides,
                topSlantX,
                topSlantZ,
                colSeed);
        }
    }

    private static void AddBasaltPrismColumn(
        MeshAccumulator mesh,
        Vector3 basePos,
        Vector3 tangent,
        Vector3 outward,
        Vector3 upAxis,
        float radius,
        float height,
        int sides,
        float slantX,
        float slantZ,
        int seed)
    {
        float rotation = Hash01(seed, 71) * Mathf.PI * 2f;
        Vector3[] bottomRing = new Vector3[sides];
        Vector3[] midRing = new Vector3[sides];
        Vector3[] topRing = new Vector3[sides];
        Vector3[] crownRing = new Vector3[sides];

        for (int side = 0; side < sides; side++)
        {
            float angle = rotation + side / (float)sides * Mathf.PI * 2f;
            float jitter = Mathf.Lerp(0.84f, 1.16f, Hash01(seed, 101 + side * 19));
            float cx = Mathf.Cos(angle) * radius * jitter;
            float cz = Mathf.Sin(angle) * radius * jitter;
            Vector3 radialVec = tangent * cx + outward * cz;
            float slantDelta = (Mathf.Cos(angle) * slantX + Mathf.Sin(angle) * slantZ) * radius;
            float topHeight = Mathf.Max(0.25f, height + slantDelta);

            bottomRing[side] = basePos + radialVec * 1.12f;
            midRing[side] = basePos + upAxis * (topHeight * 0.62f) + radialVec * 0.98f;
            topRing[side] = basePos + upAxis * (topHeight * 0.92f) + radialVec * 0.92f;
            crownRing[side] = basePos + upAxis * topHeight + radialVec * 0.72f;
        }

        Vector3 topCenter = Vector3.zero;
        for (int side = 0; side < sides; side++) topCenter += crownRing[side];
        topCenter /= sides;

        for (int side = 0; side < sides; side++)
        {
            int next = (side + 1) % sides;
            mesh.AddQuadWorldUv(bottomRing[side], bottomRing[next], midRing[next], midRing[side]);
            mesh.AddQuadWorldUv(midRing[side], midRing[next], topRing[next], topRing[side]);
            mesh.AddQuadWorldUv(topRing[side], topRing[next], crownRing[next], crownRing[side]);
            mesh.AddTriangleWorldUv(topCenter, crownRing[side], crownRing[next]);
        }
    }

    private static void AddAngularTalusBoulder(
        MeshAccumulator mesh,
        Vector3 basePos,
        Vector3 tangent,
        Vector3 outward,
        Vector3 upAxis,
        float width,
        float depth,
        float height,
        int seed)
    {
        int sides = 6;
        float[] ringHeights = { 0f, 0.42f, 0.80f, 1f };
        float[] ringRadii = { 0.88f, 1.05f, 0.82f, 0.42f };
        Vector3[,] points = new Vector3[ringHeights.Length, sides];
        float baseRot = Hash01(seed, 19) * Mathf.PI * 2f;

        for (int ring = 0; ring < ringHeights.Length; ring++)
        {
            float t = ringHeights[ring];
            float twist = baseRot + SignedHash(seed, 43 + ring * 23) * 0.24f;
            for (int side = 0; side < sides; side++)
            {
                float angle = twist + side / (float)sides * Mathf.PI * 2f;
                float rScale = ringRadii[ring] * Mathf.Lerp(0.76f, 1.24f, Hash01(seed, 97 + ring * 31 + side * 13));
                float rx = Mathf.Cos(angle) * width * 0.5f * rScale;
                float rz = Mathf.Sin(angle) * depth * 0.5f * rScale;
                float slantY = height * (t + SignedHash(seed, 149 + side * 17) * 0.08f);
                points[ring, side] = basePos + tangent * rx + outward * rz + upAxis * slantY;
            }
        }

        for (int ring = 0; ring < ringHeights.Length - 1; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                mesh.AddQuadWorldUv(
                    points[ring, side],
                    points[ring, next],
                    points[ring + 1, next],
                    points[ring + 1, side]);
            }
        }

        Vector3 topCenter = Vector3.zero;
        for (int side = 0; side < sides; side++) topCenter += points[ringHeights.Length - 1, side];
        topCenter /= sides;
        for (int side = 0; side < sides; side++)
        {
            int next = (side + 1) % sides;
            mesh.AddTriangleWorldUv(topCenter, points[ringHeights.Length - 1, side], points[ringHeights.Length - 1, next]);
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
            || archetype == RockArchetype.SteppedTerrace
                ? 7
                : 6;
        const int ringCount = 6;
        float[] ringHeight;
        float[] ringRadius;
        float leanScale;
        float rotationJitter;
        switch (archetype)
        {
            case RockArchetype.FoundationBoulder:
                ringHeight = new[] { 0f, 0.14f, 0.40f, 0.68f, 0.88f, 1f };
                ringRadius = new[] { 0.86f, 1f, 1.02f, 0.92f, 0.74f, 0.46f };
                leanScale = 0.04f;
                rotationJitter = 0.14f;
                break;
            case RockArchetype.ShelfSlab:
                ringHeight = new[] { 0f, 0.12f, 0.34f, 0.64f, 0.88f, 1f };
                ringRadius = new[] { 0.88f, 1f, 1.02f, 0.96f, 0.88f, 0.70f };
                leanScale = 0.04f;
                rotationJitter = 0.09f;
                break;
            case RockArchetype.FracturedButtress:
                ringHeight = new[] { 0f, 0.12f, 0.38f, 0.62f, 0.84f, 1f };
                ringRadius = new[] { 0.92f, 1f, 0.90f, 0.80f, 0.66f, 0.46f };
                leanScale = 0.06f;
                rotationJitter = 0.16f;
                break;
            case RockArchetype.SteppedTerrace:
                // Horizontal rock bench steps echoing sedimentary shelf faces.
                ringHeight = new[] { 0f, 0.24f, 0.36f, 0.66f, 0.78f, 1f };
                ringRadius = new[] { 1.04f, 1f, 0.82f, 0.78f, 0.60f, 0.48f };
                leanScale = 0.04f;
                rotationJitter = 0.10f;
                break;
            default:
                ringHeight = new[] { 0f, 0.14f, 0.38f, 0.64f, 0.84f, 1f };
                ringRadius = new[] { 0.78f, 0.94f, 1f, 0.88f, 0.66f, 0.36f };
                leanScale = 0.055f;
                rotationJitter = 0.18f;
                break;
        }

        Vector3[,] points = new Vector3[ringCount, sideCount];
        Vector2 lean = new Vector2(SignedHash(seed, 17), SignedHash(seed, 29)) * leanScale;
        float baseRotation = Hash01(seed, 53) * Mathf.PI * 2f;
        float noiseAmplitude = archetype == RockArchetype.ShelfSlab || archetype == RockArchetype.SteppedTerrace
            ? 0.14f
            : 0.20f;

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

    private static Vector3 EnsureNonBuildableRimPosition(
        TerrainSampleCache cache,
        Vector3 position,
        Vector3 outward)
    {
        outward.y = 0f;
        if (outward.sqrMagnitude <= 0.0001f) return position;
        outward.Normalize();

        float step = Mathf.Max(0.25f, cache.Step * 1.5f);
        const int maximumAttempts = 10;
        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            if (!IsTabletop(cache, position.x, position.z))
            {
                position -= outward * step;
                break;
            }

            PlateauSampleData data = SamplePlateauData(cache, position.x, position.z);
            if (!data.IsDefined || data.BuildableWeight <= 0.22f)
            {
                break;
            }

            // Push slightly outward toward the rim collar so formations frame buildable basins
            // rather than burying buildable tiles.
            position += outward * step;
        }

        return position;
    }

    private static PlateauSampleData SamplePlateauData(TerrainSampleCache cache, float x, float z)
    {
        if (cache.PlateauData == null) return default;
        int sampleX = Mathf.Clamp(Mathf.RoundToInt(x / cache.Step), 0, cache.Resolution - 1);
        int sampleZ = Mathf.Clamp(Mathf.RoundToInt(z / cache.Step), 0, cache.Resolution - 1);
        return cache.PlateauData[cache.GetIndex(sampleX, sampleZ)];
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
