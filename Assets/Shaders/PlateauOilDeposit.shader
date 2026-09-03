Shader "Moonlight/Plateau Oil Deposit"
{
    Properties
    {
        _BaseMap ("Oil Albedo", 2D) = "white" {}
        _BaseColor ("Oil Tint", Color) = (1, 1, 1, 1)

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1

        _HeightMap ("16-bit Height Map", 2D) = "white" {}
        _ParallaxMap ("Parallax Map", 2D) = "gray" {}
        _HeightScale ("Parallax Depth", Range(0, 0.5)) = 0.1
        _ParallaxBlend ("Parallax Map Blend", Range(0, 5)) = 1
        _ParallaxMinSteps ("Parallax Min Steps", Range(4, 64)) = 32
        _ParallaxMaxSteps ("Parallax Max Steps", Range(32, 128)) = 64

        _Smoothness ("Smoothness", Range(0, 1)) = 0.28

        _SandColorA ("Plateau Sand A", Color) = (0.75, 0.75, 0.7, 1)
        _SandColorB ("Plateau Sand B", Color) = (0.7, 0.7, 0.65, 1)
        _RockColor ("Plateau Rock", Color) = (0.4, 0.4, 0.4, 1)

        _EdgeSeed ("Edge Variation", Float) = 0
        _SandBlendStart ("Sand Blend Start", Range(0, 1)) = 0.48
        _FadeStart ("Edge Fade Start", Range(0, 1.2)) = 0.74
        _FadeEnd ("Edge Fade End", Range(0, 1.2)) = 0.98
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 300
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 tangentWS : TEXCOORD2;
                half3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            TEXTURE2D(_HeightMap);
            SAMPLER(sampler_HeightMap);

            TEXTURE2D(_ParallaxMap);
            SAMPLER(sampler_ParallaxMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;

                half4 _BaseColor;
                half4 _SandColorA;
                half4 _SandColorB;
                half4 _RockColor;

                half _BumpScale;
                half _Smoothness;

                half _HeightScale;
                half _ParallaxBlend;
                half _ParallaxMinSteps;
                half _ParallaxMaxSteps;

                float _EdgeSeed;

                half _SandBlendStart;
                half _FadeStart;
                half _FadeEnd;
            CBUFFER_END

            half SampleHeight(float2 uv)
            {
                half height16 = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, uv).r;
                half parallax = SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, uv).r;

                return saturate(lerp(height16, parallax, _ParallaxBlend));
            }

            float2 ParallaxOcclusionMapping(float2 uv, half3 viewDirectionTS)
            {
                float viewZ = max(abs(viewDirectionTS.z), 0.05);

                float viewAngle = saturate(abs(viewDirectionTS.z));
                int steps = (int)round(lerp(
                    _ParallaxMaxSteps,
                    _ParallaxMinSteps,
                    viewAngle));

                steps = clamp(steps, 4, 64);

                float layerDepth = 1.0 / steps;
                float currentLayerDepth = 0.0;

                float2 parallaxDirection =
                    (viewDirectionTS.xy / viewZ) * _HeightScale;

                float2 deltaUV = parallaxDirection / steps;
                float2 currentUV = uv;

                float currentDepth = 1.0 - SampleHeight(currentUV);

                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= steps || currentLayerDepth >= currentDepth)
                        break;

                    currentUV -= deltaUV;
                    currentDepth = 1.0 - SampleHeight(currentUV);
                    currentLayerDepth += layerDepth;
                }

                float2 previousUV = currentUV + deltaUV;

                float afterDepth =
                    currentDepth - currentLayerDepth;

                float beforeDepth =
                    (1.0 - SampleHeight(previousUV))
                    - (currentLayerDepth - layerDepth);

                float denominator = afterDepth - beforeDepth;

                float weight = abs(denominator) > 0.00001
                    ? saturate(afterDepth / denominator)
                    : 0.0;

                return lerp(currentUV, previousUV, weight);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;

                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                output.fogFactor =
                    ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 viewDirectionWS =
                    SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                half3 viewDirectionTS = half3(
                    dot(viewDirectionWS, input.tangentWS),
                    dot(viewDirectionWS, input.bitangentWS),
                    dot(viewDirectionWS, input.normalWS));

                float2 surfaceUV =
                    ParallaxOcclusionMapping(input.uv, viewDirectionTS);

                float2 centered = abs(input.uv * 2.0 - 1.0);

                float roundedSquare = pow(
                    pow(centered.x, 4.0)
                    + pow(centered.y, 4.0),
                    0.25);

                float broadNoise =
                    sin((input.uv.x * 7.1
                    + input.uv.y * 5.3
                    + _EdgeSeed) * 6.28318);

                broadNoise +=
                    sin((input.uv.x * 13.7
                    - input.uv.y * 9.1
                    + _EdgeSeed * 0.61) * 6.28318) * 0.5;

                float edgeCoordinate =
                    roundedSquare + broadNoise * 0.025;

                half edgeAlpha =
                    1.0h - smoothstep(
                        _FadeStart,
                        _FadeEnd,
                        edgeCoordinate);

                clip(edgeAlpha - 0.01h);

                half4 oil =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        surfaceUV)
                    * _BaseColor;

                half sedimentNoise =
                    saturate(broadNoise * 0.25h + 0.5h);

                half3 sand =
                    lerp(
                        _SandColorA.rgb,
                        _SandColorB.rgb,
                        sedimentNoise);

                half sandWeight =
                    smoothstep(
                        _SandBlendStart,
                        _FadeStart + 0.08h,
                        edgeCoordinate);

                half rockFleck =
                    smoothstep(
                        0.78h,
                        0.96h,
                        sedimentNoise)
                    * sandWeight
                    * (1.0h - edgeAlpha)
                    * 0.35h;

                half3 albedo =
                    lerp(
                        oil.rgb,
                        sand,
                        sandWeight * 0.82h);

                albedo =
                    lerp(
                        albedo,
                        _RockColor.rgb,
                        rockFleck);

                half4 packedNormal =
                    SAMPLE_TEXTURE2D(
                        _BumpMap,
                        sampler_BumpMap,
                        surfaceUV);

                half3 normalTS =
                    UnpackNormalScale(
                        packedNormal,
                        _BumpScale);

                normalTS =
                    normalize(
                        lerp(
                            normalTS,
                            half3(0, 0, 1),
                            sandWeight * 0.65h));

                half3 normalWS =
                    normalize(
                        TransformTangentToWorld(
                            normalTS,
                            half3x3(
                                input.tangentWS,
                                input.bitangentWS,
                                input.normalWS)));

                float4 shadowCoord =
                    TransformWorldToShadowCoord(input.positionWS);

                Light mainLight =
                    GetMainLight(shadowCoord);

                half diffuse =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction));

                half3 ambient =
                    SampleSH(normalWS);

                half3 lighting =
                    ambient
                    + mainLight.color
                    * diffuse
                    * mainLight.distanceAttenuation
                    * mainLight.shadowAttenuation;

                half3 halfDirection =
                    SafeNormalize(
                        mainLight.direction
                        + viewDirectionWS);

                half specular =
                    pow(
                        saturate(
                            dot(
                                normalWS,
                                halfDirection)),
                        lerp(
                            8.0h,
                            96.0h,
                            _Smoothness));

                half3 color =
                    albedo * lighting
                    + mainLight.color
                    * specular
                    * _Smoothness
                    * 0.22h;

                color =
                    MixFog(
                        color,
                        input.fogFactor);

                return half4(
                    color,
                    edgeAlpha * oil.a);
            }

            ENDHLSL
        }
    }
}