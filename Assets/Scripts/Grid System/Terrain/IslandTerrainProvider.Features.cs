using UnityEngine;
using System.Collections.Generic;

public partial class IslandTerrainProvider
{


private TerrainSample ApplyCoastClassification(float x, float z, TerrainSample sample)
{
    if (sample.TerrainType != Cell.TerrainType.Land) return sample;

    // Beach comes from the natural island shoreline only. Neighbours are classified
    // before any plateau adjustment, so a deliberate underwater plateau can never
    // create, move, or erase a beach. This is also why the neighbour test is cheap:
    // it does not re-evaluate every plateau region four times per sample.
    if (IsNaturalUnderwaterNeighbor(x - 1f, z)
        || IsNaturalUnderwaterNeighbor(x + 1f, z)
        || IsNaturalUnderwaterNeighbor(x, z - 1f)
        || IsNaturalUnderwaterNeighbor(x, z + 1f))
    {
        return new TerrainSample(Cell.TerrainType.Beach, sample.Height, sample.SourceValue, sample.PlateauInfluence);
    }

    return sample;
}


private bool IsNaturalUnderwaterNeighbor(float x, float z)
{
    return IsUnderwaterTerrain(SampleSynthesizedIsland(x, z).TerrainType);
}

private FeatureReservationMap BuildFeatureReservations(int seed)
{
    FeatureReservationMap map = new FeatureReservationMap();
    if (gridType != GridType.Type.Island) return map;

    System.Random random = new System.Random(unchecked(seed * 486187739 ^ 0x3F1A4E7D));
    float halfSize = (size - 1f) * 0.5f;
    Vector2 mapCenter = new Vector2(halfSize, halfSize);

    // 1. Analyze Coastline by raycasting from center in polar directions
    const int rayCount = 48;
    float[] coastRadii = new float[rayCount];
    Vector2[] coastPoints = new Vector2[rayCount];
    Vector2[] coastNormals = new Vector2[rayCount];

    for (int i = 0; i < rayCount; i++)
    {
        float angle = i / (float)rayCount * Mathf.PI * 2f;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float foundRadius = halfSize * 0.7f;

        // March outward from center to find transition to water
        for (float r = 1f; r <= halfSize * 1.2f; r += 0.5f)
        {
            Vector2 p = mapCenter + dir * r;
            float field = CalculateLegacyIslandField(p.x, p.y);
            if (field < settings.waterUpper)
            {
                foundRadius = r;
                break;
            }
        }

        coastRadii[i] = foundRadius;
        coastPoints[i] = mapCenter + dir * foundRadius;
        coastNormals[i] = dir;
    }

    // Identify Promontories (convex headlands) and Bays (concave inlets)
    List<int> headlandRays = new List<int>();
    List<int> bayRays = new List<int>();

    for (int i = 0; i < rayCount; i++)
    {
        int prev = (i - 1 + rayCount) % rayCount;
        int next = (i + 1) % rayCount;
        float diff = coastRadii[i] - (coastRadii[prev] + coastRadii[next]) * 0.5f;
        if (diff > 0.5f) headlandRays.Add(i);
        else if (diff < -0.5f) bayRays.Add(i);
    }

    // 2. Harbors / Flat Bays Reservation
    HarborReservationSettings harborSettings = settings.harborReservations;
    if (harborSettings.enabled && harborSettings.maxHarbors > 0)
    {
        int harborCount = random.Next(harborSettings.minHarbors, harborSettings.maxHarbors + 1);
        List<int> availableBays = new List<int>(bayRays.Count > 0 ? bayRays : new int[] { 0, rayCount / 2 });

        for (int h = 0; h < harborCount && availableBays.Count > 0; h++)
        {
            int pickIdx = random.Next(availableBays.Count);
            int rayIdx = availableBays[pickIdx];
            availableBays.RemoveAt(pickIdx);

            Vector2 coastPt = coastPoints[rayIdx];
            Vector2 normal = coastNormals[rayIdx];
            Vector2 harborCenter = coastPt - normal * 3f; // slightly inland from shoreline
            float harborRadius = RandomRange(random, harborSettings.minHarborRadius, harborSettings.maxHarborRadius);

            map.AddHarbor(new FeatureReservationMap.HarborPad(harborCenter, harborRadius, settings.surfaceFlatlandHeight));
        }
    }

    // 3. River Corridors Reservation
    RiverCorridorSettings riverSettings = settings.riverCorridors;
    if (riverSettings.enabled && riverSettings.maxRivers > 0)
    {
        int riverCount = random.Next(riverSettings.minRivers, riverSettings.maxRivers + 1);
        for (int r = 0; r < riverCount; r++)
        {
            // Pick source in interior
            float sourceAngle = RandomRange(random, 0f, Mathf.PI * 2f);
            float sourceDist = RandomRange(random, 3f, halfSize * 0.35f);
            Vector2 source = mapCenter + new Vector2(Mathf.Cos(sourceAngle), Mathf.Sin(sourceAngle)) * sourceDist;

            // Pick exit point along coast (prefer bays or non-mountain sectors)
            int exitRay = random.Next(rayCount);
            if (bayRays.Count > 0 && random.NextDouble() < 0.7)
            {
                exitRay = bayRays[random.Next(bayRays.Count)];
            }
            Vector2 mouth = coastPoints[exitRay] + coastNormals[exitRay] * 2f; // extends out into water

            FeatureReservationMap.RiverCorridor corridor = new FeatureReservationMap.RiverCorridor(
                riverSettings.channelRadius,
                riverSettings.clearanceRadius,
                riverSettings.valleyDepth);

            // Generate meandering waypoints
            int waypointCount = Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(source, mouth) / 4f), 4, 12);
            Vector2 riverDir = (mouth - source).normalized;
            Vector2 riverPerp = new Vector2(-riverDir.y, riverDir.x);

            corridor.Waypoints.Add(new FeatureReservationMap.RiverWaypoint(source, riverSettings.channelRadius));
            for (int w = 1; w < waypointCount - 1; w++)
            {
                float t = w / (float)(waypointCount - 1);
                float meander = Mathf.Sin(t * Mathf.PI * 2f + (float)random.NextDouble()) * RandomRange(random, -3f, 3f);
                Vector2 wp = Vector2.Lerp(source, mouth, t) + riverPerp * meander;
                corridor.Waypoints.Add(new FeatureReservationMap.RiverWaypoint(wp, riverSettings.channelRadius));
            }
            corridor.Waypoints.Add(new FeatureReservationMap.RiverWaypoint(mouth, riverSettings.channelRadius * 1.3f));

            map.AddRiver(corridor);
        }
    }

    // 4. Coastal Mountain Ridges Allocation
    CoastalMountainSettings mountainSettings = settings.coastalMountains;
    if (mountainSettings.enabled && mountainSettings.maxRidges > 0)
    {
        int ridgeCount = random.Next(mountainSettings.minRidges, mountainSettings.maxRidges + 1);
        List<int> candidateHeadlands = new List<int>(headlandRays.Count > 0 ? headlandRays : new int[] { rayCount / 4, rayCount * 3 / 4 });

        for (int m = 0; m < ridgeCount && candidateHeadlands.Count > 0; m++)
        {
            int pickIdx = random.Next(candidateHeadlands.Count);
            int rayIdx = candidateHeadlands[pickIdx];
            candidateHeadlands.RemoveAt(pickIdx);

            Vector2 coastPt = coastPoints[rayIdx];
            Vector2 normal = coastNormals[rayIdx];
            Vector2 tangent = new Vector2(-normal.y, normal.x);

            // Ridge origin sits on or near the headland/coast
            float ridgeLength = RandomRange(random, mountainSettings.minRidgeLength, mountainSettings.maxRidgeLength);
            float ridgeWidth = RandomRange(random, mountainSettings.minRidgeWidth, mountainSettings.maxRidgeWidth);
            float peakHeight = mountainSettings.ridgePeakHeight * RandomRange(random, 0.9f, 1.25f);

            // Direction can follow the shoreline tangent or reach from interior out onto the headland
            Vector2 ridgeDir = (tangent * RandomRange(random, 0.4f, 1f) + normal * RandomRange(random, 0.2f, 0.6f)).normalized;
            Vector2 origin = coastPt - ridgeDir * (ridgeLength * 0.4f);

            map.AddRidge(new FeatureReservationMap.CoastalRidge(
                origin,
                ridgeDir,
                ridgeLength,
                ridgeWidth,
                peakHeight,
                mountainSettings.cliffSharpness));
        }

        // Also add 1-2 interior mountain ridges
        int interiorRidges = random.Next(1, 3);
        for (int ir = 0; ir < interiorRidges; ir++)
        {
            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            float dist = RandomRange(random, 2f, halfSize * 0.35f);
            Vector2 origin = mapCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            float dirAngle = RandomRange(random, 0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(dirAngle), Mathf.Sin(dirAngle));
            float len = RandomRange(random, mountainSettings.minRidgeLength * 0.8f, mountainSettings.maxRidgeLength);
            float wid = RandomRange(random, mountainSettings.minRidgeWidth, mountainSettings.maxRidgeWidth);
            float h = mountainSettings.ridgePeakHeight * RandomRange(random, 0.95f, 1.3f);

            map.AddRidge(new FeatureReservationMap.CoastalRidge(origin, dir, len, wid, h, mountainSettings.cliffSharpness));
        }
    }

    return map;
}


private TerrainSample ApplyUnderwaterPlateauRegions(float x, float z, TerrainSample baseSample)
{
    if (!IsDeepOffshoreTerrain(baseSample.TerrainType) || plateauRegions.Count == 0)
    {
        return baseSample;
    }

    float strongestInfluence = 0f;
    float plateauHeight = settings.underwaterPlateauHeight;

    for (int i = 0; i < plateauRegions.Count; i++)
    {
        UnderwaterPlateauRegion region = plateauRegions[i];
        float influence = region.CalculateInfluence(x, z);
        if (influence > strongestInfluence)
        {
            strongestInfluence = influence;
            plateauHeight = region.Height;
        }
    }

    if (strongestInfluence <= 0f)
    {
        return baseSample;
    }

    float height = Mathf.Lerp(baseSample.Height, plateauHeight, strongestInfluence);
    Cell.TerrainType terrainType = strongestInfluence >= FullPlateauInfluence
        ? Cell.TerrainType.Plateau
        : baseSample.TerrainType;

    return new TerrainSample(terrainType, height, baseSample.SourceValue, strongestInfluence);
}


private List<UnderwaterPlateauRegion> BuildUnderwaterPlateauRegions(int seed)
{
    UnderwaterPlateauGenerationSettings plateauSettings = settings.underwaterPlateaus;
    List<UnderwaterPlateauRegion> regions = new List<UnderwaterPlateauRegion>();
    if (plateauSettings.maximumCount <= 0) return regions;

    System.Random random = new System.Random(unchecked(seed * 486187739 ^ 0x2C9277B5));
    int desiredCount = random.Next(plateauSettings.minimumCount, plateauSettings.maximumCount + 1);
    int maximumAttempts = plateauSettings.candidateAttemptsPerRegion * Mathf.Max(1, desiredCount);
    float halfSize = (size - 1f) * 0.5f;
    Vector2 mapCenter = new Vector2(halfSize, halfSize);
    int[] rejections = new int[(int)PlateauRejection.Count];
    int acceptedCoreCells = 0;

    for (int attempt = 0; attempt < maximumAttempts && regions.Count < desiredCount; attempt++)
    {
        float majorRadius = RandomRange(random, plateauSettings.minimumRadius, plateauSettings.maximumRadius);
        float aspectRatio = RandomRange(random, plateauSettings.minimumAspectRatio, plateauSettings.maximumAspectRatio);
        float minorRadius = majorRadius * aspectRatio;
        bool swapAxes = random.Next(0, 2) == 0;
        float radiusX = swapAxes ? majorRadius : minorRadius;
        float radiusZ = swapAxes ? minorRadius : majorRadius;
        float angle = RandomRange(random, 0f, Mathf.PI * 2f);
        float placementDistance = RandomRange(
            random,
            plateauSettings.minimumPlacementDistance,
            plateauSettings.maximumPlacementDistance) * halfSize;
        float placementAngle = RandomRange(random, 0f, Mathf.PI * 2f);
        Vector2 center = mapCenter + new Vector2(Mathf.Cos(placementAngle), Mathf.Sin(placementAngle)) * placementDistance;
        Vector2 distortionOffset = new Vector2(
            random.Next(-100000, 100000),
            random.Next(-100000, 100000));

        int positiveLobeCount = random.Next(
            plateauSettings.minimumPositiveLobes,
            plateauSettings.maximumPositiveLobes + 1);
        PlateauLobe[] positiveLobes = new PlateauLobe[positiveLobeCount];
        positiveLobes[0] = new PlateauLobe(center, radiusX, radiusZ, angle);

        for (int lobeIndex = 1; lobeIndex < positiveLobes.Length; lobeIndex++)
        {
            float lobeDirection = RandomRange(random, 0f, Mathf.PI * 2f);
            float lobeOffset = majorRadius
                * plateauSettings.secondaryLobeOffset
                * RandomRange(random, 0.55f, 1f);
            float lobeScale = RandomRange(
                random,
                plateauSettings.minimumSecondaryLobeScale,
                plateauSettings.maximumSecondaryLobeScale);
            float lobeAspect = RandomRange(
                random,
                plateauSettings.minimumAspectRatio,
                plateauSettings.maximumAspectRatio);
            float lobeMajorRadius = majorRadius * lobeScale;
            float lobeMinorRadius = lobeMajorRadius * lobeAspect;
            bool swapLobeAxes = random.Next(0, 2) == 0;
            Vector2 lobeCenter = center + new Vector2(
                Mathf.Cos(lobeDirection),
                Mathf.Sin(lobeDirection)) * lobeOffset;

            positiveLobes[lobeIndex] = new PlateauLobe(
                lobeCenter,
                swapLobeAxes ? lobeMajorRadius : lobeMinorRadius,
                swapLobeAxes ? lobeMinorRadius : lobeMajorRadius,
                angle + RandomRange(random, -1.1f, 1.1f));
        }

        int cutLobeCount = plateauSettings.maximumCutLobes > 0
            && random.NextDouble() < plateauSettings.cutLobeChance
                ? random.Next(1, plateauSettings.maximumCutLobes + 1)
                : 0;
        PlateauLobe[] cutLobes = new PlateauLobe[cutLobeCount];
        for (int cutIndex = 0; cutIndex < cutLobes.Length; cutIndex++)
        {
            float cutDirection = RandomRange(random, 0f, Mathf.PI * 2f);
            float cutScale = RandomRange(
                random,
                plateauSettings.minimumCutLobeScale,
                plateauSettings.maximumCutLobeScale);
            float cutRadius = majorRadius * cutScale;
            Vector2 cutCenter = center + new Vector2(
                Mathf.Cos(cutDirection),
                Mathf.Sin(cutDirection)) * majorRadius * RandomRange(random, 0.65f, 0.9f);
            cutLobes[cutIndex] = new PlateauLobe(
                cutCenter,
                cutRadius,
                cutRadius * RandomRange(random, 0.55f, 0.9f),
                cutDirection + RandomRange(random, -0.7f, 0.7f));
        }

        UnderwaterPlateauRegion candidate = new UnderwaterPlateauRegion(
            positiveLobes,
            cutLobes,
            settings.underwaterPlateauHeight,
            plateauSettings.transitionWidth,
            plateauSettings.edgeIrregularity,
            plateauSettings.edgeDistortionScale,
            distortionOffset,
            plateauSettings.cutStrength);

        // Cheap geometric filters first, then the expensive full-core substrate walk.
        if (!HasUsefulInterior(candidate, plateauSettings))
        {
            rejections[(int)PlateauRejection.InteriorTooNarrow]++;
            continue;
        }

        if (!IsSeparatedFromRegions(candidate, regions, plateauSettings.minimumRegionSeparation))
        {
            rejections[(int)PlateauRejection.TooCloseToExisting]++;
            continue;
        }

        if (!HasValidUnderwaterPlacement(candidate, plateauSettings))
        {
            rejections[(int)PlateauRejection.ShelfRelationship]++;
            continue;
        }

        if (!HasLegalPlateauSubstrate(candidate, plateauSettings, out int coreCellCount, out PlateauRejection reason))
        {
            rejections[(int)reason]++;
            continue;
        }

        acceptedCoreCells += coreCellCount;
        regions.Add(candidate);
    }

    if (plateauSettings.logPlateauGeneration)
    {
        Debug.Log(
            $"<color=cyan>Plateaus:</color> accepted {regions.Count}/{desiredCount} " +
            $"in {maximumAttempts} attempts, {acceptedCoreCells} core cells. Rejected: " +
            $"interior={rejections[(int)PlateauRejection.InteriorTooNarrow]} " +
            $"tooClose={rejections[(int)PlateauRejection.TooCloseToExisting]} " +
            $"shelf={rejections[(int)PlateauRejection.ShelfRelationship]} " +
            $"outOfBounds={rejections[(int)PlateauRejection.CoreOutOfBounds]} " +
            $"illegalSubstrate={rejections[(int)PlateauRejection.CoreOnIllegalSubstrate]} " +
            $"coreTooSmall={rejections[(int)PlateauRejection.CoreTooSmall]}");
    }

    return regions;
}


private static bool HasUsefulInterior(
    UnderwaterPlateauRegion candidate,
    UnderwaterPlateauGenerationSettings plateauSettings)
{
    float interiorRadius = Mathf.Min(candidate.RadiusX, candidate.RadiusZ) * (1f - candidate.TransitionWidth);
    return interiorRadius >= plateauSettings.minimumInteriorRadius;
}

private bool HasLegalPlateauSubstrate(
    UnderwaterPlateauRegion candidate,
    UnderwaterPlateauGenerationSettings plateauSettings,
    out int coreCellCount,
    out PlateauRejection rejection)
{
    coreCellCount = 0;
    rejection = PlateauRejection.None;

    int minX = Mathf.FloorToInt(candidate.Center.x - candidate.BoundingRadius) - 1;
    int maxX = Mathf.CeilToInt(candidate.Center.x + candidate.BoundingRadius) + 1;
    int minZ = Mathf.FloorToInt(candidate.Center.y - candidate.BoundingRadius) - 1;
    int maxZ = Mathf.CeilToInt(candidate.Center.y + candidate.BoundingRadius) + 1;

    for (int z = minZ; z <= maxZ; z++)
    {
        for (int x = minX; x <= maxX; x++)
        {
            // Cell [x,z] is sampled at its centre, matching GenerateGameplaySamples,
            // so the cells tested here are exactly the cells that will be visited.
            float sampleX = x + 0.5f;
            float sampleZ = z + 0.5f;

            if (candidate.CalculateInfluence(sampleX, sampleZ) < FullPlateauInfluence) continue;

            // A core cell outside the grid is never generated, so it is intended
            // but unpaintable - the same clipping failure, just at the border.
            if (x < 0 || x >= size || z < 0 || z >= size)
            {
                rejection = PlateauRejection.CoreOutOfBounds;
                return false;
            }

            if (!IsDeepOffshoreTerrain(SampleLegacyIsland(sampleX, sampleZ).TerrainType))
            {
                rejection = PlateauRejection.CoreOnIllegalSubstrate;
                return false;
            }

            coreCellCount++;
        }
    }

    if (coreCellCount < plateauSettings.minimumCoreCells)
    {
        rejection = PlateauRejection.CoreTooSmall;
        return false;
    }

    return true;
}


private bool HasValidUnderwaterPlacement(
    UnderwaterPlateauRegion candidate,
    UnderwaterPlateauGenerationSettings plateauSettings)
{
    int shelfSamples = 0;
    const int perimeterSampleCount = 16;
    for (int i = 0; i < perimeterSampleCount; i++)
    {
        float angle = i / (float)perimeterSampleCount * Mathf.PI * 2f;
        Vector2 point = candidate.NormalizedToWorld(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.05f);
        Cell.TerrainType type = SampleBaseIsland(point.x, point.y).TerrainType;
        if (type == Cell.TerrainType.Shallow
            || type == Cell.TerrainType.Water
            || type == Cell.TerrainType.Plateau)
        {
            shelfSamples++;
        }
    }

    switch (plateauSettings.shelfRelationship)
    {
        case PlateauShelfRelationship.ShelfAdjacent:
            return shelfSamples > 0;
        case PlateauShelfRelationship.DetachedFromShelf:
            return shelfSamples == 0;
        default:
            return true;
    }
}


private static bool IsSeparatedFromRegions(
    UnderwaterPlateauRegion candidate,
    List<UnderwaterPlateauRegion> regions,
    float minimumSeparation)
{
    for (int i = 0; i < regions.Count; i++)
    {
        UnderwaterPlateauRegion existing = regions[i];
        // BoundingRadius includes every secondary lobe and its warp allowance.
        // Using two full bounding circles here rejects elongated regions that are
        // visually separate, so use their primary footprint for placement spacing.
        float candidateSpacingRadius = Mathf.Sqrt(candidate.RadiusX * candidate.RadiusZ);
        float existingSpacingRadius = Mathf.Sqrt(existing.RadiusX * existing.RadiusZ);
        float requiredDistance = candidateSpacingRadius + existingSpacingRadius + minimumSeparation;
        if (Vector2.Distance(candidate.Center, existing.Center) < requiredDistance) return false;
    }

    return true;
}
}
