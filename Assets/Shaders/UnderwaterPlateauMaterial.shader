Shader "Custom/UnderwaterPlateauMaterial"
{
    Properties
    {
        [MainTexture] _BaseMap ("Generated Surface Colour", 2D) = "white" {}
        _Control ("Material Weights (R grass, G sand, B rock, A seabed)", 2D) = "black" {}
        _SurfaceTint ("Climate Surface Colour", 2D) = "white" {}
        _RegionMask ("Region Mask (R)", 2D) = "black" {}
        _UseSurfaceTint ("Use Climate Surface Colour", Float) = 0
        _DebugView ("Display Diagnostic Colour", Float) = 0
        [MainColor] _BaseColor ("Base Tint", Color) = (1, 1, 1, 1)
        _GeneratedColorBlend ("Generated Colour Blend", Range(0, 1)) = 0

        [Header(Tiled Material Textures)]
        _RockAlbedo ("Rock Albedo", 2D) = "white" {}
        _RockNormal ("Rock Normal", 2D) = "bump" {}
        _RockTiling ("Rock Tiling", Float) = 0.16
        _RockNormalScale ("Rock Normal Scale", Range(0, 3)) = 1.15
        _SandAlbedo ("Sand Albedo", 2D) = "white" {}
        _SandNormal ("Sand Normal", 2D) = "bump" {}
        _SandTiling ("Sand Tiling", Float) = 0.28
        _SandNormalScale ("Sand Normal Scale", Range(0, 3)) = 0.85
        _SeabedAlbedo ("Seabed Albedo", 2D) = "white" {}
        _SeabedNormal ("Seabed Normal", 2D) = "bump" {}
        _SeabedTiling ("Seabed Tiling", Float) = 0.30
        _SeabedNormalScale ("Seabed Normal Scale", Range(0, 3)) = 1.0
        _GrassAlbedo ("Grass Albedo", 2D) = "white" {}
        _GrassNormal ("Grass Normal", 2D) = "bump" {}
        _GrassTiling ("Grass Tiling", Float) = 0.25
        _TextureDetailStrength ("Texture Detail Blend", Range(0, 1)) = 0.72
        _WaterLevel ("Water Level", Float) = -1.0

        [Header(Rock Palette)]
        _RockDarkColor ("Crevice Colour", Color) = (0.06, 0.11, 0.15, 1)
        _RockMidColor ("Stone Colour", Color) = (0.19, 0.29, 0.34, 1)
        _RockLightColor ("Worn Face Colour", Color) = (0.42, 0.54, 0.58, 1)
        _SedimentColor ("Ledge Sediment", Color) = (0.38, 0.48, 0.51, 1)
        _AlgaeColor ("Algae Colour", Color) = (0.12, 0.31, 0.27, 1)

        [Header(Procedural Detail)]
        _RockScale ("Rock Detail Scale", Range(0.05, 4)) = 0.72
        _StrataScale ("Strata Scale", Range(0.05, 3)) = 0.46
        _StrataStrength ("Strata Strength", Range(0, 1)) = 0.76
        _CrackStrength ("Crevice Strength", Range(0, 1)) = 0.60
        _SedimentStrength ("Top Sediment", Range(0, 1)) = 0.44
        _AlgaeStrength ("Algae Strength", Range(0, 1)) = 0.36
        _RippleScale ("Sand Ripple Scale", Range(0.2, 12)) = 3.2
        _RippleStrength ("Sand Ripple Strength", Range(0, 1)) = 0.24
        _NormalStrength ("Procedural Normal Strength", Range(0, 1)) = 0.28
        _Smoothness ("Wetness", Range(0, 1)) = 0.44
        _WorldSeed ("World Seed", Float) = 0

        [Header(Submerged Caustics)]
        _CausticStrength ("Caustic Strength", Range(0, 1.5)) = 0.42
        _CausticScale ("Caustic Scale", Float) = 0.17
        _CausticSpeed ("Caustic Speed", Float) = 0.55

        [Header(Placement and Influence Overlay)]
        [HideInInspector] _PlacementMask ("Placement Mask", 2D) = "black" {}
        [HideInInspector] _PlacementOverlayState ("Placement Overlay State", Vector) = (0, 0, 0, 1)
        [HideInInspector] _PlacementGridParams ("Placement Grid Params", Vector) = (100, 1, 0, 0)

        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Placement/PlacementOverlayCommon.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 lightmapUV : TEXCOORD1;
            float2 dynamicLightmapUV : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float4 shadowCoord : TEXCOORD3;
            half fogFactor : TEXCOORD4;
            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5);
            half3 vertexLighting : TEXCOORD6;
            float3 placementData : TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TEXTURE2D(_Control);         SAMPLER(sampler_Control);
        TEXTURE2D(_BaseMap);         SAMPLER(sampler_BaseMap);
        TEXTURE2D(_SurfaceTint);
        TEXTURE2D(_RegionMask);
        TEXTURE2D(_PlacementMask);   SAMPLER(sampler_PlacementMask);

        TEXTURE2D(_RockAlbedo);      SAMPLER(sampler_RockAlbedo);
        TEXTURE2D(_RockNormal);      SAMPLER(sampler_RockNormal);
        TEXTURE2D(_SandAlbedo);      SAMPLER(sampler_SandAlbedo);
        TEXTURE2D(_SandNormal);      SAMPLER(sampler_SandNormal);
        TEXTURE2D(_SeabedAlbedo);    SAMPLER(sampler_SeabedAlbedo);
        TEXTURE2D(_SeabedNormal);    SAMPLER(sampler_SeabedNormal);
        TEXTURE2D(_GrassAlbedo);     SAMPLER(sampler_GrassAlbedo);
        TEXTURE2D(_GrassNormal);     SAMPLER(sampler_GrassNormal);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _Control_ST, _SurfaceTint_ST, _RegionMask_ST;
            float4 _PlacementMask_ST, _PlacementOverlayState, _PlacementGridParams;
            half _DebugView, _UseSurfaceTint;
            half4 _BaseColor;
            half4 _RockDarkColor;
            half4 _RockMidColor;
            half4 _RockLightColor;
            half4 _SedimentColor;
            half4 _AlgaeColor;
            half _GeneratedColorBlend;
            half _TextureDetailStrength;
            float _RockTiling, _SandTiling, _SeabedTiling, _GrassTiling;
            half _RockNormalScale, _SandNormalScale, _SeabedNormalScale;
            float _WaterLevel;
            half _RockScale;
            half _StrataScale;
            half _StrataStrength;
            half _CrackStrength;
            half _SedimentStrength;
            half _AlgaeStrength;
            half _RippleScale;
            half _RippleStrength;
            half _NormalStrength;
            half _Smoothness;
            float _WorldSeed;
            half _CausticStrength;
            float _CausticScale;
            float _CausticSpeed;
        CBUFFER_END

        float Hash21(float2 samplePosition)
        {
            samplePosition = frac(samplePosition * float2(123.34, 456.21));
            samplePosition += dot(samplePosition, samplePosition + 45.32);
            return frac(samplePosition.x * samplePosition.y);
        }

        float ValueNoise(float2 samplePosition)
        {
            float2 cell = floor(samplePosition);
            float2 local = frac(samplePosition);
            local = local * local * (3.0 - 2.0 * local);

            float a = Hash21(cell);
            float b = Hash21(cell + float2(1.0, 0.0));
            float c = Hash21(cell + float2(0.0, 1.0));
            float d = Hash21(cell + float2(1.0, 1.0));
            return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
        }

        float FractalNoise(float2 samplePosition)
        {
            float value = ValueNoise(samplePosition) * 0.58;
            value += ValueNoise(samplePosition * 2.03 + 17.7) * 0.29;
            value += ValueNoise(samplePosition * 4.11 - 9.2) * 0.13;
            return value;
        }

        // Dual-layer animated cellular caustic shimmer in world coordinates.
        float SampleSurfaceCaustics(float2 xz, float time)
        {
            float2 uvA = xz * _CausticScale + float2(time * _CausticSpeed * 0.12, -time * _CausticSpeed * 0.08);
            float2 uvB = xz * (_CausticScale * 1.43) + float2(-time * _CausticSpeed * 0.09, time * _CausticSpeed * 0.11);
            float nA = abs(ValueNoise(uvA) * 2.0 - 1.0);
            float nB = abs(ValueNoise(uvB + 11.3) * 2.0 - 1.0);
            float web = saturate(1.0 - min(nA, nB) * 3.4);
            return web * web * 0.65 + (1.0 - max(nA, nB)) * 0.35;
        }

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv;
            output.placementData = float3(
                input.positionOS.xz / max(_PlacementGridParams.y, 1e-4),
                input.positionOS.y);
            OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
            OUTPUT_SH(output.normalWS, output.vertexSH);
            output.vertexLighting = VertexLighting(positionInputs.positionWS, output.normalWS);
            output.shadowCoord = GetShadowCoord(positionInputs);
            output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
            return output;
        }

        half3 EvaluateProceduralNormal(half3 normalWS, float3 positionWS, float detailHeight)
        {
            float3 positionDx = ddx(positionWS);
            float3 positionDy = ddy(positionWS);
            float heightDx = ddx(detailHeight);
            float heightDy = ddy(detailHeight);
            float3 gradientX = cross(positionDy, normalWS);
            float3 gradientY = cross(normalWS, positionDx);
            float determinant = dot(positionDx, gradientX);
            float3 surfaceGradient = (gradientX * heightDx + gradientY * heightDy)
                / (abs(determinant) > 0.0001 ? determinant : (determinant < 0 ? -0.0001 : 0.0001));
            return normalize(normalWS - surfaceGradient * _NormalStrength);
        }

        float3 BlendTriplanarWorldNormal(
            float3 geomNormalWS,
            float3 nX, float3 nY, float3 nZ,
            float3 blend)
        {
            float3 gradient = float3(0, nX.y, nX.x) / max(nX.z, 0.12) * blend.x
                            + float3(nY.x, 0, nY.y) / max(nY.z, 0.12) * blend.y
                            + float3(nZ.x, nZ.y, 0) / max(nZ.z, 0.12) * blend.z;
            return normalize(geomNormalWS + gradient - geomNormalWS * dot(gradient, geomNormalWS));
        }

        float3 BlendPlanarWorldNormal(float3 geomNormalWS, float3 detailTS)
        {
            float3 gradient = float3(detailTS.x, 0, detailTS.y) / max(detailTS.z, 0.12);
            return normalize(geomNormalWS + gradient - geomNormalWS * dot(gradient, geomNormalWS));
        }

        void EvaluatePlateauSurface(Varyings input, out SurfaceData surface, out half3 normalWS)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half3 generatedSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
            float4 rawControl = SAMPLE_TEXTURE2D(_Control, sampler_Control,
                input.uv * _Control_ST.xy + _Control_ST.zw);

            half3 geometricNormal = normalize(input.normalWS);
            float3 positionDx = ddx(input.positionWS);
            float3 positionDy = ddy(input.positionWS);
            float upward = saturate((geometricNormal.y - 0.12) / 0.78);
            float vertical = 1.0 - smoothstep(0.18, 0.88, upward);

            // Symmetric 3D triplanar projection weights prevent any vertical cliff stretching.
            float3 triBlend = pow(abs(geometricNormal), 4.0);
            triBlend /= max(triBlend.x + triBlend.y + triBlend.z, 1e-4);

            float3 world = input.positionWS + float3(_WorldSeed * 1.73, 0.0, _WorldSeed * -2.11);

            // Isotropic 3D procedural grain across all 3 triplanar planes.
            float grainX = FractalNoise(world.zy * _RockScale * 0.48);
            float grainY = FractalNoise(world.xz * _RockScale * 0.48);
            float grainZ = FractalNoise(world.xy * _RockScale * 0.48);
            float macroRock = grainX * triBlend.x + grainY * triBlend.y + grainZ * triBlend.z;

            // Tiled photographic rock detail sampled triplanarly for real mineral grain.
            float rockTile = max(0.02, _RockTiling);
            float3 rockTexX = SAMPLE_TEXTURE2D_GRAD(_RockAlbedo, sampler_RockAlbedo, world.zy * rockTile, positionDx.zy * rockTile, positionDy.zy * rockTile).rgb;
            float3 rockTexY = SAMPLE_TEXTURE2D_GRAD(_RockAlbedo, sampler_RockAlbedo, world.xz * rockTile, positionDx.xz * rockTile, positionDy.xz * rockTile).rgb;
            float3 rockTexZ = SAMPLE_TEXTURE2D_GRAD(_RockAlbedo, sampler_RockAlbedo, world.xy * rockTile, positionDx.xy * rockTile, positionDy.xy * rockTile).rgb;
            float3 tiledRockAlbedo = rockTexX * triBlend.x + rockTexY * triBlend.y + rockTexZ * triBlend.z;
            float rockTexLuma = dot(tiledRockAlbedo, float3(0.299, 0.587, 0.114));

            float3 rockNX = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_RockNormal, sampler_RockNormal, world.zy * rockTile, positionDx.zy * rockTile, positionDy.zy * rockTile), _RockNormalScale);
            float3 rockNY = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_RockNormal, sampler_RockNormal, world.xz * rockTile, positionDx.xz * rockTile, positionDy.xz * rockTile), _RockNormalScale);
            float3 rockNZ = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_RockNormal, sampler_RockNormal, world.xy * rockTile, positionDx.xy * rockTile, positionDy.xy * rockTile), _RockNormalScale);
            float3 rockTiledNormalWS = BlendTriplanarWorldNormal(geometricNormal, rockNX, rockNY, rockNZ, triBlend);

            // Warped horizontal & slightly dipping sedimentary strata layers.
            float strataWarp = FractalNoise(world.xz * 0.11 + 5.4) * 1.45 + world.x * 0.035;
            float warpedHeight = world.y * _StrataScale + strataWarp;
            float strataWave = sin(warpedHeight * 6.2831853);
            float strata = strataWave * 0.5 + 0.5;
            float ledge = smoothstep(0.55, 0.92, strata)
                * smoothstep(0.18, 0.68, FractalNoise(world.xz * 0.25 - 3.1));

            // 3D triplanar rock fractures (eliminates vertical cliff stripes!).
            float crackFreq = _RockScale * 1.45;
            float crackX = FractalNoise(world.zy * crackFreq + 19.3);
            float crackY = FractalNoise(world.xz * crackFreq + 31.7);
            float crackZ = FractalNoise(world.xy * crackFreq - 14.2);
            float crackSample = crackX * triBlend.x + crackY * triBlend.y + crackZ * triBlend.z;
            float crack = 1.0 - smoothstep(0.035, 0.135, abs(crackSample - 0.50));
            crack *= lerp(0.50, 1.0, vertical);

            half3 rock = lerp(_RockDarkColor.rgb, _RockMidColor.rgb, smoothstep(0.18, 0.78, macroRock));
            rock = lerp(rock, _RockLightColor.rgb,
                saturate(ledge * _StrataStrength * 0.72 + smoothstep(0.68, 0.92, macroRock) * 0.36));
            // Modulate procedural stone palette with tiled cliff stone micro-texture contrast.
            half3 texturedStone = rock * lerp(1.0, (half)(0.62 + rockTexLuma * 0.86), _TextureDetailStrength * 0.85h);
            rock = lerp(texturedStone, _RockDarkColor.rgb * 0.42h, (half)(crack * _CrackStrength));

            float sedimentNoise = FractalNoise(world.xz * 0.22 + 71.0);
            float sedimentMask = pow(upward, 2.8) * _SedimentStrength
                * smoothstep(0.24, 0.72, sedimentNoise + ledge * 0.40);
            rock = lerp(rock, _SedimentColor.rgb, (half)saturate(sedimentMask));

            float algaeNoise = FractalNoise(world.xz * 0.31 - 41.0);
            float algaeMask = _AlgaeStrength
                * smoothstep(0.46, 0.82, algaeNoise + crack * 0.26)
                * lerp(0.95, 0.35, upward);
            rock = lerp(rock, _AlgaeColor.rgb, (half)saturate(algaeMask));

            // Marine tabletop sand: combine TextureBuilder semantic tint with cool mineral sand texture & ripples.
            float2 wpos = input.positionWS.xz;
            float sandTile = max(0.02, _SandTiling);
            float3 sandTex = SAMPLE_TEXTURE2D_GRAD(_SandAlbedo, sampler_SandAlbedo, wpos * sandTile, positionDx.xz * sandTile, positionDy.xz * sandTile).rgb;
            float3 sandTS = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_SandNormal, sampler_SandNormal, wpos * sandTile, positionDx.xz * sandTile, positionDy.xz * sandTile), _SandNormalScale);
            float3 sandTiledNormalWS = BlendPlanarWorldNormal(geometricNormal, sandTS);

            float rippleWarp = FractalNoise(world.xz * 0.14 + 93.0) * 2.4;
            float rippleA = sin((world.x * 0.86 + world.z * 0.31) * _RippleScale + rippleWarp);
            float rippleB = sin((world.x * -0.24 + world.z * 0.91) * (_RippleScale * 0.57) - rippleWarp);
            float ripple = rippleA * 0.68 + rippleB * 0.32;
            float rippleMask = pow(upward, 3.2) * _RippleStrength;

            float sandLuma = dot(sandTex, float3(0.299, 0.587, 0.114));
            half3 sandBaseTint = generatedSample * _BaseColor.rgb;
            half3 marineSand = sandBaseTint * lerp(1.0, (half)(0.70 + sandLuma * 0.62), _TextureDetailStrength);
            marineSand *= 1.0 + (half)(ripple * rippleMask * 0.16);

            // Seabed border floor (matches TerrainSplatNormal's _SeabedAlbedo / _SeabedTiling outside plateau footprint).
            float seabedTile = max(0.02, _SeabedTiling);
            float3 seabedAlbedo = SAMPLE_TEXTURE2D_GRAD(_SeabedAlbedo, sampler_SeabedAlbedo, wpos * seabedTile, positionDx.xz * seabedTile, positionDy.xz * seabedTile).rgb;
            float3 seabedTS = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_SeabedNormal, sampler_SeabedNormal, wpos * seabedTile, positionDx.xz * seabedTile, positionDy.xz * seabedTile), _SeabedNormalScale);
            float3 seabedNormalWS = BlendPlanarWorldNormal(geometricNormal, seabedTS);

            // Blend Surface layers only when _GeneratedColorBlend is enabled (Plateau ground mesh vs volumetric rocks).
            float isTerrainSurface = step(0.25, _GeneratedColorBlend);
            float rawSand = rawControl.g;
            float rawRock = rawControl.b;
            float rawGrass = rawControl.r;
            float rawTotalLand = rawSand + rawRock;

            // Steep sides on plateau ground always pull toward rock even across sand-rock transitions.
            float rockShare = saturate(rawRock / max(0.001, rawTotalLand) + vertical * 0.72);
            float abyssFade = saturate(1.0 - max(rawSand, rawRock) * 1.15) * saturate(1.0 - rawGrass * 4.0);

            half3 plateauGround = lerp(marineSand, rock, (half)rockShare);
            float3 plateauGroundNormal = normalize(lerp(sandTiledNormalWS, rockTiledNormalWS, rockShare));

            // Seamless border match against neighbouring Ocean/Abyss chunks using _SeabedAlbedo.
            float macro = SAMPLE_TEXTURE2D_GRAD(_GrassAlbedo, sampler_GrassAlbedo, wpos * 0.02, positionDx.xz * 0.02, positionDy.xz * 0.02).g;
            half3 borderSeabed = (half3)seabedAlbedo * (half)(0.75 + macro * 0.5);
            plateauGround = lerp(plateauGround, borderSeabed, (half)abyssFade);
            plateauGroundNormal = normalize(lerp(plateauGroundNormal, seabedNormalWS, abyssFade));

            // Handle any adjacent Island corner extending above water on a shared world chunk.
            float aboveWaterIsland = saturate(rawGrass * 4.0) * smoothstep(_WaterLevel - 1.2, _WaterLevel + 0.2, input.positionWS.y);
            if (aboveWaterIsland > 0.01)
            {
                float grassTile = max(0.02, _GrassTiling);
                float3 grassTex = SAMPLE_TEXTURE2D_GRAD(_GrassAlbedo, sampler_GrassAlbedo, wpos * grassTile, positionDx.xz * grassTile, positionDy.xz * grassTile).rgb;
                half3 surfaceTint = (half3)SAMPLE_TEXTURE2D(_SurfaceTint, sampler_Control, input.uv * _SurfaceTint_ST.xy + _SurfaceTint_ST.zw).rgb;
                half3 islandApprox = lerp((half3)grassTex * surfaceTint, generatedSample * _BaseColor.rgb, 0.45h);
                plateauGround = lerp(plateauGround, islandApprox, (half)aboveWaterIsland);
            }

            half3 albedo = lerp(rock, plateauGround, (half)(_GeneratedColorBlend * isTerrainSurface));
            float3 baseDetailNormalWS = normalize(lerp(rockTiledNormalWS, plateauGroundNormal, isTerrainSurface));

            // Subtle submerged animated sunlight caustic shimmer on tabletop/ledges below water surface.
            float depthBelowWater = max(0.0, _WaterLevel - input.positionWS.y);
            float causticFade = saturate(depthBelowWater * 1.2) * exp(-depthBelowWater * 0.065) * upward * (1.0 - aboveWaterIsland);
            float causticPattern = SampleSurfaceCaustics(wpos, _Time.y);
            albedo += half3(0.14, 0.34, 0.42) * (half)(causticPattern * causticFade * _CausticStrength * 0.28);

            // Anno-style step-grid placement & influence disc overlay (renders on submerged plateaus!).
            half3 placementEmission = 0;
            float4 plateauOverlayState = float4(_PlacementOverlayState.x * isTerrainSurface, _PlacementOverlayState.yzw);
            half3 overlayTint;
            half overlayWeight;
            half overlayWaterAlpha;
            EvaluatePlacementOverlayCore(
                TEXTURE2D_ARGS(_PlacementMask, sampler_PlacementMask),
                input.placementData.xy,
                _PlacementGridParams.x,
                plateauOverlayState,
                overlayTint,
                overlayWeight,
                placementEmission,
                overlayWaterAlpha);
            albedo = lerp(albedo, albedo * overlayTint + overlayTint * 0.24h, saturate(overlayWeight));

            float detailHeight = macroRock * vertical * 0.54
                + strataWave * _StrataStrength * vertical * 0.24
                - crack * _CrackStrength * 0.32
                + ripple * rippleMask * isTerrainSurface * (1.0 - abyssFade);
            normalWS = (half3)EvaluateProceduralNormal((half3)baseDetailNormalWS, input.positionWS, detailHeight);

            surface = (SurfaceData)0;
            surface.albedo = albedo;
            surface.emission = placementEmission;
            surface.smoothness = _Smoothness;
            surface.normalTS = half3(0, 0, 1);
            surface.occlusion = 1;
            surface.alpha = 1;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_BRANCH if (_DebugView > 0.5)
                return half4(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                    input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb, 1);
            SurfaceData surface;
            half3 normalWS;
            EvaluatePlateauSurface(input, surface, normalWS);
            InputData inputData = (InputData)0;
            inputData.positionWS = input.positionWS;
            inputData.normalWS = normalWS;
            inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            inputData.shadowCoord = input.shadowCoord;
            inputData.fogCoord = input.fogFactor;
            inputData.vertexLighting = input.vertexLighting;
            inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
            inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            half3 color = UniversalFragmentPBR(inputData, surface).rgb;

            color = MixFog(color, input.fogFactor);
            return half4(color, 1.0h);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                half3 normalWS;
                EvaluatePlateauSurface(input, surface, normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TerrainMetaVertex
            #pragma fragment TerrainMetaFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            Varyings TerrainMetaVertex(Attributes input)
            {
                Varyings output = Vert(input);
                output.positionCS = MetaVertexPosition(input.positionOS, input.lightmapUV,
                    input.dynamicLightmapUV, unity_LightmapST, unity_DynamicLightmapST);
                return output;
            }
            half4 TerrainMetaFragment(Varyings input) : SV_Target
            {
                SurfaceData surface;
                half3 normalWS;
                EvaluatePlateauSurface(input, surface, normalWS);
                MetaInput meta = (MetaInput)0;
                meta.Albedo = surface.albedo;
                meta.Emission = surface.emission;
                return MetaFragment(meta);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings o = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings o = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
