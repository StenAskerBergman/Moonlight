// tldr: The standalone underwater plateau chunk type: its tabletop footprint, escarpment profile and rim
// formations. A separate landform from islands, sharing only the world seabed datum.

using UnityEngine;

public sealed partial class IslandTerrainProvider
{
    private readonly struct PlateauFootprint
    {
        public PlateauFootprint(
            float signedDistance,
            float boundaryDistance,
            float radialDistance,
            float worldAngle)
        {
            SignedDistance = signedDistance;
            BoundaryDistance = boundaryDistance;
            RadialDistance = radialDistance;
            WorldAngle = worldAngle;
        }

        public float SignedDistance { get; }
        public float BoundaryDistance { get; }
        public float RadialDistance { get; }
        public float WorldAngle { get; }
    }

    private const float MinimumSculptedUpperEscarpmentWidth = 4.4f;
    private const float MinimumSculptedLowerApronWidth = 7.2f;

    // The standalone plateau module has one interface for every caller. It owns the
    // connected footprint, perimeter coordinate, local rock/sand profile, and final
    // classification so gameplay and dense visual sampling cannot drift apart.
    private TerrainSample EvaluateStandalonePlateau(
        float localX,
        float localZ,
        float worldX,
        float worldZ)
    {
        StandalonePlateauSettings plateau = settings.standalonePlateau;
        TerrainSample seabed = SampleSharedSeabed(worldX, worldZ);

        PlateauFootprint footprint = EvaluatePlateauFootprint(
            localX,
            localZ,
            worldX,
            worldZ,
            plateau);
        float abyssHeight = settings.abyssHeight;
        float signedPerimeterDistance = footprint.SignedDistance;
        float boundaryDistance = footprint.BoundaryDistance;
        float geologyNoise = SampleBoundedGeologyNoise(worldX, worldZ, 47.9f, -133.1f) * 2f - 1f;
        float ridgedGeology = SampleRidgedRockNoise(worldX, worldZ, 0.085f);
        float fineCragNoise = SampleRidgedRockNoise(worldX + 23.4f, worldZ - 41.7f, 0.22f);
        float widthVariation = geologyNoise * plateau.profileAsymmetry + (ridgedGeology - 0.5f) * 0.28f;
        float baseUpperWidth = Mathf.Max(plateau.upperEscarpmentWidth, MinimumSculptedUpperEscarpmentWidth);
        float baseLowerWidth = Mathf.Max(plateau.lowerApronWidth, MinimumSculptedLowerApronWidth);
        float upperWidth = Mathf.Max(1.5f, baseUpperWidth * (1f + widthVariation));
        float lowerWidth = Mathf.Max(2.5f, baseLowerWidth * (1f - widthVariation * 0.42f));
        float rockLength = upperWidth + lowerWidth;

        // Carve 1-2 natural canyon/saddle passes through the rim ridge so the plateau reads
        // like an open Anno 2070 sea-mount amphitheater rather than a uniform closed wall.
        float passMask = EvaluateRimPassMask(footprint.WorldAngle, plateau);
        bool isTabletop = signedPerimeterDistance <= 0f;
        float interiorWeight = isTabletop
            ? SmootherStep01(Mathf.InverseLerp(0f, -plateau.rockyRimWidth, signedPerimeterDistance))
            : 0f;
        float macroUndulation = SampleBoundedGeologyNoise(worldX * 0.55f, worldZ * 0.55f, -31.2f, 54.8f) * 2f - 1f;
        float currentWave = Mathf.Sin((worldX * 0.46f + worldZ * 0.29f) * 1.15f + geologyNoise * 1.8f);
        float tabletopNoise = SampleBoundedGeologyNoise(worldX * 2.1f, worldZ * 2.1f, 19.1f, -73.4f) * 2f - 1f;
        // Keep interior tabletop height modulation strictly within buildable slope tolerance
        // while breaking up flat monotony with gentle basin dunes and sediment swells.
        float safeRelief = Mathf.Min(plateau.tabletopRelief, 0.075f);
        float tabletopHeight = settings.underwaterPlateauHeight
            + (macroUndulation * 0.65f + tabletopNoise * 0.22f + currentWave * 0.13f) * safeRelief * interiorWeight;

        float formationWeight = 0f;
        float formationHeight = isTabletop
            ? EvaluatePerimeterFormationHeight(
                footprint.WorldAngle,
                signedPerimeterDistance,
                boundaryDistance,
                geologyNoise,
                ridgedGeology,
                fineCragNoise,
                passMask,
                plateau,
                out formationWeight)
            : 0f;

        // Always sculpt perimeter rock ridges & tiered rim shelves continuously into the heightfield
        // so TerrainMeshBuilder produces an organic amphitheater rather than a cut pancake.
        float sculptedRimShare = plateau.generateVolumetricRockGeometry ? 0.52f : 1f;
        float maxSafeFormationRise = Mathf.Max(0.5f, (settings.waterHeight - plateau.minimumFormationClearance) - tabletopHeight);
        float appliedFormationRise = Mathf.Min(formationHeight * sculptedRimShare, maxSafeFormationRise * 0.72f);

        float rockHeight = isTabletop
            ? tabletopHeight + appliedFormationRise
            : EvaluateRockProfile(
                signedPerimeterDistance,
                upperWidth,
                rockLength,
                abyssHeight,
                geologyNoise,
                ridgedGeology,
                fineCragNoise,
                footprint.WorldAngle,
                plateau);

        // Blend rim height smoothly from tabletop + formation across 0 so there is zero cliff-edge pinch.
        if (!isTabletop && appliedFormationRise > 0.001f)
        {
            float rimSpill = appliedFormationRise * (1f - SmootherStep01(signedPerimeterDistance / Mathf.Max(0.8f, upperWidth * 0.48f)));
            rockHeight += rimSpill;
        }

        float effectiveSandCount = plateau.sandOpeningCount > 0 ? plateau.sandOpeningCount : 1;
        float sandLength = rockLength * Mathf.Min(1.22f, plateau.sandDescentLengthMultiplier);
        float influenceLength = effectiveSandCount > 0 ? sandLength : rockLength;
        float sandProgress = Mathf.Clamp01(signedPerimeterDistance / Mathf.Max(0.001f, sandLength));
        float sandMask = EvaluateSandOpeningMask(
            footprint.WorldAngle,
            footprint.RadialDistance,
            boundaryDistance,
            sandProgress,
            passMask,
            plateau);

        float sandDrop = 1f - Mathf.Pow(1f - sandProgress, 2.35f);
        float sandHeight = Mathf.Lerp(tabletopHeight, abyssHeight, sandDrop);

        float height = Mathf.Lerp(rockHeight, sandHeight, sandMask * 0.78f);
        float influence = isTabletop
            ? 1f
            : 1f - SmootherStep01(signedPerimeterDistance / Mathf.Max(1f, influenceLength));

        float rimWeight = (1f - SmootherStep01(
            Mathf.Abs(signedPerimeterDistance) / Mathf.Max(0.6f, plateau.rockyRimWidth))) * (1f - passMask * 0.75f);
        float effectiveFormation = Mathf.Clamp01(formationWeight * (1f - passMask * 0.7f));
        float buildableWeight = interiorWeight
            * (1f - SmootherStep01(Mathf.Clamp01(effectiveFormation * 1.12f)));
        float abyssFade = SmootherStep01(Mathf.InverseLerp(
            upperWidth * 0.92f,
            Mathf.Max(upperWidth + 0.001f, rockLength),
            signedPerimeterDistance));
        float activeSandSlope = sandMask * (1f - abyssFade) * (isTabletop ? 1f : influence);
        float rockWeight = isTabletop
            ? Mathf.Max(rimWeight, effectiveFormation) * (1f - sandMask * 0.85f)
            : (1f - activeSandSlope * 0.8f) * (1f - abyssFade);
        float sandWeight = Mathf.Max(buildableWeight, activeSandSlope);
        float talusWeight = !isTabletop
            ? (1f - abyssFade) * SmootherStep01(Mathf.InverseLerp(upperWidth * 0.45f, upperWidth + lowerWidth * 0.48f, signedPerimeterDistance))
            : 0f;
        float reefWeight = (1f - activeSandSlope * 0.65f)
            * Mathf.Max(rimWeight * 0.92f, Mathf.Max(effectiveFormation * 0.85f, isTabletop ? 0f : (1f - abyssFade) * (0.32f + ridgedGeology * 0.25f)));

        float materialMacro = geologyNoise * 0.5f + 0.5f;
        float materialDetail = tabletopNoise * 0.5f + 0.5f;
        float mixedSediment = Mathf.Clamp01(Mathf.Min(sandWeight, rockWeight) * 2.8f);
        float gravelWeight = (rimWeight * 0.42f + mixedSediment * 0.82f + talusWeight * 0.85f)
            * SmootherStep01(Mathf.InverseLerp(0.28f, 0.74f, 0.55f * materialDetail + 0.45f * fineCragNoise))
            * (1f - abyssFade);
        float mudWeight = buildableWeight
            * (1f - rimWeight)
            * (1f - Mathf.Clamp01(effectiveFormation))
            * SmootherStep01(Mathf.InverseLerp(0.36f, 0.74f, 1f - materialMacro))
            * 0.68f;
        float siltWeight = Mathf.Max(
            SmootherStep01(Mathf.InverseLerp(0.20f, 0.88f, abyssFade)),
            (1f - activeSandSlope) * (1f - rockWeight) * (isTabletop ? 0f : 0.36f));
        PlateauZone zone = influence <= 0.001f
            ? PlateauZone.None
            : isTabletop
                ? (rimWeight > 0.2f || formationWeight > 0.18f
                    ? PlateauZone.RockyRim
                    : PlateauZone.Tabletop)
                : activeSandSlope > 0.5f
                    ? PlateauZone.SandSlope
                    : signedPerimeterDistance < upperWidth
                        ? PlateauZone.UpperEscarpment
                        : signedPerimeterDistance < rockLength
                            ? PlateauZone.LowerApron
                            : PlateauZone.AbyssFade;
        PlateauSampleData plateauData = new PlateauSampleData(
            zone,
            influence,
            buildableWeight,
            rockWeight,
            sandWeight,
            reefWeight,
            gravelWeight,
            mudWeight,
            siltWeight,
            abyssFade);

        return new TerrainSample(
            isTabletop ? Cell.TerrainType.Plateau : seabed.TerrainType,
            height,
            seabed.SourceValue,
            plateauData);
    }

    private PlateauFootprint EvaluatePlateauFootprint(
        float localX,
        float localZ,
        float worldX,
        float worldZ,
        StandalonePlateauSettings plateau)
    {
        // The generated mesh spans 0..size. Using (size - 1) here moved the landform
        // half a cell off centre and made opposing chunk margins disagree.
        float halfSize = Mathf.Max(1f, size * 0.5f);
        float rawX = localX - halfSize;
        float rawZ = localZ - halfSize;

        EvaluateDomainWarp(worldX, worldZ, worldSeed, out float warpX, out float warpZ);
        // Offset the mass from the exact chunk centre before applying bounded warp.
        // A perfectly centred radial field is what made the old result read as a
        // manufactured cake even when its edge carried high-frequency noise.
        float centerOffsetX = (SeedUnit(61) - 0.5f) * halfSize * 0.16f;
        float centerOffsetZ = (SeedUnit(67) - 0.5f) * halfSize * 0.16f;
        float deformedX = rawX - centerOffsetX + warpX * 0.38f;
        float deformedZ = rawZ - centerOffsetZ + warpZ * 0.38f;

        float rotation = SeedUnit(71) * Mathf.PI * 2f;
        float cosRotation = Mathf.Cos(rotation);
        float sinRotation = Mathf.Sin(rotation);
        float shapeX = deformedX * cosRotation + deformedZ * sinRotation;
        float shapeZ = -deformedX * sinRotation + deformedZ * cosRotation;

        PlateauShapeMode shape = ResolvePlateauShape(plateau.shapeMode);
        float majorScale = 1f;
        float minorScale = 1f;
        switch (shape)
        {
            case PlateauShapeMode.Elongated:
                majorScale = Mathf.Sqrt(plateau.elongation);
                minorScale = 1f / Mathf.Sqrt(plateau.elongation);
                break;
            case PlateauShapeMode.Crescent:
                majorScale = Mathf.Lerp(1.12f, plateau.elongation, 0.72f);
                minorScale = 0.84f;
                break;
            case PlateauShapeMode.TwinLobed:
                majorScale = Mathf.Lerp(1.08f, plateau.elongation, 0.58f);
                minorScale = 0.90f;
                break;
            case PlateauShapeMode.Rounded:
                majorScale = Mathf.Lerp(0.96f, 1.08f, SeedUnit(89));
                minorScale = 1f / majorScale;
                break;
        }

        // The plateau owns a bounded radial generation domain. Uniform containment
        // keeps its complete profile inside the chunk while preserving an organic
        // silhouette; no side or corner of the square chunk participates in shaping.
        float footprintRadius = halfSize * 0.55f;
        float normalizedX = shapeX / (footprintRadius * majorScale);
        float normalizedZ = shapeZ / (footprintRadius * minorScale);
        if (shape == PlateauShapeMode.Crescent)
        {
            float direction = SeedUnit(97) < 0.5f ? -1f : 1f;
            normalizedZ += direction * plateau.curvature * (normalizedX * normalizedX - 0.12f);
        }

        // A superellipse creates broad shoulders and flatter reaches between corners.
        // Low-order lobes and notches then break that macro form. Fine noise remains
        // subordinate, so it cannot turn the buildable clearing into disconnected
        // speckles.
        float edgePower = plateau.edgeSquareness;
        float normalizedRadius = Mathf.Pow(
            Mathf.Pow(Mathf.Abs(normalizedX), edgePower)
            + Mathf.Pow(Mathf.Abs(normalizedZ), edgePower),
            1f / edgePower);
        float shapeAngle = Mathf.Atan2(normalizedZ, normalizedX);
        float centerWorldX = worldX - rawX;
        float centerWorldZ = worldZ - rawZ;
        float broadRing = halfSize * 0.72f;
        float detailRing = halfSize * 1.63f;
        float broadShape = SampleComposedNoise(
            centerWorldX + Mathf.Cos(shapeAngle) * broadRing,
            centerWorldZ + Mathf.Sin(shapeAngle) * broadRing) * 2f - 1f;
        float detailShape = SampleComposedNoise(
            centerWorldX + Mathf.Cos(shapeAngle) * detailRing + 173.7f,
            centerWorldZ + Mathf.Sin(shapeAngle) * detailRing - 91.3f) * 2f - 1f;

        float catalogueLobing = 0f;
        if (shape == PlateauShapeMode.TwinLobed)
        {
            catalogueLobing = Mathf.Cos(shapeAngle * 2f) * 0.105f;
        }
        else if (shape == PlateauShapeMode.Crescent)
        {
            catalogueLobing = Mathf.Sin(shapeAngle) * 0.035f;
        }

        float macroAsymmetry =
            Mathf.Sin(shapeAngle + SeedUnit(101) * Mathf.PI * 2f) * plateau.silhouetteLobing * 0.48f
            + Mathf.Cos(shapeAngle * 2f + SeedUnit(107) * Mathf.PI * 2f) * plateau.silhouetteLobing * 0.34f
            + Mathf.Sin(shapeAngle * 3f + SeedUnit(109) * Mathf.PI * 2f) * plateau.silhouetteLobing * 0.22f;

        float localizedLobes = 0f;
        const int lobeCount = 3;
        float lobeSpacing = Mathf.PI * 2f / lobeCount;
        float lobeRotation = SeedUnit(127) * Mathf.PI * 2f;
        for (int lobe = 0; lobe < lobeCount; lobe++)
        {
            float centerAngle = lobeRotation
                + lobe * lobeSpacing
                + (SeedUnit(131 + lobe * 17) - 0.5f) * lobeSpacing * 0.42f;
            float width = Mathf.Lerp(0.38f, 0.72f, SeedUnit(137 + lobe * 17));
            float mask = AngularMask(shapeAngle, centerAngle, width);
            localizedLobes += mask * Mathf.Lerp(0.035f, 0.085f, SeedUnit(149 + lobe * 17));
        }

        float localizedNotches = 0f;
        int notchCount = plateau.boundaryNotchCount;
        if (notchCount > 0)
        {
            float notchSpacing = Mathf.PI * 2f / notchCount;
            float notchRotation = lobeRotation + notchSpacing * 0.43f;
            for (int notch = 0; notch < notchCount; notch++)
            {
                float centerAngle = notchRotation
                    + notch * notchSpacing
                    + (SeedUnit(211 + notch * 19) - 0.5f) * notchSpacing * 0.34f;
                float width = Mathf.Lerp(0.24f, 0.52f, SeedUnit(223 + notch * 19));
                localizedNotches += AngularMask(shapeAngle, centerAngle, width)
                    * plateau.boundaryNotchDepth
                    * Mathf.Lerp(0.72f, 1.18f, SeedUnit(229 + notch * 19));
            }
        }

        float boundaryRadius01 = Mathf.Clamp(
            plateau.tabletopRadius
            + broadShape * plateau.silhouetteLobing
            + detailShape * plateau.silhouetteNoise
            + catalogueLobing,
            0.27f,
            0.70f);

        float baseUpperWidth = Mathf.Max(plateau.upperEscarpmentWidth, MinimumSculptedUpperEscarpmentWidth);
        float baseLowerWidth = Mathf.Max(plateau.lowerApronWidth, MinimumSculptedLowerApronWidth);
        float maximumUpperWidth = baseUpperWidth * (1f + plateau.profileAsymmetry + 0.14f);
        float maximumLowerWidth = baseLowerWidth * (1f + plateau.profileAsymmetry * 0.42f + 0.14f);
        float maximumProfileLength = (maximumUpperWidth + maximumLowerWidth)
            * Mathf.Max(1f, Mathf.Min(1.22f, plateau.sandDescentLengthMultiplier));

        float maximumAxisScale = Mathf.Max(majorScale, minorScale);
        float maximumDomainWarp = Mathf.Min(
            settings.domainWarp.amplitude,
            Mathf.Min(7.5f, size * 0.20f)) * 0.38f;
        float maximumCentreDisplacement = Mathf.Sqrt(2f)
            * (halfSize * 0.08f + maximumDomainWarp);
        float maximumBoundaryRadius = (halfSize - maximumCentreDisplacement - maximumProfileLength - 1f)
            / Mathf.Max(0.001f, footprintRadius * maximumAxisScale);
        float containedBoundaryRadius = Mathf.Clamp(maximumBoundaryRadius, 0.05f, 0.72f);
        boundaryRadius01 = Mathf.Min(boundaryRadius01, containedBoundaryRadius);
        boundaryRadius01 = Mathf.Clamp(
            boundaryRadius01 + macroAsymmetry + localizedLobes - localizedNotches,
            Mathf.Min(0.18f, containedBoundaryRadius),
            containedBoundaryRadius);
        float distanceScale = footprintRadius * maximumAxisScale;
        float signedDistance = (normalizedRadius - boundaryRadius01) * distanceScale;
        float radialDistance = Mathf.Sqrt(deformedX * deformedX + deformedZ * deformedZ);
        float boundaryDistance = Mathf.Max(1f, radialDistance - signedDistance);
        float worldAngle = Mathf.Atan2(deformedZ, deformedX);
        return new PlateauFootprint(
            signedDistance,
            boundaryDistance,
            radialDistance,
            worldAngle);
    }

    private PlateauShapeMode ResolvePlateauShape(PlateauShapeMode configured)
    {
        if (configured != PlateauShapeMode.Auto) return configured;

        // Auto favours silhouettes with an obvious major gesture. Rounded remains an
        // explicit catalogue option, but it is a poor default for the target's broken,
        // enclosed clearing.
        const int catalogueCount = 3;
        int catalogueIndex = Mathf.Min(catalogueCount - 1, Mathf.FloorToInt(SeedUnit(53) * catalogueCount));
        return (PlateauShapeMode)(catalogueIndex + 2);
    }

    private float EvaluateRimPassMask(float worldAngle, StandalonePlateauSettings plateau)
    {
        int passCount = plateau.sandOpeningCount > 0
            ? Mathf.Clamp(plateau.sandOpeningCount, 1, 3)
            : 2;
        float fullCircle = Mathf.PI * 2f;
        float spacing = fullCircle / passCount;
        float globalRotation = SeedUnit(307) * fullCircle;
        float strongest = 0f;

        for (int index = 0; index < passCount; index++)
        {
            float jitter = (SeedUnit(331 + index * 17) - 0.5f) * spacing * 0.34f;
            float centerAngle = globalRotation + (index + 0.5f) * spacing + jitter;
            float halfAngle = Mathf.Lerp(0.22f, 0.36f, SeedUnit(353 + index * 23));
            strongest = Mathf.Max(strongest, AngularMask(worldAngle, centerAngle, halfAngle));
        }

        return strongest;
    }

    private float SampleBoundedGeologyNoise(float worldX, float worldZ, float offsetX, float offsetZ)
    {
        float phaseX = Mathf.Repeat(SeedUnit(401) * NoiseOffsetWrap + offsetX, NoiseOffsetWrap);
        float phaseZ = Mathf.Repeat(SeedUnit(409) * NoiseOffsetWrap + offsetZ, NoiseOffsetWrap);

        float x0 = Mathf.Repeat(worldX * 0.055f + phaseX, 256f);
        float z0 = Mathf.Repeat(worldZ * 0.055f + phaseZ, 256f);
        float x1 = Mathf.Repeat(worldX * 0.125f + phaseX * 1.7f + 37.3f, 256f);
        float z1 = Mathf.Repeat(worldZ * 0.125f + phaseZ * 1.3f - 19.1f, 256f);
        float x2 = Mathf.Repeat(worldX * 0.27f - phaseX * 0.8f + 91.7f, 256f);
        float z2 = Mathf.Repeat(worldZ * 0.27f + phaseZ * 1.9f + 63.4f, 256f);

        float n0 = Mathf.PerlinNoise(x0, z0);
        float n1 = Mathf.PerlinNoise(x1, z1);
        float n2 = Mathf.PerlinNoise(x2, z2);
        return Mathf.Clamp01(n0 * 0.58f + n1 * 0.28f + n2 * 0.14f);
    }

    private float SampleRidgedRockNoise(float worldX, float worldZ, float frequency)
    {
        float phaseX = Mathf.Repeat(SeedUnit(419) * NoiseOffsetWrap, NoiseOffsetWrap);
        float phaseZ = Mathf.Repeat(SeedUnit(431) * NoiseOffsetWrap, NoiseOffsetWrap);

        float weight = 0.5f;
        float amplitude = 0.62f;
        float total = 0f;
        float norm = 0f;
        float freq = Mathf.Max(0.01f, frequency);

        for (int octave = 0; octave < 3; octave++)
        {
            float sx = Mathf.Repeat(worldX * freq + phaseX + octave * 41.7f, 256f);
            float sz = Mathf.Repeat(worldZ * freq + phaseZ + octave * 73.1f, 256f);
            float signal = 1f - Mathf.Abs(Mathf.PerlinNoise(sx, sz) * 2f - 1f);
            signal *= signal;
            signal *= Mathf.Clamp01(weight);
            weight = signal * 1.65f;

            total += signal * amplitude;
            norm += amplitude;
            freq *= 2.08f;
            amplitude *= 0.48f;
        }

        return norm > 0.0001f ? Mathf.Clamp01(total / norm) : 0.5f;
    }

    private float EvaluatePerimeterFormationHeight(
        float angle,
        float signedDistance,
        float boundaryDistance,
        float geologyNoise,
        float ridgedGeology,
        float fineCragNoise,
        float passMask,
        StandalonePlateauSettings plateau,
        out float formationWeight)
    {
        float passSuppression = 1f - passMask * 0.88f;
        float rimWeight = 1f - SmootherStep01(
            Mathf.Abs(signedDistance) / Mathf.Max(0.5f, plateau.rockyRimWidth));
        float ridgedFactor = Mathf.Clamp01(ridgedGeology * 0.72f + fineCragNoise * 0.28f);
        float outcropNoise = Mathf.Pow(Mathf.Clamp01((geologyNoise * 0.48f + ridgedFactor * 0.72f - 0.12f)), 1.65f);
        formationWeight = rimWeight * outcropNoise * 0.78f * passSuppression;
        float height = (outcropNoise * plateau.rimOutcropHeight
            + (geologyNoise * 0.55f + (fineCragNoise - 0.5f) * 0.45f) * plateau.rimErosionHeight) * rimWeight;

        // Broad overlapping rock headlands provide the primary rim silhouette while
        // stepped shelving gives the ridge natural sedimentary strata rather than sand mounds.
        int clusterCount = plateau.perimeterClusterCount;
        if (clusterCount > 0 && plateau.perimeterClusterHeight > 0f)
        {
            float clusterFullCircle = Mathf.PI * 2f;
            float spacing = clusterFullCircle / clusterCount;
            float rotation = SeedUnit(503) * clusterFullCircle;
            for (int index = 0; index < clusterCount; index++)
            {
                float centerAngle = rotation
                    + (index + 0.5f) * spacing
                    + (SeedUnit(521 + index * 31) - 0.5f) * spacing * 0.58f;
                float angleDelta = AngularDistance(angle, centerAngle);
                float width = plateau.perimeterClusterWidth
                    * Mathf.Lerp(0.72f, 1.35f, SeedUnit(541 + index * 31));
                float lateralDistance = angleDelta * Mathf.Max(1f, boundaryDistance);
                float radialCenter = -plateau.rockyRimWidth
                    * Mathf.Lerp(0.24f, 0.74f, SeedUnit(557 + index * 31));
                float radialDistance = Mathf.Abs(signedDistance - radialCenter);
                float depth = plateau.perimeterClusterDepth
                    * Mathf.Lerp(0.75f, 1.32f, SeedUnit(563 + index * 31));
                float ellipse = Mathf.Sqrt(
                    lateralDistance * lateralDistance / Mathf.Max(0.001f, width * width)
                    + radialDistance * radialDistance / Mathf.Max(0.001f, depth * depth));
                float clusterMask = 1f - SmootherStep01(ellipse);
                if (clusterMask <= 0f) continue;

                float crag = Mathf.Lerp(0.72f, 1.24f, ridgedFactor);
                float steppedProfile = Mathf.Pow(clusterMask, 0.58f);
                // Soft terrace benching along cluster slopes creates layered rim steps.
                float terrace = Mathf.Sin(steppedProfile * Mathf.PI * 3f) * 0.06f * steppedProfile;
                float clusterHeight = plateau.perimeterClusterHeight
                    * Mathf.Lerp(0.65f, 1.18f, SeedUnit(577 + index * 31));
                float shoulder = Mathf.Max(0f, (steppedProfile + terrace) * clusterHeight * crag);
                height = Mathf.Max(height, shoulder);
                formationWeight = Mathf.Max(formationWeight, clusterMask * passSuppression);
            }
        }

        if (plateau.occasionalSpireHeight > 0f && plateau.perimeterSpireCount > 0)
        {
            float fullCircle = Mathf.PI * 2f;
            for (int index = 0; index < plateau.perimeterSpireCount; index++)
            {
                float centerAngle = SeedUnit(701 + index * 43) * fullCircle;
                float angleDelta = AngularDistance(angle, centerAngle);
                float lateralDistance = angleDelta * Mathf.Max(1f, boundaryDistance);
                float width = plateau.spireBaseWidth * Mathf.Lerp(0.76f, 1.32f, SeedUnit(719 + index * 43));
                float angularMask = 1f - SmootherStep01(lateralDistance / Mathf.Max(0.001f, width));
                float radialCenter = -plateau.rockyRimWidth * Mathf.Lerp(0.18f, 0.54f, SeedUnit(733 + index * 43));
                float radialDistance = Mathf.Abs(signedDistance - radialCenter);
                float radialMask = 1f - SmootherStep01(
                    radialDistance / Mathf.Max(0.001f, width * 0.74f));
                float spireMask = Mathf.Clamp01(angularMask * radialMask);
                if (spireMask <= 0f) continue;

                float spireHeight = plateau.occasionalSpireHeight
                    * Mathf.Lerp(0.58f, 1f, SeedUnit(751 + index * 43));
                float crag = Mathf.Lerp(0.78f, 1.18f, ridgedFactor);
                float broadShoulder = Mathf.Pow(spireMask, 0.48f) * spireHeight * 0.48f;
                float brokenPeak = Mathf.Pow(spireMask, 1.22f) * spireHeight * 0.62f * crag;
                height = Mathf.Max(height, broadShoulder + brokenPeak);
                formationWeight = Mathf.Max(formationWeight, spireMask * passSuppression);
            }
        }

        return height * passSuppression;
    }

    private float EvaluateRockProfile(
        float signedDistance,
        float upperWidth,
        float totalLength,
        float abyssHeight,
        float geologyNoise,
        float ridgedGeology,
        float fineCragNoise,
        float worldAngle,
        StandalonePlateauSettings plateau)
    {
        float authoredDrop = Mathf.Max(0.001f, plateau.cliffDropDepth + plateau.lowerApronDrop);
        float upperDropShare = plateau.cliffDropDepth / authoredDrop;
        float upperBottomHeight = Mathf.Lerp(
            settings.underwaterPlateauHeight,
            abyssHeight,
            upperDropShare);
        // Subtle geological bedding dip tilted gently across the seamount.
        float beddingDip = (Mathf.Sin(worldAngle * 2f + SeedUnit(613) * 6.283f) * 0.10f)
            + (geologyNoise * 0.08f);
        float profileHeight;

        if (signedDistance < upperWidth)
        {
            float t = Mathf.Clamp01(signedDistance / Mathf.Max(0.001f, upperWidth));
            // Stepped sedimentary strata ledges break up sheer wall monotony into Anno-like shelves.
            float strataCount = 4.2f;
            float shiftedT = Mathf.Clamp01(t + beddingDip * Mathf.Sin(t * Mathf.PI));
            float strataPhase = shiftedT * strataCount;
            float strataIndex = Mathf.Floor(strataPhase);
            float strataFrac = strataPhase - strataIndex;
            float stepBias = Mathf.Lerp(0.32f, 0.72f, ridgedGeology);
            float steppedT = (strataIndex + Mathf.Pow(strataFrac, stepBias)) / strataCount;
            float steepDrop = Mathf.Lerp(Mathf.Pow(t, 0.46f), Mathf.Pow(steppedT, 0.56f), 0.55f);

            profileHeight = Mathf.Lerp(settings.underwaterPlateauHeight, upperBottomHeight, steepDrop);
            float fractureEnvelope = Mathf.Sin(t * Mathf.PI);
            float fractureSignal = (ridgedGeology - 0.46f) * 1.35f + geologyNoise * 0.45f + (fineCragNoise - 0.5f) * 0.5f;
            profileHeight += fractureSignal * plateau.cliffFractureStrength * fractureEnvelope;
        }
        else
        {
            float lowerWidth = Mathf.Max(0.001f, totalLength - upperWidth);
            float t = Mathf.Clamp01((signedDistance - upperWidth) / lowerWidth);
            float softeningDrop = 1f - Mathf.Pow(1f - t, 2.55f);
            profileHeight = Mathf.Lerp(upperBottomHeight, abyssHeight, softeningDrop);
            // Concave talus accumulation mounds near the base of the upper escarpment.
            float talusEnvelope = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.35f);
            float talusSignal = (ridgedGeology - 0.38f) * 0.9f + geologyNoise * 0.4f;
            profileHeight += talusSignal * plateau.cliffFractureStrength * 0.48f * talusEnvelope;
        }

        return Mathf.Max(abyssHeight, profileHeight);
    }

    private float EvaluateSandOpeningMask(
        float angle,
        float radialDistance,
        float boundaryDistance,
        float descentProgress,
        float passMask,
        StandalonePlateauSettings plateau)
    {
        int count = plateau.sandOpeningCount > 0 ? plateau.sandOpeningCount : 1;
        float fullCircle = Mathf.PI * 2f;
        float spacing = fullCircle / count;
        float globalRotation = SeedUnit(307) * fullCircle;
        float strongest = passMask * Mathf.Lerp(0.45f, 0.85f, descentProgress);

        for (int opening = 0; opening < count; opening++)
        {
            float jitter = (SeedUnit(331 + opening * 17) - 0.5f) * spacing * 0.34f;
            float centerAngle = globalRotation + (opening + 0.5f) * spacing + jitter;
            float deltaAngle = AngularDistance(angle, centerAngle);
            float lateralDistance = deltaAngle * Mathf.Max(boundaryDistance, radialDistance);
            float width = plateau.sandOpeningTopWidth
                * Mathf.Lerp(1f, plateau.sandOpeningWidthMultiplier, descentProgress);
            float halfWidth = width * 0.5f;
            float openingMask = 1f - SmootherStep01(Mathf.InverseLerp(halfWidth * 0.72f, halfWidth, lateralDistance));
            strongest = Mathf.Max(strongest, openingMask);
        }

        return Mathf.Clamp01(strongest);
    }

    private float SeedUnit(int salt)
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

    private static float SmootherStep01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private static float AngularDistance(float angle, float center)
    {
        return Mathf.Abs(Mathf.Atan2(
            Mathf.Sin(angle - center),
            Mathf.Cos(angle - center)));
    }

    private static float AngularMask(float angle, float center, float halfWidth)
    {
        return 1f - SmootherStep01(
            AngularDistance(angle, center) / Mathf.Max(0.001f, halfWidth));
    }

    private static void PopulatePlateauSlopes(TerrainSampleCache cache)
    {
        int resolution = cache.Resolution;
        float step = cache.Step;

        System.Threading.Tasks.Parallel.For(0, resolution, z =>
        {
            for (int x = 0; x < resolution; x++)
            {
                float hLD = cache.GetHeight(x - 1, z - 1);
                float hL0 = cache.GetHeight(x - 1, z);
                float hLU = cache.GetHeight(x - 1, z + 1);
                float hRD = cache.GetHeight(x + 1, z - 1);
                float hR0 = cache.GetHeight(x + 1, z);
                float hRU = cache.GetHeight(x + 1, z + 1);
                float hCD = cache.GetHeight(x, z - 1);
                float hCU = cache.GetHeight(x, z + 1);

                float gradX = ((hRD + 2f * hR0 + hRU) - (hLD + 2f * hL0 + hLU)) / (8f * step);
                float gradZ = ((hLU + 2f * hCU + hRU) - (hLD + 2f * hCD + hRD)) / (8f * step);
                cache.Slopes[cache.GetIndex(x, z)] = Mathf.Sqrt(gradX * gradX + gradZ * gradZ);
            }
        });
    }
}
