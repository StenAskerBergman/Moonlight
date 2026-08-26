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

    // 3. River Corridors Reservation
    RiverCorridorSettings riverSettings = settings.riverCorridors;
    if (riverSettings.enabled && riverSettings.maxRivers > 0)
    {
        int riverCount = random.Next(riverSettings.minRivers, riverSettings.maxRivers + 1);
        for (int r = 0; r < riverCount; r++)
        {
            // Pick exit point along coast (prefer sheltered bays)
            int exitRay = random.Next(rayCount);
            if (bayRays.Count > 0 && random.NextDouble() < 0.85)
            {
                exitRay = bayRays[random.Next(bayRays.Count)];
            }
            Vector2 mouth = coastPoints[exitRay] + coastNormals[exitRay] * 2.0f;

            // River source starts in the interior and flows outward towards the mouth on the same side
            Vector2 toCenter = (mapCenter - mouth).normalized;
            float distToCenter = Vector2.Distance(mapCenter, mouth);
            float riverLength = RandomRange(random, 6f, Mathf.Min(12f, distToCenter * 0.55f));
            Vector2 source = mouth + toCenter * riverLength;

            FeatureReservationMap.RiverCorridor corridor = new FeatureReservationMap.RiverCorridor(
                riverSettings.valleyDepth, riverSettings.channelRadius, riverSettings.clearanceRadius);

            float totalDist = Vector2.Distance(source, mouth);
            int waypointCount = Mathf.Clamp(Mathf.RoundToInt(totalDist / 2.5f), 8, 24);
            Vector2 riverDir = (mouth - source).normalized;
            Vector2 riverPerp = new Vector2(-riverDir.y, riverDir.x);

            float phi1 = (float)random.NextDouble() * Mathf.PI * 2f;
            float phi2 = (float)random.NextDouble() * Mathf.PI * 2f;
            float phi3 = (float)random.NextDouble() * Mathf.PI * 2f;
            float meanderAmp1 = RandomRange(random, 1.2f, 2.4f);
            float meanderAmp2 = RandomRange(random, 0.4f, 1.0f);
            float meanderAmp3 = RandomRange(random, 0.15f, 0.4f);

            float baseChannel = riverSettings.channelRadius;
            float baseClearance = riverSettings.clearanceRadius;

            for (int w = 0; w < waypointCount; w++)
            {
                float t = w / (float)(waypointCount - 1);

                // Multi-harmonic compound meandering with envelope anchoring at source and mouth
                float envelope = Mathf.Sin(t * Mathf.PI);
                float meander = (Mathf.Sin(t * Mathf.PI * 2f + phi1) * meanderAmp1 +
                                 Mathf.Sin(t * Mathf.PI * 4f + phi2) * meanderAmp2 +
                                 Mathf.Sin(t * Mathf.PI * 8f + phi3) * meanderAmp3) * envelope;

                Vector2 wpPos = Vector2.Lerp(source, mouth, t) + riverPerp * meander;

                // Dynamic width: narrow upstream (0.4m), broadening to delta mouth (1.4m)
                float tChannel = Mathf.Pow(t, 1.35f);
                float channelRad = Mathf.Lerp(baseChannel * 0.35f, baseChannel * 1.15f, tChannel);
                float clearanceRad = Mathf.Lerp(baseClearance * 0.55f, baseClearance * 1.15f, t);

                // Subtle organic width wobble along the stream
                float wobble = 1f + 0.05f * Mathf.Sin(t * Mathf.PI * 10f + phi2);
                channelRad *= wobble;

                corridor.Waypoints.Add(new FeatureReservationMap.RiverWaypoint(wpPos, channelRad, clearanceRad));
            }

            corridor.ComputeBounds();
            map.AddRiver(corridor);

            float mouthAngle = Mathf.Atan2(mouth.y - mapCenter.y, mouth.x - mapCenter.x);
            sectorMap.AddSector(PerimeterSectorType.RiverMouth, mouthAngle - 0.15f, mouthAngle + 0.15f, 0.08f);
        }
    }

    // 4. Coastal Mountain Ridges Allocation
    CoastalMountainSettings mountainSettings = settings.coastalMountains;
    if (mountainSettings.enabled && mountainSettings.maxRidges > 0)
    {
        // Compute mean radius and landmass scale factor to naturally vary mountain density with island size
        float meanRadius = 0f;
        for (int i = 0; i < rayCount; i++) meanRadius += coastRadii[i];
        meanRadius /= Mathf.Max(1, rayCount);
        float sizeFactor = Mathf.Clamp01((meanRadius - halfSize * 0.25f) / (halfSize * 0.40f));

        int maxCoastalAllowed = Mathf.Max(1, Mathf.RoundToInt(mountainSettings.maxRidges * sizeFactor));
        int ridgeCount = random.Next(Mathf.Min(mountainSettings.minRidges, maxCoastalAllowed), maxCoastalAllowed + 1);
        List<int> candidateHeadlands = new List<int>(headlandRays.Count > 0 ? headlandRays : new int[] { rayCount / 4, rayCount * 3 / 4 });

        // Coastal Ridges bounded generation & validation
        int maxCoastalAttempts = ridgeCount * 5;
        int coastalAccepted = 0;
        for (int attempt = 0; attempt < maxCoastalAttempts && coastalAccepted < ridgeCount && candidateHeadlands.Count > 0; attempt++)
        {
            int pickIdx = random.Next(candidateHeadlands.Count);
            int rayIdx = candidateHeadlands[pickIdx];
            candidateHeadlands.RemoveAt(pickIdx);

            Vector2 coastPt = coastPoints[rayIdx];
            Vector2 normal = coastNormals[rayIdx];
            Vector2 tangent = new Vector2(-normal.y, normal.x);

            float maxAllowedLength = Mathf.Clamp(meanRadius * 0.90f, 8f, 22f);
            float maxAllowedWidth = Mathf.Clamp(meanRadius * 0.35f, 3.5f, 6.5f);

            float ridgeLength = RandomRange(random, 8f, maxAllowedLength);
            float ridgeWidth = RandomRange(random, 3.2f, maxAllowedWidth);
            float peakHeight = mountainSettings.ridgePeakHeight * RandomRange(random, 1.0f, 1.35f);

            // Orient along the coastal contour and across the headland
            float tanSign = random.Next(0, 2) == 0 ? 1f : -1f;
            Vector2 ridgeDir = (tangent * tanSign * RandomRange(random, 0.85f, 1f) - normal * RandomRange(random, -0.05f, 0.20f)).normalized;
            Vector2 origin = coastPt - normal * (ridgeWidth * 0.15f);

            FeatureReservationMap.CoastalRidge ridge = new FeatureReservationMap.CoastalRidge(
                origin, ridgeDir, ridgeLength, ridgeWidth, peakHeight, mountainSettings.cliffSharpness);

            bool passed = TryAddValidatedRidge(map, ridge, out string rejection, out RidgeValidationMetrics rMetrics);
            Debug.Log($"<color={(passed ? "lime" : "yellow")}>[Mountain Candidate - Coastal #{attempt + 1}]</color> {(passed ? "ACCEPTED" : "REJECTED")}: {rMetrics} | Rejection: {rejection ?? "None"}");

            if (passed)
            {
                coastalAccepted++;
                float ridgeAngle = Mathf.Atan2(coastPt.y - mapCenter.y, coastPt.x - mapCenter.x);
                sectorMap.AddSector(PerimeterSectorType.MountainCoast, ridgeAngle - 0.35f, ridgeAngle + 0.35f, 0.15f);
            }
        }

        // Interior Ridges bounded generation & validation
        int maxInteriorAllowed = Mathf.Max(1, Mathf.RoundToInt(3 * sizeFactor));
        int interiorRidges = random.Next(1, maxInteriorAllowed + 1);
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
            float h = mountainSettings.ridgePeakHeight * RandomRange(random, 1.05f, 1.4f);

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

            expectedMass++;
            float baseField = CalculateLegacyIslandField(point.x, point.y);
            if (baseField <= settings.abyssUpper)
            {
                survivingAcrossRun = 0;
                continue;
            }

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
