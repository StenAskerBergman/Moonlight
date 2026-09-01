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
        float geologyNoise = SampleComposedNoise(worldX + 47.9f, worldZ - 133.1f) * 2f - 1f;
        float widthVariation = geologyNoise * plateau.profileAsymmetry;
        float upperWidth = Mathf.Max(0.5f, plateau.upperEscarpmentWidth * (1f + widthVariation));
        float lowerWidth = Mathf.Max(1f, plateau.lowerApronWidth * (1f - widthVariation * 0.55f));
        float rockLength = upperWidth + lowerWidth;

        bool isTabletop = signedPerimeterDistance <= 0f;
        float interiorWeight = isTabletop
            ? SmootherStep01(Mathf.InverseLerp(0f, -plateau.rockyRimWidth, signedPerimeterDistance))
            : 0f;
        float tabletopNoise = SampleComposedNoise(worldX * 2.7f + 19.1f, worldZ * 2.7f - 73.4f) * 2f - 1f;
        float tabletopHeight = settings.underwaterPlateauHeight
            + tabletopNoise * plateau.tabletopRelief * interiorWeight;
        float formationWeight = 0f;
        float formationHeight = isTabletop
            ? EvaluatePerimeterFormationHeight(
                footprint.WorldAngle,
                signedPerimeterDistance,
                boundaryDistance,
                geologyNoise,
                plateau,
                out formationWeight)
            : 0f;
        float rockHeight = isTabletop
            ? tabletopHeight + (plateau.generateVolumetricRockGeometry ? 0f : formationHeight)
            : EvaluateRockProfile(
                signedPerimeterDistance,
                upperWidth,
                rockLength,
                abyssHeight,
                geologyNoise,
                plateau);

        float sandLength = rockLength * plateau.sandDescentLengthMultiplier;
        float influenceLength = plateau.sandOpeningCount > 0 ? sandLength : rockLength;
        float sandProgress = Mathf.Clamp01(signedPerimeterDistance / Mathf.Max(0.001f, sandLength));
        float sandMask = EvaluateSandOpeningMask(
            footprint.WorldAngle,
            footprint.RadialDistance,
            boundaryDistance,
            sandProgress,
            plateau);

        // Cubic ease-out is steepest at the tabletop break, softens through the
        // middle, and has zero gradient where it meets the broad lower apron.
        float sandDrop = 1f - Mathf.Pow(1f - sandProgress, 3f);
        float sandHeight = Mathf.Lerp(settings.underwaterPlateauHeight, abyssHeight, sandDrop);

        float height = Mathf.Lerp(rockHeight, sandHeight, sandMask);
        float influence = isTabletop
            ? 1f
            : 1f - SmootherStep01(signedPerimeterDistance / Mathf.Max(1f, influenceLength));

        float rimWeight = 1f - SmootherStep01(
            Mathf.Abs(signedPerimeterDistance) / Mathf.Max(0.5f, plateau.rockyRimWidth));
        float buildableWeight = interiorWeight
            * (1f - SmootherStep01(Mathf.Clamp01(formationWeight * 1.15f)));
        float abyssFade = SmootherStep01(Mathf.InverseLerp(
            upperWidth,
            Mathf.Max(upperWidth + 0.001f, rockLength),
            signedPerimeterDistance));
        float activeSandSlope = sandMask * (1f - abyssFade) * (isTabletop ? 1f : influence);
        float rockWeight = isTabletop
            ? Mathf.Max(rimWeight, formationWeight) * (1f - sandMask)
            : (1f - activeSandSlope) * (1f - abyssFade);
        float sandWeight = Mathf.Max(buildableWeight, activeSandSlope);
        float reefWeight = (1f - activeSandSlope)
            * Mathf.Max(rimWeight, isTabletop ? 0f : (1f - abyssFade) * 0.35f);
        // Secondary sediment weights reuse the geology fields already sampled for
        // shape and relief, so material variety remains deterministic without adding
        // more procedural-noise work to every visual sample.
        float materialMacro = geologyNoise * 0.5f + 0.5f;
        float materialDetail = tabletopNoise * 0.5f + 0.5f;
        float mixedSediment = Mathf.Clamp01(Mathf.Min(sandWeight, rockWeight) * 2.8f);
        float gravelWeight = (rimWeight * 0.38f + mixedSediment * 0.78f)
            * SmootherStep01(Mathf.InverseLerp(0.34f, 0.76f, materialDetail))
            * (1f - abyssFade);
        float mudWeight = buildableWeight
            * (1f - rimWeight)
            * (1f - Mathf.Clamp01(formationWeight))
            * SmootherStep01(Mathf.InverseLerp(0.38f, 0.73f, 1f - materialMacro))
            * 0.72f;
        float siltWeight = Mathf.Max(
            SmootherStep01(Mathf.InverseLerp(0.22f, 0.9f, abyssFade)),
            (1f - activeSandSlope) * (1f - rockWeight) * (isTabletop ? 0f : 0.32f));
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

        float maximumUpperWidth = plateau.upperEscarpmentWidth * (1f + plateau.profileAsymmetry);
        float maximumLowerWidth = plateau.lowerApronWidth * (1f + plateau.profileAsymmetry * 0.55f);
        float maximumProfileLength = maximumUpperWidth + maximumLowerWidth;
        if (plateau.sandOpeningCount > 0)
        {
            maximumProfileLength *= plateau.sandDescentLengthMultiplier;
        }

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

    private float EvaluatePerimeterFormationHeight(
        float angle,
        float signedDistance,
        float boundaryDistance,
        float geologyNoise,
        StandalonePlateauSettings plateau,
        out float formationWeight)
    {
        float rimWeight = 1f - SmootherStep01(
            Mathf.Abs(signedDistance) / Mathf.Max(0.5f, plateau.rockyRimWidth));
        float outcropNoise = Mathf.Pow(Mathf.Clamp01((geologyNoise + 0.10f) / 1.10f), 2.2f);
        formationWeight = rimWeight * outcropNoise * 0.72f;
        float height = (outcropNoise * plateau.rimOutcropHeight
            + geologyNoise * plateau.rimErosionHeight) * rimWeight;

        // Broad overlapping clusters provide the primary perimeter silhouette. They
        // read as shoulders, buttresses, and broken walls; the rarer spire pass below
        // only adds dominant landmarks on top of this massing.
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
                    * Mathf.Lerp(0.68f, 1.32f, SeedUnit(541 + index * 31));
                float lateralDistance = angleDelta * Mathf.Max(1f, boundaryDistance);
                float radialCenter = -plateau.rockyRimWidth
                    * Mathf.Lerp(0.28f, 0.78f, SeedUnit(557 + index * 31));
                float radialDistance = Mathf.Abs(signedDistance - radialCenter);
                float depth = plateau.perimeterClusterDepth
                    * Mathf.Lerp(0.72f, 1.30f, SeedUnit(563 + index * 31));
                float ellipse = Mathf.Sqrt(
                    lateralDistance * lateralDistance / Mathf.Max(0.001f, width * width)
                    + radialDistance * radialDistance / Mathf.Max(0.001f, depth * depth));
                float clusterMask = 1f - SmootherStep01(ellipse);
                if (clusterMask <= 0f) continue;

                float crag = Mathf.Lerp(0.68f, 1.16f, Mathf.Clamp01(geologyNoise * 0.5f + 0.5f));
                float clusterHeight = plateau.perimeterClusterHeight
                    * Mathf.Lerp(0.62f, 1.18f, SeedUnit(577 + index * 31));
                float shoulder = Mathf.Pow(clusterMask, 0.58f) * clusterHeight * crag;
                height = Mathf.Max(height, shoulder);
                formationWeight = Mathf.Max(formationWeight, clusterMask);
            }
        }

        if (plateau.occasionalSpireHeight <= 0f || plateau.perimeterSpireCount <= 0)
        {
            return height;
        }

        float fullCircle = Mathf.PI * 2f;
        for (int index = 0; index < plateau.perimeterSpireCount; index++)
        {
            float centerAngle = SeedUnit(701 + index * 43) * fullCircle;
            float angleDelta = Mathf.Abs(Mathf.DeltaAngle(
                angle * Mathf.Rad2Deg,
                centerAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float lateralDistance = angleDelta * Mathf.Max(1f, boundaryDistance);
            float width = plateau.spireBaseWidth * Mathf.Lerp(0.72f, 1.28f, SeedUnit(719 + index * 43));
            float angularMask = 1f - SmootherStep01(lateralDistance / Mathf.Max(0.001f, width));
            float radialCenter = -plateau.rockyRimWidth * Mathf.Lerp(0.18f, 0.55f, SeedUnit(733 + index * 43));
            float radialDistance = Mathf.Abs(signedDistance - radialCenter);
            float radialMask = 1f - SmootherStep01(
                radialDistance / Mathf.Max(0.001f, width * 0.72f));
            float spireMask = Mathf.Clamp01(angularMask * radialMask);
            float spireHeight = plateau.occasionalSpireHeight
                * Mathf.Lerp(0.58f, 1f, SeedUnit(751 + index * 43));
            float crag = Mathf.Lerp(0.76f, 1.12f, Mathf.Clamp01(geologyNoise * 0.5f + 0.5f));
            float broadShoulder = Mathf.Pow(spireMask, 0.48f) * spireHeight * 0.46f;
            float brokenPeak = Mathf.Pow(spireMask, 1.28f) * spireHeight * 0.68f * crag;
            height = Mathf.Max(height, broadShoulder + brokenPeak);
            formationWeight = Mathf.Max(formationWeight, spireMask);
        }

        return height;
    }

    private float EvaluateRockProfile(
        float signedDistance,
        float upperWidth,
        float totalLength,
        float abyssHeight,
        float geologyNoise,
        StandalonePlateauSettings plateau)
    {
        float authoredDrop = Mathf.Max(0.001f, plateau.cliffDropDepth + plateau.lowerApronDrop);
        float upperDropShare = plateau.cliffDropDepth / authoredDrop;
        float upperBottomHeight = Mathf.Lerp(
            settings.underwaterPlateauHeight,
            abyssHeight,
            upperDropShare);
        float profileHeight;
        if (signedDistance < upperWidth)
        {
            float t = Mathf.Clamp01(signedDistance / Mathf.Max(0.001f, upperWidth));
            float steepDrop = Mathf.Pow(t, 0.42f);
            profileHeight = Mathf.Lerp(settings.underwaterPlateauHeight, upperBottomHeight, steepDrop);
            float fractureEnvelope = Mathf.Sin(t * Mathf.PI);
            float fracture = geologyNoise * plateau.cliffFractureStrength * fractureEnvelope;
            profileHeight += fracture;
        }
        else
        {
            float lowerWidth = Mathf.Max(0.001f, totalLength - upperWidth);
            float t = Mathf.Clamp01((signedDistance - upperWidth) / lowerWidth);
            float softeningDrop = 1f - Mathf.Pow(1f - t, 2.6f);
            profileHeight = Mathf.Lerp(upperBottomHeight, abyssHeight, softeningDrop);
            float apronFracture = geologyNoise
                * plateau.cliffFractureStrength
                * 0.38f
                * Mathf.Sin(t * Mathf.PI);
            profileHeight += apronFracture;
        }

        return Mathf.Max(abyssHeight, profileHeight);
    }

    private float EvaluateSandOpeningMask(
        float angle,
        float radialDistance,
        float boundaryDistance,
        float descentProgress,
        StandalonePlateauSettings plateau)
    {
        int count = plateau.sandOpeningCount;
        if (count <= 0) return 0f;

        float fullCircle = Mathf.PI * 2f;
        float spacing = fullCircle / count;
        float globalRotation = SeedUnit(307) * fullCircle;
        float strongest = 0f;

        for (int opening = 0; opening < count; opening++)
        {
            float jitter = (SeedUnit(331 + opening * 17) - 0.5f) * spacing * 0.34f;
            float centerAngle = globalRotation + (opening + 0.5f) * spacing + jitter;
            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, centerAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float lateralDistance = deltaAngle * Mathf.Max(boundaryDistance, radialDistance);
            float width = plateau.sandOpeningTopWidth
                * Mathf.Lerp(1f, plateau.sandOpeningWidthMultiplier, descentProgress);
            float halfWidth = width * 0.5f;
            float openingMask = 1f - SmootherStep01(Mathf.InverseLerp(halfWidth * 0.72f, halfWidth, lateralDistance));
            strongest = Mathf.Max(strongest, openingMask);
        }

        return strongest;
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
