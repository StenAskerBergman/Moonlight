Shader "Custom/UnderwaterPlateauMaterial"
{
    Properties
    {
        [MainTexture] _BaseMap ("Generated Surface Colour", 2D) = "white" {}
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
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
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
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
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
                    / max(0.0001, abs(determinant));
                return normalize(normalWS - surfaceGradient * _NormalStrength);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

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

                half3 generatedColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb
                    * _BaseColor.rgb;
                float generatedInfluence = _GeneratedColorBlend * smoothstep(0.08, 0.72, upward);
                half3 albedo = lerp(rock, generatedColor, generatedInfluence);

                float rippleWarp = FractalNoise(world.xz * 0.14 + 93.0) * 2.4;
                float rippleA = sin((world.x * 0.86 + world.z * 0.31) * _RippleScale + rippleWarp);
                float rippleB = sin((world.x * -0.24 + world.z * 0.91) * (_RippleScale * 0.57) - rippleWarp);
                float ripple = rippleA * 0.68 + rippleB * 0.32;
                float rippleMask = pow(upward, 4.0) * _RippleStrength * generatedInfluence;
                albedo *= 1.0 + ripple * rippleMask * 0.22;

                float detailHeight = macroRock * vertical * 0.54
                    + strataWave * _StrataStrength * vertical * 0.20
                    - crack * _CrackStrength * 0.28
                    + ripple * rippleMask;
                half3 normalWS = EvaluateProceduralNormal(geometricNormal, input.positionWS, detailHeight);

                Light mainLight = GetMainLight(input.shadowCoord);
                half normalLight = saturate(dot(normalWS, mainLight.direction));
                half attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 lighting = max(SampleSH(normalWS), half3(0.055, 0.072, 0.078));
                lighting += mainLight.color * (0.10 + normalLight * attenuation);

                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specularPower = lerp(12.0h, 88.0h, _Smoothness);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower)
                    * _Smoothness * attenuation;
                half3 color = albedo * lighting + mainLight.color * specular * lerp(0.025h, 0.18h, _Smoothness);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < lightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half diffuse = saturate(dot(normalWS, light.direction));
                    color += albedo * light.color * diffuse
                        * light.distanceAttenuation * light.shadowAttenuation;
                }
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
