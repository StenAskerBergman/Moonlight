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
    return IsUnderwaterTerrain(SampleLegacyIsland(x, z).TerrainType);
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
