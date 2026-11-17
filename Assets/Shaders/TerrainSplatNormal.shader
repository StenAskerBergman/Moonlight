Shader "Moonlight/Terrain Splat (Normal Mapped)"
{
    // Four tiled material layers blended by an RGBA control map.
    //
    // WHY THIS EXISTS. The terrain previously rendered one baked colour per texel through
    // URP/Lit: 8 texels per world unit, no normal map, no tiled detail. Reference coastlines
    // show individual pebbles and wet rock, which is roughly two orders of magnitude more
    // detail than a baked map at that density can hold. Detail has to come from tiled material
    // textures sampled at their own frequency, with the baked map reduced to deciding only
    // WHICH material - which it can do at low resolution.
    //
    // TextureBuilder supplies surface weights and climate colour from terrain sampling.
    // Every render pass shares the same material evaluation.
    Properties
    {
        _BaseMap ("Diagnostic Colour", 2D) = "white" {}
        _DebugView ("Display Diagnostic Colour", Float) = 0
        _SurfaceTint ("Climate Surface Colour (A snow coverage)", 2D) = "white" {}
        _UseSurfaceTint ("Use Climate Surface Colour", Float) = 0
        _Control ("Control (R grass, G sand, B rock, A seabed)", 2D) = "black" {}

        [Header(Layer R   Grass)]
        _GrassAlbedo ("Grass Albedo", 2D) = "white" {}
        _GrassNormal ("Grass Normal", 2D) = "bump" {}
        _GrassTint ("Grass Tint", Color) = (1,1,1,1)
        _GrassTiling ("Grass Tiling", Float) = 0.25
        _GrassNormalScale ("Grass Normal Scale", Range(0,3)) = 1
        _GrassSmoothness ("Grass Smoothness", Range(0,1)) = 0.15

        [Header(Layer G   Sand or Shingle)]
        _SandAlbedo ("Sand Albedo", 2D) = "white" {}
        _SandNormal ("Sand Normal", 2D) = "bump" {}
        _SandTint ("Sand Tint", Color) = (1,1,1,1)
        _SandTiling ("Sand Tiling", Float) = 0.5
        _SandNormalScale ("Sand Normal Scale", Range(0,3)) = 1
        _SandSmoothness ("Sand Smoothness", Range(0,1)) = 0.3

        [Header(Layer B   Rock   triplanar)]
        _RockAlbedo ("Rock Albedo", 2D) = "white" {}
        _RockNormal ("Rock Normal", 2D) = "bump" {}
        _RockTint ("Rock Tint", Color) = (1,1,1,1)
        _RockTiling ("Rock Tiling", Float) = 0.18
        _RockNormalScale ("Rock Normal Scale", Range(0,3)) = 1
        _RockSmoothness ("Rock Smoothness", Range(0,1)) = 0.2

        [Header(Layer A   Seabed)]
        _SeabedAlbedo ("Seabed Albedo", 2D) = "white" {}
        _SeabedNormal ("Seabed Normal", 2D) = "bump" {}
        _SeabedTint ("Seabed Tint", Color) = (1,1,1,1)
        _SeabedTiling ("Seabed Tiling", Float) = 0.3
        _SeabedNormalScale ("Seabed Normal Scale", Range(0,3)) = 1
        _SeabedSmoothness ("Seabed Smoothness", Range(0,1)) = 0.45

        [Header(Region   alternate tints)]
        // Part of an island reading as a different place, without a second material or a second
        // mesh. The mask is authored by TextureBuilder from IslandTerrainProvider's region field;
        // an island with no region leaves it black and every layer keeps its base tint.
        _RegionMask ("Region Mask (R)", 2D) = "black" {}
        _GrassTintRegion ("Grass Tint (region)", Color) = (1,1,1,1)
        _SandTintRegion ("Sand Tint (region)", Color) = (1,1,1,1)
        _RockTintRegion ("Rock Tint (region)", Color) = (1,1,1,1)
        _SeabedTintRegion ("Seabed Tint (region)", Color) = (1,1,1,1)
        _RegionSmoothnessScale ("Region Smoothness Scale", Range(0,3)) = 1

        [Header(Detail break up)]
        _MacroTiling ("Macro Variation Tiling", Float) = 0.02
        _MacroStrength ("Macro Variation Strength", Range(0,1)) = 0.25

        [Header(Placement and Influence Overlay)]
        [HideInInspector] _PlacementMask ("Placement Mask", 2D) = "black" {}
        [HideInInspector] _PlacementOverlayState ("Placement Overlay State", Vector) = (0, 0, 0, 1)
        [HideInInspector] _PlacementGridParams ("Placement Grid Params", Vector) = (100, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Placement/PlacementOverlayCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Control_ST, _BaseMap_ST, _SurfaceTint_ST, _RegionMask_ST;
            float _DebugView, _UseSurfaceTint;
            float4 _GrassTint, _SandTint, _RockTint, _SeabedTint;
            float _GrassTiling, _SandTiling, _RockTiling, _SeabedTiling;
            float _GrassNormalScale, _SandNormalScale, _RockNormalScale, _SeabedNormalScale;
            float _GrassSmoothness, _SandSmoothness, _RockSmoothness, _SeabedSmoothness;
            float _MacroTiling, _MacroStrength;
            float4 _GrassTintRegion, _SandTintRegion, _RockTintRegion, _SeabedTintRegion;
            float _RegionSmoothnessScale;
            float4 _PlacementMask_ST;
            float4 _PlacementOverlayState;
            float4 _PlacementGridParams;
        CBUFFER_END

        TEXTURE2D(_Control);       SAMPLER(sampler_Control);
        TEXTURE2D(_RegionMask);
        TEXTURE2D(_BaseMap);
        TEXTURE2D(_SurfaceTint);
        TEXTURE2D(_PlacementMask); SAMPLER(sampler_PlacementMask);

        TEXTURE2D(_GrassAlbedo);   SAMPLER(sampler_GrassAlbedo);
        TEXTURE2D(_GrassNormal);   SAMPLER(sampler_GrassNormal);
        TEXTURE2D(_SandAlbedo);    SAMPLER(sampler_SandAlbedo);
        TEXTURE2D(_SandNormal);    SAMPLER(sampler_SandNormal);
        TEXTURE2D(_RockAlbedo);    SAMPLER(sampler_RockAlbedo);
        TEXTURE2D(_RockNormal);    SAMPLER(sampler_RockNormal);
        TEXTURE2D(_SeabedAlbedo);  SAMPLER(sampler_SeabedAlbedo);
        TEXTURE2D(_SeabedNormal);  SAMPLER(sampler_SeabedNormal);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
            float2 lightmapUV : TEXCOORD1;
            float2 dynamicLightmapUV : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS   : TEXCOORD1;
            float2 uv         : TEXCOORD2;
            float  fogCoord   : TEXCOORD3;
            float4 shadowCoord: TEXCOORD4;
            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5);
            half3 vertexLighting : TEXCOORD6;
            float3 placementData : TEXCOORD7;
            UNITY_VERTEX_OUTPUT_STEREO
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        Varyings Vertex(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs n = GetVertexNormalInputs(input.normalOS);

            o.positionCS = p.positionCS;
            o.positionWS = p.positionWS;
            o.normalWS = n.normalWS;
            o.uv = input.uv;
            o.placementData = float3(
                input.positionOS.xz / max(_PlacementGridParams.y, 1e-4),
                input.positionOS.y);
            OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, o.lightmapUV);
            OUTPUT_SH(n.normalWS, o.vertexSH);
            o.vertexLighting = VertexLighting(p.positionWS, n.normalWS);
            o.fogCoord = ComputeFogFactor(p.positionCS.z);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                o.shadowCoord = GetShadowCoord(p);
            #endif
            return o;
        }

        // Planar UVs increase along world X and Z. Project the detail gradient onto
        // the geometric surface: neutral maps preserve it, including vertical faces.
        float3 PlanarNormalWS(float3 detail, float3 normalWS)
        {
            float3 gradient = float3(detail.x, 0, detail.y) / max(detail.z, 0.1);
            return normalize(normalWS + gradient - normalWS * dot(gradient, normalWS));
        }

        void SampleLayer(
            TEXTURE2D_PARAM(albedoTex, albedoSampler), TEXTURE2D_PARAM(normalTex, normalSampler),
            float2 uv, float2 uvDx, float2 uvDy, float normalScale,
            out float3 albedo, out float3 normalTS)
        {
            albedo = SAMPLE_TEXTURE2D_GRAD(albedoTex, albedoSampler, uv, uvDx, uvDy).rgb;
            normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalTex, normalSampler, uv, uvDx, uvDy), normalScale);
        }

        // Triplanar for rock only. A vertical cliff sampled with world-XZ UVs smears the
        // texture along the face; every other layer here sits on near-horizontal ground where
        // planar mapping is correct and cheaper.
        void SampleRockTriplanar(
            float3 positionWS, float3 positionDx, float3 positionDy, float3 normalWS, float tiling, float normalScale,
            out float3 albedo, out float3 shadedNormalWS)
        {
            float3 blend = pow(abs(normalWS), 4.0);
            blend /= max(blend.x + blend.y + blend.z, 1e-4);

            float2 uvX = positionWS.zy * tiling;
            float2 uvY = positionWS.xz * tiling;
            float2 uvZ = positionWS.xy * tiling;

            albedo = SAMPLE_TEXTURE2D_GRAD(_RockAlbedo, sampler_RockAlbedo, uvX, positionDx.zy * tiling, positionDy.zy * tiling).rgb * blend.x
                   + SAMPLE_TEXTURE2D_GRAD(_RockAlbedo, sampler_RockAlbedo, uvY, positionDx.xz * tiling, positionDy.xz * tiling).rgb * blend.y
                   + SAMPLE_TEXTURE2D_GRAD(_RockAlbedo, sampler_RockAlbedo, uvZ, positionDx.xy * tiling, positionDy.xy * tiling).rgb * blend.z;

            float3 nX = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_RockNormal, sampler_RockNormal, uvX, positionDx.zy * tiling, positionDy.zy * tiling), normalScale);
            float3 nY = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_RockNormal, sampler_RockNormal, uvY, positionDx.xz * tiling, positionDy.xz * tiling), normalScale);
            float3 nZ = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_RockNormal, sampler_RockNormal, uvZ, positionDx.xy * tiling, positionDy.xy * tiling), normalScale);
            // Reorient each projection into world coordinates before blending. UV axes
            // stay world-aligned on both signs of a face; the geometric normal
            // supplies the outward direction. Neutral maps return normalWS exactly.
            float3 gradient = float3(0, nX.y, nX.x) / max(nX.z, 0.1) * blend.x
                            + float3(nY.x, 0, nY.y) / max(nY.z, 0.1) * blend.y
                            + float3(nZ.x, nZ.y, 0) / max(nZ.z, 0.1) * blend.z;
            shadedNormalWS = normalize(normalWS + gradient - normalWS * dot(gradient, normalWS));
        }

        void EvaluateSurface(Varyings input, out SurfaceData surfaceData, out float3 shadedNormalWS)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            float4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, input.uv * _Control_ST.xy + _Control_ST.zw);
            float total = dot(control, float4(1, 1, 1, 1));
            control = total > 1e-4 ? control / total : float4(0, 0, 0, 1);

            float3 normalWS = normalize(input.normalWS);
            float2 wpos = input.positionWS.xz;

            float3 positionDx = ddx(input.positionWS), positionDy = ddy(input.positionWS);
            float3 aGrass = 0, aSand = 0, aRock = 0, aSeabed = 0;
            float3 nGrass = normalWS, nSand = normalWS, nRock = normalWS, nSeabed = normalWS;
            float3 detail;
            // Explicit gradients keep mip selection valid across weight branches.
            UNITY_BRANCH if (control.r > 0)
            {
                SampleLayer(TEXTURE2D_ARGS(_GrassAlbedo, sampler_GrassAlbedo), TEXTURE2D_ARGS(_GrassNormal, sampler_GrassNormal),
                    wpos * _GrassTiling, positionDx.xz * _GrassTiling, positionDy.xz * _GrassTiling,
                    _GrassNormalScale, aGrass, detail);
                nGrass = PlanarNormalWS(detail, normalWS);
            }
            UNITY_BRANCH if (control.g > 0)
            {
                SampleLayer(TEXTURE2D_ARGS(_SandAlbedo, sampler_SandAlbedo), TEXTURE2D_ARGS(_SandNormal, sampler_SandNormal),
                    wpos * _SandTiling, positionDx.xz * _SandTiling, positionDy.xz * _SandTiling,
                    _SandNormalScale, aSand, detail);
                nSand = PlanarNormalWS(detail, normalWS);
            }
            UNITY_BRANCH if (control.a > 0)
            {
                SampleLayer(TEXTURE2D_ARGS(_SeabedAlbedo, sampler_SeabedAlbedo), TEXTURE2D_ARGS(_SeabedNormal, sampler_SeabedNormal),
                    wpos * _SeabedTiling, positionDx.xz * _SeabedTiling, positionDy.xz * _SeabedTiling,
                    _SeabedNormalScale, aSeabed, detail);
                nSeabed = PlanarNormalWS(detail, normalWS);
            }
            UNITY_BRANCH if (control.b > 0)
                SampleRockTriplanar(input.positionWS, positionDx, positionDy, normalWS,
                    _RockTiling, _RockNormalScale, aRock, nRock);

            // Tint-only regional variation: the same four tiled materials, recoloured. The
            // mask is continuous (smoothstepped in the provider), so this cannot introduce the
            // hard-edged value step that serrates against the texel grid.
            float region = SAMPLE_TEXTURE2D(_RegionMask, sampler_Control, input.uv * _RegionMask_ST.xy + _RegionMask_ST.zw).r;
            float3 grassTint  = lerp(_GrassTint.rgb,  _GrassTintRegion.rgb,  region);
            float3 sandTint   = lerp(_SandTint.rgb,   _SandTintRegion.rgb,   region);
            float3 rockTint   = lerp(_RockTint.rgb,   _RockTintRegion.rgb,   region);
            float3 seabedTint = lerp(_SeabedTint.rgb, _SeabedTintRegion.rgb, region);

            float3 albedo = aGrass * grassTint * control.r + aSand * sandTint * control.g
                          + aRock * rockTint * control.b + aSeabed * seabedTint * control.a;
            float snowCoverage = 0;
            UNITY_BRANCH if (_UseSurfaceTint > 0.5)
            {
                float3 regionalDetail =
                    aGrass * lerp(1.0, _GrassTintRegion.rgb / max(_GrassTint.rgb, 0.001), region) * control.r
                  + aSand * lerp(1.0, _SandTintRegion.rgb / max(_SandTint.rgb, 0.001), region) * control.g
                  + aRock * lerp(1.0, _RockTintRegion.rgb / max(_RockTint.rgb, 0.001), region) * control.b
                  + aSeabed * lerp(1.0, _SeabedTintRegion.rgb / max(_SeabedTint.rgb, 0.001), region) * control.a;
                float4 surfaceTint = SAMPLE_TEXTURE2D(_SurfaceTint, sampler_Control,
                    input.uv * _SurfaceTint_ST.xy + _SurfaceTint_ST.zw);
                snowCoverage = saturate(surfaceTint.a);
                albedo = lerp(regionalDetail, 1.0, snowCoverage) * surfaceTint.rgb;
            }

            shadedNormalWS = normalize(
                  nGrass * control.r + nSand * control.g + nRock * control.b + nSeabed * control.a);
            shadedNormalWS = normalize(lerp(shadedNormalWS, normalWS, snowCoverage));

            float smoothness = _GrassSmoothness * control.r + _SandSmoothness * control.g
                             + _RockSmoothness * control.b + _SeabedSmoothness * control.a;
            // Wet volcanic rock and dry ash differ in gloss as much as in colour, so the region
            // scales this too. 1 leaves the surface response untouched.
            smoothness = saturate(smoothness * lerp(1.0, _RegionSmoothnessScale, region));
            smoothness = lerp(smoothness, 0.25, snowCoverage);

            // Low-frequency tint variation, so a single tiled texture does not read as a
            // repeating grid across a 90 unit island.
            UNITY_BRANCH if (_MacroStrength > 0)
            {
                float macro = SAMPLE_TEXTURE2D_GRAD(_GrassAlbedo, sampler_GrassAlbedo,
                    wpos * _MacroTiling, positionDx.xz * _MacroTiling, positionDy.xz * _MacroTiling).g;
                albedo *= lerp(1.0, 0.6 + macro * 0.8, _MacroStrength);
            }
            half3 placementEmission = 0;
            float aboveWaterMask = step(_PlacementGridParams.z - 0.03, input.placementData.z);
            float4 overlayState = float4(_PlacementOverlayState.x * aboveWaterMask, _PlacementOverlayState.yzw);
            half3 overlayTint;
            half overlayWeight;
            half overlayWaterAlpha;
            EvaluatePlacementOverlayCore(
                TEXTURE2D_ARGS(_PlacementMask, sampler_PlacementMask),
                input.placementData.xy,
                _PlacementGridParams.x,
                overlayState,
                overlayTint,
                overlayWeight,
                placementEmission,
                overlayWaterAlpha);
            albedo = lerp(albedo, albedo * overlayTint + overlayTint * 0.25h, saturate(overlayWeight));

            surfaceData = (SurfaceData)0;
            surfaceData.albedo = albedo;
            surfaceData.emission = placementEmission;
            surfaceData.smoothness = smoothness;
            surfaceData.normalTS = float3(0, 0, 1);
            surfaceData.occlusion = 1;
            surfaceData.alpha = 1;
        }

        half4 Fragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            UNITY_BRANCH if (_DebugView > 0.5)
                return half4(SAMPLE_TEXTURE2D(_BaseMap, sampler_Control,
                    input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb, 1);
            SurfaceData surfaceData;
            float3 shadedNormalWS;
            EvaluateSurface(input, surfaceData, shadedNormalWS);
            InputData inputData = (InputData)0;
            inputData.positionWS = input.positionWS;
            inputData.normalWS = shadedNormalWS;
            inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
            inputData.fogCoord = input.fogCoord;
            inputData.vertexLighting = input.vertexLighting;
            inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, shadedNormalWS);
            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

            half4 color = UniversalFragmentPBR(inputData, surfaceData);
            color.rgb = MixFog(color.rgb, input.fogCoord);
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                float3 normalWS;
                EvaluateSurface(input, surface, normalWS);
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
                Varyings output = Vertex(input);
                output.positionCS = MetaVertexPosition(input.positionOS, input.lightmapUV,
                    input.dynamicLightmapUV, unity_LightmapST, unity_DynamicLightmapST);
                return output;
            }
            half4 TerrainMetaFragment(Varyings input) : SV_Target
            {
                SurfaceData surface;
                float3 normalWS;
                EvaluateSurface(input, surface, normalWS);
                MetaInput meta = (MetaInput)0;
                meta.Albedo = surface.albedo;
                return MetaFragment(meta);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
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

    FallBack "Universal Render Pipeline/Lit"
}
