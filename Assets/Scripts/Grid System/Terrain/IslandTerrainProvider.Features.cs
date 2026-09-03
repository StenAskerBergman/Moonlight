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
    float baseField = CalculateLegacyIslandField(x, z);
    return baseField < settings.waterUpper;
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

        // Coarse march outward to find water crossing
        float rMin = 1f;
        float rMax = halfSize * 1.2f;
        float coarseStep = 3.0f;
        float rCurr = rMin;

        while (rCurr <= rMax)
        {
            Vector2 p = mapCenter + dir * rCurr;
            if (CalculateLegacyIslandField(p.x, p.y) < settings.waterUpper)
            {
                break;
            }
            rCurr += coarseStep;
        }

        // Binary refinement within the coarse bracket (3 iterations -> 0.375m accuracy)
        float low = Mathf.Max(rMin, rCurr - coarseStep);
        float high = Mathf.Min(rMax, rCurr);
        for (int step = 0; step < 3; step++)
        {
            float mid = (low + high) * 0.5f;
            Vector2 p = mapCenter + dir * mid;
            if (CalculateLegacyIslandField(p.x, p.y) < settings.waterUpper)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }
        foundRadius = (low + high) * 0.5f;

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

    // 2. Build PerimeterSectorMap around circumference
    PerimeterSectorMap sectorMap = new PerimeterSectorMap(mapCenter);
    // Base sector around entire perimeter is Beach
    sectorMap.AddSector(PerimeterSectorType.Beach, -Mathf.PI, Mathf.PI, 0.25f);

    // Rivers are routed after ridges exist, so their sources can be selected from
    // actual generated mountain mass rather than an arbitrary short coastal offset.
    RiverCorridorSettings riverSettings = settings.riverCorridors;

    // 3. Coastal Mountain Ridges Allocation
    CoastalMountainSettings mountainSettings = settings.coastalMountains;
    if (mountainSettings.enabled && mountainSettings.maxRidges > 0)
    {
        // Compute mean radius and landmass scale factor to naturally vary mountain density with island size
        float meanRadius = 0f;
        for (int i = 0; i < rayCount; i++) meanRadius += coastRadii[i];
        meanRadius /= Mathf.Max(1, rayCount);
        float sizeFactor = Mathf.Clamp01((meanRadius - halfSize * 0.25f) / (halfSize * 0.40f));

        int minimumCoastalRequired = Mathf.Min(mountainSettings.minRidges, mountainSettings.maxRidges);
        int maxCoastalAllowed = Mathf.Clamp(
            Mathf.RoundToInt(mountainSettings.maxRidges * sizeFactor),
            minimumCoastalRequired,
            mountainSettings.maxRidges);
        int ridgeCount = random.Next(minimumCoastalRequired, maxCoastalAllowed + 1);

        // Divide the circumference into seeded sectors. The previous candidate list
        // contained only sharp one-ray headlands, then removed a candidate even when
        // validation rejected it. Broad coastlines therefore got one mountain at one
        // end of a crescent. Sector-local retries produce several separated coastal
        // massifs while keeping every candidate close to its assigned sector.
        int attemptsPerSector = 6;
        int sectorPhase = random.Next(rayCount);
        for (int sector = 0; sector < ridgeCount; sector++)
        {
            int sectorCenter = (sectorPhase + Mathf.RoundToInt(sector * rayCount / (float)ridgeCount)) % rayCount;
            bool acceptedInSector = false;

            for (int sectorAttempt = 0; sectorAttempt < attemptsPerSector && !acceptedInSector; sectorAttempt++)
            {
                int searchOffset = AlternatingSearchOffset(sectorAttempt);
                int jitter = sectorAttempt == 0 ? 0 : random.Next(-1, 2);
                int rayIdx = (sectorCenter + searchOffset + jitter + rayCount) % rayCount;

                Vector2 coastPt = coastPoints[rayIdx];
                Vector2 normal = coastNormals[rayIdx];
                Vector2 tangent = new Vector2(-normal.y, normal.x);

                float maxAllowedLength = Mathf.Min(
                    mountainSettings.maxRidgeLength,
                    Mathf.Clamp(meanRadius * 0.85f, 7f, 16f));
                float minAllowedLength = Mathf.Min(
                    maxAllowedLength,
                    Mathf.Max(6f, mountainSettings.minRidgeLength));
                float maxAllowedWidth = Mathf.Min(
                    mountainSettings.maxRidgeWidth,
                    Mathf.Clamp(meanRadius * 0.32f, 3.5f, 7f));
                float minAllowedWidth = Mathf.Min(
                    maxAllowedWidth,
                    Mathf.Max(3.25f, mountainSettings.minRidgeWidth));

                float ridgeLength = RandomRange(random, minAllowedLength, maxAllowedLength);
                float ridgeWidth = RandomRange(random, minAllowedWidth, maxAllowedWidth);
                float peakHeight = CapPeakToWidth(mountainSettings.ridgePeakHeight * RandomRange(random, 0.95f, 1.25f), ridgeWidth);

                // Centre the ridge on its chosen coast sector. The old origin was the
                // beginning of the capsule, so the entire formation trailed off to one
                // side and frequently left the land it was meant to crown.
                float tanSign = random.Next(0, 2) == 0 ? 1f : -1f;
                Vector2 ridgeDir = (tangent * tanSign - normal * RandomRange(random, -0.05f, 0.18f)).normalized;
                Vector2 axisCenter = coastPt - normal * (ridgeWidth * 0.32f);
                Vector2 origin = axisCenter - ridgeDir * (ridgeLength * 0.5f);

                FeatureReservationMap.CoastalRidge ridge = new FeatureReservationMap.CoastalRidge(
                    origin, ridgeDir, ridgeLength, ridgeWidth, peakHeight, mountainSettings.cliffSharpness);

                bool passed = TryAddValidatedRidge(map, ridge, out string rejection, out RidgeValidationMetrics rMetrics);
                Debug.Log($"<color={(passed ? "lime" : "yellow")}>[Mountain Candidate - Coastal S{sector + 1}.{sectorAttempt + 1}]</color> {(passed ? "ACCEPTED" : "REJECTED")}: {rMetrics} | Rejection: {rejection ?? "None"}");

                if (passed)
                {
                    acceptedInSector = true;
                    float ridgeAngle = Mathf.Atan2(coastPt.y - mapCenter.y, coastPt.x - mapCenter.x);
                    sectorMap.AddSector(PerimeterSectorType.MountainCoast, ridgeAngle - 0.42f, ridgeAngle + 0.42f, 0.15f);
                }
            }
        }

        // Mountains belong to the perimeter. Interior ridges used the same scarce
        // buildable footprint as the city and could bisect an otherwise good plain.
        int interiorRidges = 0;
        int maxInteriorAttempts = interiorRidges * 6;
        int interiorAccepted = 0;

        for (int attempt = 0; attempt < maxInteriorAttempts && interiorAccepted < interiorRidges; attempt++)
        {
            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            float dist = RandomRange(random, 2f, halfSize * 0.38f * sizeFactor);
            Vector2 origin = mapCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

            // Candidate origin must be situated on dry interior mainland
            float baseField = CalculateLegacyIslandField(origin.x, origin.y);
            if (baseField < settings.beachUpper + 0.04f) continue;
            if (map.GetMountainAllowance(origin.x, origin.y) < 0.40f) continue;

            float maxAllowedLength = Mathf.Clamp(meanRadius * 0.85f, 8f, 20f);
            float maxAllowedWidth = Mathf.Clamp(meanRadius * 0.32f, 3.2f, 5.8f);

            float dirAngle = RandomRange(random, 0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(dirAngle), Mathf.Sin(dirAngle));
            float len = RandomRange(random, 8f, maxAllowedLength);
            float wid = RandomRange(random, 3.2f, maxAllowedWidth);
            float h = CapPeakToWidth(mountainSettings.ridgePeakHeight * RandomRange(random, 1.05f, 1.4f), wid);

            FeatureReservationMap.CoastalRidge ridge = new FeatureReservationMap.CoastalRidge(
                origin - dir * (len * 0.5f), dir, len, wid, h, mountainSettings.cliffSharpness);

            bool passed = TryAddValidatedRidge(map, ridge, out string rejection, out RidgeValidationMetrics rMetrics);
            Debug.Log($"<color={(passed ? "lime" : "yellow")}>[Mountain Candidate - Interior #{attempt + 1}]</color> {(passed ? "ACCEPTED" : "REJECTED")}: {rMetrics} | Rejection: {rejection ?? "None"}");

            if (passed)
            {
                interiorAccepted++;
            }
        }
    }

    if (mountainSettings.enabled && map.Ridges.Count == 0)
    {
        Debug.LogWarning($"[Terrain Provider] Bounded candidate retries exhausted for seed {chunkSeed}: 0 mountain ridges passed authoritative validation. Terrain will generate as lowland plain with 0 ridges.");
    }

    BuildMountainFedRivers(
        map, sectorMap, riverSettings, random, mapCenter,
        coastPoints, coastNormals, bayRays);

    map.Sectors = sectorMap;

    // 5. Derive Deterministic Mine Anchors from Mountain Flanks
    List<FeatureReservationMap.MineAnchor> candidateMines = new List<FeatureReservationMap.MineAnchor>();
    for (int ix = 6; ix < size - 6; ix += 2)
    {
        for (int iz = 6; iz < size - 6; iz += 2)
        {
            float baseField = CalculateLegacyIslandField(ix, iz);
            if (baseField < settings.waterUpper + 0.05f) continue; // Must be on dry island mainland

            float allowance = map.GetMountainAllowance(ix, iz);
            if (allowance < 0.35f) continue;

            float mHeight = map.GetSynthesizedMountainHeight(ix, iz);
            // Candidate must be situated on the mountain base flank
            if (mHeight >= 0.45f && mHeight <= 2.2f)
            {
                float hL = map.GetSynthesizedMountainHeight(ix - 1, iz);
                float hR = map.GetSynthesizedMountainHeight(ix + 1, iz);
                float hD = map.GetSynthesizedMountainHeight(ix, iz - 1);
                float hU = map.GetSynthesizedMountainHeight(ix, iz + 1);
                Vector2 grad = new Vector2(hR - hL, hU - hD);
                float slope = grad.magnitude * 0.5f;

                if (slope >= 0.20f && slope <= 0.85f)
                {
                    Vector2 normal = grad.sqrMagnitude > 0.001f ? grad.normalized : Vector2.up;
                    ResourceNodeType type = ResourceNodeType.Mine;

                    candidateMines.Add(new FeatureReservationMap.MineAnchor(new Vector2(ix, iz), normal, slope, type));
                }
            }
        }
    }

    for (int i = 0; i < candidateMines.Count && map.MineAnchors.Count < 6; i++)
    {
        var cand = candidateMines[i];
        bool tooClose = false;
        for (int j = 0; j < map.MineAnchors.Count; j++)
        {
            if (Vector2.Distance(cand.Position, map.MineAnchors[j].Position) < 7.5f)
            {
                tooClose = true;
                break;
            }
        }
        if (!tooClose)
        {
            map.AddMineAnchor(cand);
        }
    }

    return map;
}

private void BuildMountainFedRivers(
    FeatureReservationMap map,
    PerimeterSectorMap sectorMap,
    RiverCorridorSettings riverSettings,
    System.Random random,
    Vector2 mapCenter,
    Vector2[] coastPoints,
    Vector2[] coastNormals,
    List<int> bayRays)
{
    if (!riverSettings.enabled || riverSettings.maxRivers <= 0 || map.Ridges.Count == 0) return;

    int riverCount = random.Next(riverSettings.minRivers, riverSettings.maxRivers + 1);
    List<Vector2> usedSources = new List<Vector2>();
    List<int> usedMouthRays = new List<int>();

    for (int riverIndex = 0; riverIndex < riverCount; riverIndex++)
    {
        int exitRay = PickRiverMouthRay(random, coastPoints.Length, bayRays, usedMouthRays);
        usedMouthRays.Add(exitRay);
        Vector2 mouth = coastPoints[exitRay] + coastNormals[exitRay] * 2f;

        if (!TryPickMountainRiverSource(map, mouth, riverSettings.minimumRiverLength, random, usedSources,
                out Vector2 source, out float sourceTerrainHeight))
        {
            continue;
        }
        usedSources.Add(source);

        bool hasLake = random.NextDouble() < riverSettings.lakeSourceChance;
        FeatureReservationMap.RiverSourceKind sourceKind = hasLake
            ? FeatureReservationMap.RiverSourceKind.Lake
            : FeatureReservationMap.RiverSourceKind.Waterfall;
        float sourceSurfaceHeight = Mathf.Max(settings.surfaceFlatlandHeight + 0.1f, sourceTerrainHeight - 0.28f);

        if (hasLake)
        {
            float lakeRadius = RandomRange(random, riverSettings.minLakeRadius, riverSettings.maxLakeRadius);
            map.AddLake(new FeatureReservationMap.LakeBasin(
                source, lakeRadius, sourceSurfaceHeight, riverSettings.lakeDepth));
        }

        FeatureReservationMap.RiverCorridor corridor = new FeatureReservationMap.RiverCorridor(
            riverSettings.valleyDepth,
            riverSettings.channelRadius,
            riverSettings.clearanceRadius,
            sourceKind,
            sourceSurfaceHeight,
            settings.waterHeight + 0.03f);

        float totalDistance = Vector2.Distance(source, mouth);
        int waypointCount = Mathf.Clamp(Mathf.CeilToInt(totalDistance / 2f) + 1, 10, 48);
        Vector2 riverDirection = (mouth - source).normalized;
        Vector2 riverPerpendicular = new Vector2(-riverDirection.y, riverDirection.x);
        float phaseA = RandomRange(random, 0f, Mathf.PI * 2f);
        float phaseB = RandomRange(random, 0f, Mathf.PI * 2f);
        float meanderScale = Mathf.Clamp(totalDistance * 0.055f, 1.2f, 3.8f);

        for (int waypointIndex = 0; waypointIndex < waypointCount; waypointIndex++)
        {
            float t = waypointIndex / (float)(waypointCount - 1);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float meander = (Mathf.Sin(t * Mathf.PI * 2f + phaseA)
                + Mathf.Sin(t * Mathf.PI * 5f + phaseB) * 0.32f) * meanderScale * envelope;
            Vector2 position = Vector2.Lerp(source, mouth, t) + riverPerpendicular * meander;

            float widthT = Mathf.Pow(t, 1.2f);
            float channelRadius = Mathf.Lerp(riverSettings.channelRadius * 0.38f,
                riverSettings.channelRadius * 1.3f, widthT);
            float clearanceRadius = Mathf.Lerp(riverSettings.clearanceRadius * 0.55f,
                riverSettings.clearanceRadius * 1.15f, t);
            corridor.Waypoints.Add(new FeatureReservationMap.RiverWaypoint(
                position, channelRadius, clearanceRadius));
        }

        corridor.ComputeBounds();
        map.AddRiver(corridor);

        float mouthAngle = Mathf.Atan2(mouth.y - mapCenter.y, mouth.x - mapCenter.x);
        sectorMap.AddSector(PerimeterSectorType.RiverMouth,
            mouthAngle - 0.15f, mouthAngle + 0.15f, 0.08f);
    }
}

private int PickRiverMouthRay(
    System.Random random,
    int rayCount,
    List<int> bayRays,
    List<int> usedRays)
{
    List<int> pool = bayRays.Count > 0 ? bayRays : null;
    for (int attempt = 0; attempt < rayCount * 2; attempt++)
    {
        int candidate = pool != null && random.NextDouble() < 0.85
            ? pool[random.Next(pool.Count)]
            : random.Next(rayCount);
        bool separated = true;
        for (int i = 0; i < usedRays.Count; i++)
        {
            int delta = Mathf.Abs(candidate - usedRays[i]);
            delta = Mathf.Min(delta, rayCount - delta);
            if (delta < Mathf.Max(4, rayCount / 8)) { separated = false; break; }
        }
        if (separated) return candidate;
    }
    return random.Next(rayCount);
}

private bool TryPickMountainRiverSource(
    FeatureReservationMap map,
    Vector2 mouth,
    float minimumLength,
    System.Random random,
    List<Vector2> usedSources,
    out Vector2 source,
    out float sourceHeight)
{
    List<Vector3> candidates = new List<Vector3>();
    for (int x = 4; x < size - 4; x += 2)
    {
        for (int z = 4; z < size - 4; z += 2)
        {
            Vector2 position = new Vector2(x, z);
            if (Vector2.Distance(position, mouth) < minimumLength) continue;

            float ridgeHeight = map.GetSynthesizedMountainHeight(x, z);
            if (ridgeHeight < 0.75f) continue;
            float baseField = CalculateLegacyIslandField(x, z);
            if (baseField < settings.waterUpper + 0.05f) continue;

            bool tooClose = false;
            for (int i = 0; i < usedSources.Count; i++)
            {
                if (Vector2.Distance(position, usedSources[i]) < 10f) { tooClose = true; break; }
            }
            if (tooClose) continue;

            float terrainHeight = CalculateBaseContinuousHeight(baseField) + ridgeHeight;
            candidates.Add(new Vector3(x, terrainHeight, z));
        }
    }

    candidates.Sort((a, b) => b.y.CompareTo(a.y));
    if (candidates.Count == 0)
    {
        source = Vector2.zero;
        sourceHeight = 0f;
        return false;
    }

    int pick = random.Next(Mathf.Min(8, candidates.Count));
    Vector3 selected = candidates[pick];
    source = new Vector2(selected.x, selected.z);
    sourceHeight = selected.y;
    return true;
}

private static int AlternatingSearchOffset(int attempt)
{
    if (attempt <= 0) return 0;
    int magnitude = (attempt + 1) / 2;
    return (attempt & 1) == 1 ? magnitude : -magnitude;
}

// A ridge's realized gradient scales with peak/width, and ValidateMountainHeightfield bounds the
// heightfield's max slope by that same ratio. Requesting a tall peak on a narrow ridge therefore
// asks for a mountain the validator will reject - seeds were aborting by fractions of a percent
// (2.27 against an allowed 2.26). Capping the request at generation time keeps the guard at its
// original strictness instead of widening it: a ridge that wants to be taller has to be wider,
// which is also what stops narrow ridges turning into spikes.
private const float MaxPeakPerWidth = 0.82f;

private static float CapPeakToWidth(float requestedPeak, float width)
{
    return Mathf.Min(requestedPeak, Mathf.Max(2f, width) * MaxPeakPerWidth);
}

public struct RidgeValidationMetrics
{
    public float RequestedPeak;
    public float RealizedPeak;
    public float PeakRatio;
    public int ExpectedMass;
    public int SurvivingMass;
    public float SurvivingRatio;
    public float SurvivingWidth;
    public float RequiredWidth;
    public float ConnectedRatio;
    public float MaxObservedSlope;
    public float MaxAllowedSlope;

    public override string ToString()
    {
        return $"Peak: {RealizedPeak:F2}/{RequestedPeak:F2} (Ratio: {PeakRatio:P0}) | " +
               $"Mass: {SurvivingMass}/{ExpectedMass} ({SurvivingRatio:P0}) | " +
               $"Width: {SurvivingWidth:F2} (Req: {RequiredWidth:F2}) | " +
               $"Connected: {ConnectedRatio:P0} | " +
               $"Slope: {MaxObservedSlope:F2} (MaxAllowed: {MaxAllowedSlope:F2})";
    }
}

private bool TryAddValidatedRidge(
    FeatureReservationMap map,
    FeatureReservationMap.CoastalRidge ridge,
    out string rejection,
    out RidgeValidationMetrics metrics)
{
    metrics = default;
    CoastalMountainSettings validation = settings.coastalMountains;
    const float sampleStep = 0.5f;
    float alongStart = -ridge.Width * 0.5f;
    float alongEnd = ridge.Length + ridge.Width * 0.5f;
    float perpendicularExtent = ridge.Width * 1.8f;
    int alongCount = Mathf.Max(2, Mathf.CeilToInt((alongEnd - alongStart) / sampleStep) + 1);
    int acrossCount = Mathf.Max(2, Mathf.CeilToInt(perpendicularExtent * 2f / sampleStep) + 1);
    bool[,] surviving = new bool[alongCount, acrossCount];
    float[,] boosts = new float[alongCount, acrossCount];
    Vector2 normal = new Vector2(-ridge.Direction.y, ridge.Direction.x);
    int expectedMass = 0;
    int survivingMass = 0;
    float realizedPeak = 0f;
    float maximumSlope = 0f;
    float maximumSurvivingWidth = 0f;
    float supportThreshold = ridge.PeakHeight * 0.08f;

    for (int alongIndex = 0; alongIndex < alongCount; alongIndex++)
    {
        float along = Mathf.Min(alongEnd, alongStart + alongIndex * sampleStep);
        int survivingAcrossRun = 0;
        int longestAcrossRun = 0;

        for (int acrossIndex = 0; acrossIndex < acrossCount; acrossIndex++)
        {
            float across = Mathf.Min(perpendicularExtent, -perpendicularExtent + acrossIndex * sampleStep);
            Vector2 point = ridge.Origin + ridge.Direction * along + normal * across;
            float rawElevation = ridge.EvaluateRawElevation(point);
            if (rawElevation < supportThreshold)
            {
                survivingAcrossRun = 0;
                continue;
            }

            float baseField = CalculateLegacyIslandField(point.x, point.y);
            if (baseField <= settings.waterUpper)
            {
                // Submerged footprint is excluded from BOTH the numerator and the denominator.
                //
                // This check exists to reject ridges shredded by river-valley suppression or the
                // mountain-allowance mask - i.e. ridges that failed to occupy land available to
                // them. Open sea was never available, so counting it as "expected" penalised a
                // ridge purely for overhanging the water. That is exactly what a mountain
                // plunging into the sea does, so the more genuinely coastal a candidate was, the
                // more certainly it was rejected here, and the survivors were the ones sitting
                // inland. Measured downstream as ridge mass stopping ~1 world unit short of the
                // waterline with mountainBoost 0.000 at the shore, leaving a sand band between
                // the mountain and the sea.
                survivingAcrossRun = 0;
                continue;
            }

            expectedMass++;

            float u = Mathf.Clamp01((baseField - settings.abyssUpper) / Mathf.Max(0.01f, settings.waterUpper - settings.abyssUpper));
            float landMask = u * u * (3f - 2f * u);
            float boost = rawElevation * map.GetMountainAllowance(point.x, point.y) * landMask;
            if (!float.IsFinite(boost) || boost < supportThreshold)
            {
                survivingAcrossRun = 0;
                continue;
            }

            surviving[alongIndex, acrossIndex] = true;
            boosts[alongIndex, acrossIndex] = boost;
            survivingMass++;
            survivingAcrossRun++;
            longestAcrossRun = Mathf.Max(longestAcrossRun, survivingAcrossRun);
            realizedPeak = Mathf.Max(realizedPeak, boost);
        }

        maximumSurvivingWidth = Mathf.Max(maximumSurvivingWidth, longestAcrossRun * sampleStep);
    }

    // Populate metrics
    metrics.RequestedPeak = ridge.PeakHeight;
    metrics.RealizedPeak = realizedPeak;
    metrics.PeakRatio = ridge.PeakHeight > 0.001f ? (realizedPeak / ridge.PeakHeight) : 0f;
    metrics.ExpectedMass = expectedMass;
    metrics.SurvivingMass = survivingMass;
    metrics.SurvivingRatio = expectedMass > 0 ? (survivingMass / (float)expectedMass) : 0f;
    metrics.SurvivingWidth = maximumSurvivingWidth;
    metrics.RequiredWidth = ridge.Width * 0.90f;

    // Minimum structural mass
    int minimumSamples = Mathf.Max(12, Mathf.RoundToInt(ridge.Length * ridge.Width * 0.45f));
    if (expectedMass == 0 || survivingMass < minimumSamples)
    {
        rejection = $"surviving mass {survivingMass} samples is below minimum {minimumSamples} (expected: {expectedMass})";
        return false;
    }

    // Footprint surviving ratio (must be at least 40% on valid land)
    if (metrics.SurvivingRatio < 0.40f)
    {
        rejection = $"surviving mass ratio {metrics.SurvivingRatio:P0} is below required 40%";
        return false;
    }

    // Realized peak envelope
    if (realizedPeak < ridge.PeakHeight * 0.70f || realizedPeak > ridge.PeakHeight * 1.05f)
    {
        rejection = $"realized peak {realizedPeak:F2} is outside [0.70..1.05]*requested ({ridge.PeakHeight:F2})";
        return false;
    }

    // Minimum transverse width (rejects thin 1D sliver branches)
    if (maximumSurvivingWidth < metrics.RequiredWidth)
    {
        rejection = $"surviving width {maximumSurvivingWidth:F2} is below required width {metrics.RequiredWidth:F2}";
        return false;
    }

    // Dominant connected component
    int largestConnectedMass = FindLargestConnectedMountainMass(surviving);
    metrics.ConnectedRatio = survivingMass > 0 ? (largestConnectedMass / (float)survivingMass) : 0f;
    if (metrics.ConnectedRatio < 0.75f)
    {
        rejection = $"connected mass ratio {metrics.ConnectedRatio:P0} is below required 75%";
        return false;
    }

    // Maximum local slope sanity
    for (int alongIndex = 0; alongIndex < alongCount; alongIndex++)
    {
        for (int acrossIndex = 0; acrossIndex < acrossCount; acrossIndex++)
        {
            if (!surviving[alongIndex, acrossIndex]) continue;
            if (alongIndex + 1 < alongCount && surviving[alongIndex + 1, acrossIndex])
            {
                maximumSlope = Mathf.Max(maximumSlope,
                    Mathf.Abs(boosts[alongIndex + 1, acrossIndex] - boosts[alongIndex, acrossIndex]) / sampleStep);
            }
            if (acrossIndex + 1 < acrossCount && surviving[alongIndex, acrossIndex + 1])
            {
                maximumSlope = Mathf.Max(maximumSlope,
                    Mathf.Abs(boosts[alongIndex, acrossIndex + 1] - boosts[alongIndex, acrossIndex]) / sampleStep);
            }
        }
    }

    metrics.MaxObservedSlope = maximumSlope;
    metrics.MaxAllowedSlope = Mathf.Max(2.5f, (ridge.PeakHeight / Mathf.Max(1.5f, ridge.Width)) * 2.2f + 0.5f);

    if (maximumSlope < 0.15f || maximumSlope > metrics.MaxAllowedSlope)
    {
        rejection = $"maximum slope {maximumSlope:F2} is outside sanity range [0.15..{metrics.MaxAllowedSlope:F2}]";
        return false;
    }

    map.AddRidge(ridge);
    rejection = null;
    return true;
}

private static int FindLargestConnectedMountainMass(bool[,] samples)
{
    int width = samples.GetLength(0);
    int height = samples.GetLength(1);
    bool[,] visited = new bool[width, height];
    int largest = 0;
    Queue<Vector2Int> open = new Queue<Vector2Int>();
    Vector2Int[] directions =
    {
        Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up
    };

    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            if (!samples[x, y] || visited[x, y]) continue;

            int mass = 0;
            visited[x, y] = true;
            open.Enqueue(new Vector2Int(x, y));
            while (open.Count > 0)
            {
                Vector2Int current = open.Dequeue();
                mass++;
                for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
                {
                    Vector2Int next = current + directions[directionIndex];
                    if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height) continue;
                    if (!samples[next.x, next.y] || visited[next.x, next.y]) continue;
                    visited[next.x, next.y] = true;
                    open.Enqueue(next);
                }
            }

            largest = Mathf.Max(largest, mass);
        }
    }

    return largest;
}


}
