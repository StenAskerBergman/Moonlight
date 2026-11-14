Shader "Custom/UnderwaterPlateauMaterial"
{
    Properties
    {
        [MainTexture] _BaseMap ("Generated Surface Colour", 2D) = "white" {}
        _Control ("Material Weights (R grass, G sand, B rock, A seabed)", 2D) = "black" {}
        _DebugView ("Display Diagnostic Colour", Float) = 0
        [MainColor] _BaseColor ("Base Tint", Color) = (1, 1, 1, 1)
        _GeneratedColorBlend ("Generated Colour Blend", Range(0, 1)) = 0

        [Header(Rock Palette)]
        _RockDarkColor ("Crevice Colour", Color) = (0.075, 0.12, 0.13, 1)
        _RockMidColor ("Stone Colour", Color) = (0.22, 0.31, 0.32, 1)
        _RockLightColor ("Worn Face Colour", Color) = (0.48, 0.57, 0.56, 1)
        _SedimentColor ("Ledge Sediment", Color) = (0.55, 0.58, 0.52, 1)
        _AlgaeColor ("Algae Colour", Color) = (0.13, 0.28, 0.22, 1)

        [Header(Procedural Detail)]
        _RockScale ("Rock Detail Scale", Range(0.05, 4)) = 0.72
        _StrataScale ("Strata Scale", Range(0.05, 3)) = 0.42
        _StrataStrength ("Strata Strength", Range(0, 1)) = 0.72
        _CrackStrength ("Crevice Strength", Range(0, 1)) = 0.55
        _SedimentStrength ("Top Sediment", Range(0, 1)) = 0.42
        _AlgaeStrength ("Algae Strength", Range(0, 1)) = 0.34
        _RippleScale ("Sand Ripple Scale", Range(0.2, 12)) = 3.2
        _RippleStrength ("Sand Ripple Strength", Range(0, 1)) = 0.18
        _NormalStrength ("Procedural Normal Strength", Range(0, 1)) = 0.24
        _Smoothness ("Wetness", Range(0, 1)) = 0.48
        _WorldSeed ("World Seed", Float) = 0

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
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TEXTURE2D(_Control);
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _Control_ST;
            half _DebugView;
            half4 _BaseColor;
            half4 _RockDarkColor;
            half4 _RockMidColor;
            half4 _RockLightColor;
            half4 _SedimentColor;
            half4 _AlgaeColor;
            half _GeneratedColorBlend;
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

        void EvaluatePlateauSurface(Varyings input, out SurfaceData surface, out half3 normalWS)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 generatedSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
            float4 materialWeights = SAMPLE_TEXTURE2D(_Control, sampler_BaseMap,
                input.uv * _Control_ST.xy + _Control_ST.zw);
            float sandWeight = materialWeights.g / max(dot(materialWeights, float4(1, 1, 1, 1)), 0.0001);

            half3 geometricNormal = normalize(input.normalWS);
            float upward = saturate((geometricNormal.y - 0.08) / 0.82);
            float vertical = 1.0 - smoothstep(0.18, 0.88, upward);
            float3 world = input.positionWS + float3(_WorldSeed * 1.73, 0.0, _WorldSeed * -2.11);

            float horizontalRock = FractalNoise(world.xz * _RockScale * 0.46);
            float sideX = FractalNoise(world.zy * float2(_RockScale * 0.34, _RockScale * 0.10));
            float sideZ = FractalNoise(world.xy * float2(_RockScale * 0.34, _RockScale * 0.10));
            float sideBlend = abs(geometricNormal.x) / max(0.001, abs(geometricNormal.x) + abs(geometricNormal.z));
            float verticalGrain = lerp(sideZ, sideX, sideBlend);
            float macroRock = lerp(horizontalRock, verticalGrain, vertical);

            float warpedHeight = world.y * _StrataScale
                + FractalNoise(world.xz * 0.11 + 5.4) * 1.55;
            float strataWave = sin(warpedHeight * 6.2831853);
            float strata = strataWave * 0.5 + 0.5;
            float ledge = smoothstep(0.58, 0.94, strata)
                * smoothstep(0.18, 0.66, FractalNoise(world.xz * 0.27 - 3.1));

            float crackNoise = FractalNoise(world.xz * (_RockScale * 1.85) + 31.0);
            float crack = 1.0 - smoothstep(0.035, 0.14, abs(crackNoise - 0.50));
            crack *= lerp(0.42, 1.0, vertical);

            half3 rock = lerp(_RockDarkColor.rgb, _RockMidColor.rgb, smoothstep(0.16, 0.78, macroRock));
            rock = lerp(rock, _RockLightColor.rgb,
                saturate(ledge * _StrataStrength * 0.72 + smoothstep(0.72, 0.94, macroRock) * 0.34));
            rock = lerp(rock, _RockDarkColor.rgb * 0.48, crack * _CrackStrength);

            float sedimentNoise = FractalNoise(world.xz * 0.22 + 71.0);
            float sedimentMask = pow(upward, 3.2) * _SedimentStrength
                * smoothstep(0.22, 0.72, sedimentNoise + ledge * 0.42);
            rock = lerp(rock, _SedimentColor.rgb, saturate(sedimentMask));

            float algaeNoise = FractalNoise(world.xz * 0.31 - 41.0);
            float algaeMask = _AlgaeStrength
                * smoothstep(0.48, 0.82, algaeNoise + crack * 0.26)
                * lerp(1.0, 0.38, upward);
            rock = lerp(rock, _AlgaeColor.rgb, saturate(algaeMask));

            half3 generatedColor = generatedSample * _BaseColor.rgb;
            float generatedInfluence = _GeneratedColorBlend * smoothstep(0.08, 0.72, upward);
            half3 albedo = lerp(rock, generatedColor, generatedInfluence);

            float rippleWarp = FractalNoise(world.xz * 0.14 + 93.0) * 2.4;
            float rippleA = sin((world.x * 0.86 + world.z * 0.31) * _RippleScale + rippleWarp);
            float rippleB = sin((world.x * -0.24 + world.z * 0.91) * (_RippleScale * 0.57) - rippleWarp);
            float ripple = rippleA * 0.68 + rippleB * 0.32;
            float rippleMask = pow(upward, 4.0) * _RippleStrength * generatedInfluence * sandWeight;
            albedo *= 1.0 + ripple * rippleMask * 0.22;

            float detailHeight = macroRock * vertical * 0.54
                + strataWave * _StrataStrength * vertical * 0.20
                - crack * _CrackStrength * 0.28
                + ripple * rippleMask;
            normalWS = EvaluateProceduralNormal(geometricNormal, input.positionWS, detailHeight);

            surface = (SurfaceData)0;
            surface.albedo = albedo;
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
