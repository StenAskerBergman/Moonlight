Shader "Moonlight/Shore Ocean"
{
    // The open sea, shaped by the coast it meets.
    //
    // Replaces "Ocean Shader Graph" on a 24-vertex cube, whose only motion was the whole sea
    // heaving +/-0.1 as one rigid sheet (4 vertices 540 m apart), whose shoreline alpha went
    // opaque within 9 cm of depth, and whose foam was a static band measured in camera-ray
    // depth (so it changed width whenever the camera moved).
    //
    // Here, on a tessellated surface built by OceanSurfaceBuilder:
    //   * open-water swell is a sum of Gerstner waves that dies away over shallow water;
    //   * near every island a shore-bound wave train, driven by a baked distance-to-coast
    //     field, rolls in toward that island's own coastline and breaks into foam;
    //   * colour, transparency and waterline foam come from TRUE vertical water depth,
    //     reconstructed from the depth buffer, so they do not depend on camera angle;
    //   * the swash on the sand is drawn by the terrain shader from the same timing
    //     (OceanShoreCommon.hlsl), so water and beach stay in phase.
    Properties
    {
        [Header(Colour and depth)]
        _ShallowTint ("Shallow Tint", Color) = (0.82, 0.97, 0.95, 1)
        _DeepColor ("Deep Colour", Color) = (0.015, 0.13, 0.21, 1)
        _Absorption ("Absorption per metre (RGB)", Vector) = (0.42, 0.12, 0.085, 0)
        _ViewPathWeight ("View Path Contribution", Range(0, 1)) = 0.3
        _EdgeFade ("Shoreline Edge Fade (m of depth)", Range(0.01, 1.5)) = 0.3

        [Header(Surface)]
        [NoScaleOffset][Normal] _NormalMap ("Detail Normal Map", 2D) = "bump" {}
        _NormalTiling ("Detail Tiling A, B (per metre)", Vector) = (0.085, 0.13, 0, 0)
        _NormalScroll ("Detail Scroll A.xy B.zw (m/s)", Vector) = (0.035, 0.02, -0.025, 0.03)
        _NormalStrength ("Detail Normal Strength", Range(0, 2)) = 0.55
        _DetailFadeDistance ("Detail Fade Distance", Float) = 220
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.8
        _FresnelBias ("Fresnel Bias", Range(0, 1)) = 0.02
        _SpecularStrength ("Sun Glint", Range(0, 8)) = 2.5
        _SpecularPower ("Sun Glint Sharpness", Range(8, 1024)) = 256
        _RefractionStrength ("Refraction", Range(0, 0.1)) = 0.025

        [Header(Open water swell   dir.xy height length)]
        _SwellA ("Swell A", Vector) = (0.8, 0.6, 0.09, 24)
        _SwellB ("Swell B", Vector) = (-0.35, 0.94, 0.05, 13)
        _SwellC ("Swell C", Vector) = (0.97, -0.25, 0.03, 7.5)
        _SwellSteepness ("Swell Steepness", Range(0, 1)) = 0.45
        _SwellSpeed ("Swell Speed", Range(0, 2)) = 0.7
        _SwellShallowDepth ("Swell Dies Out Above Depth (m)", Range(0.5, 20)) = 8

        [Header(Foam)]
        [NoScaleOffset] _FoamTex ("Foam Texture", 2D) = "white" {}
        _FoamColor ("Foam Colour", Color) = (0.95, 0.98, 1, 1)
        _FoamTiling ("Foam Tiling (per metre)", Float) = 0.18
        _FoamSoftness ("Foam Softness", Range(0.01, 0.5)) = 0.12
        _ContactFoamDepth ("Waterline Foam Depth (m)", Range(0.01, 2)) = 0.45
        _BreakerFoam ("Breaker Foam", Range(0, 2)) = 1
        _WhitecapFoam ("Open Water Whitecaps", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanVertex
            #pragma fragment OceanFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "OceanShoreCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowTint, _DeepColor, _Absorption;
                float _ViewPathWeight, _EdgeFade;
                float4 _NormalTiling, _NormalScroll;
                float _NormalStrength, _DetailFadeDistance, _Smoothness, _ReflectionStrength;
                float _FresnelBias, _SpecularStrength, _SpecularPower, _RefractionStrength;
                float4 _SwellA, _SwellB, _SwellC;
                float _SwellSteepness, _SwellSpeed, _SwellShallowDepth;
                float4 _FoamColor;
                float _FoamTiling, _FoamSoftness, _ContactFoamDepth, _BreakerFoam, _WhitecapFoam;
            CBUFFER_END

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamTex);   SAMPLER(sampler_FoamTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 restXZ     : TEXCOORD2;   // undisplaced position: stable texture space
                float  fogFactor  : TEXCOORD3;
                float  whitecap   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // One Gerstner component. wave = (dir.x, dir.z, height, wavelength).
            void AddGerstner(float4 wave, float2 xz, float attenuation,
                inout float3 offset, inout float3 tangent, inout float3 binormal, inout float crest)
            {
                float2 d = normalize(wave.xy + float2(1e-5, 0));
                float k = OCEAN_TWO_PI / max(wave.w, 0.5);
                float c = sqrt(9.81 / k) * _SwellSpeed;
                float a = wave.z * attenuation;
                float q = _SwellSteepness;
                float f = k * (dot(d, xz) - c * _Time.y);
                float sf = sin(f);
                float cf = cos(f);

                offset += float3(q * a * d.x * cf, a * sf, q * a * d.y * cf);
                tangent += float3(-q * a * k * d.x * d.x * sf, a * k * d.x * cf, -q * a * k * d.x * d.y * sf);
                binormal += float3(-q * a * k * d.x * d.y * sf, a * k * d.y * cf, -q * a * k * d.y * d.y * sf);
                crest += saturate(sf) * wave.z;
            }

            Varyings OceanVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 rest = positionWS.xz;
                float edgeFade = OceanDenseFade(rest);
                OceanShoreSample shore = SampleOceanShore(rest);

                // Swell shoals away over shallow water and right next to a coast, so open-water
                // motion never reaches the beach - the shore train below owns that zone.
                float swellAttenuation = edgeFade
                    * smoothstep(0.5, _SwellShallowDepth, shore.depth)
                    * smoothstep(1.0, 10.0, shore.distance);

                float3 offset = 0;
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float crest = 0;
                AddGerstner(_SwellA, rest, swellAttenuation, offset, tangent, binormal, crest);
                AddGerstner(_SwellB, rest, swellAttenuation, offset, tangent, binormal, crest);
                AddGerstner(_SwellC, rest, swellAttenuation, offset, tangent, binormal, crest);

                float shoreHeight, shoreSlope;
                OceanShoreWave(shore, rest, shoreHeight, shoreSlope);
                shoreHeight *= edgeFade;
                shoreSlope *= edgeFade;
                offset.y += shoreHeight;
                tangent.y += shoreSlope * shore.seaward.x;
                binormal.y += shoreSlope * shore.seaward.y;

                positionWS += offset;

                o.positionWS = positionWS;
                o.normalWS = normalize(cross(binormal, tangent));
                o.restXZ = rest;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                float swellTotal = max(_SwellA.z + _SwellB.z + _SwellC.z, 1e-3);
                o.whitecap = saturate(crest / swellTotal) * swellAttenuation;
                return o;
            }

            float SceneEyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        rawDepth = 1.0 - rawDepth;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
                }
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            half4 OceanFragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                float2 xz = input.restXZ;
                float t = _Time.y;
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                float surfaceEye = -TransformWorldToView(positionWS).z;
                float viewDistance = length(_WorldSpaceCameraPos - positionWS);

                // --- normals: geometric waves + two scrolling detail layers in WORLD space ---
                float detailFade = lerp(0.35, 1.0, saturate(1.0 - viewDistance / max(_DetailFadeDistance, 1.0)));
                float2 uvA = xz * _NormalTiling.x + _NormalScroll.xy * t;
                float2 uvB = xz * _NormalTiling.y + _NormalScroll.zw * t;
                float3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA), _NormalStrength * detailFade);
                float3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB), _NormalStrength * detailFade);
                float2 detail = nA.xy + nB.xy;   // whiteout blend; texture x -> world x, y -> world z
                float3 geometricNormal = normalize(input.normalWS);
                float3 normalWS = normalize(geometricNormal + float3(detail.x, 0, detail.y));

                bool frontFace = IS_FRONT_VFACE(isFrontFace, true, false);
                if (!frontFace)
                {
                    // Seen from below (diving camera): a dim, tinted ceiling. The underwater
                    // transition feature owns the rest of the underwater look.
                    float3 below = SampleSceneColor(screenUV) * _ShallowTint.rgb * 0.55 + _DeepColor.rgb * 0.45;
                    return half4(MixFog(below, input.fogFactor), 1);
                }

                // --- true vertical water depth from the depth buffer ---
                float sceneEye = SceneEyeDepth(SampleSceneDepth(screenUV));
                float3 rayPerEye = unity_OrthoParams.w > 0.5
                    ? -UNITY_MATRIX_V[2].xyz
                    : (positionWS - _WorldSpaceCameraPos) / max(surfaceEye, 1e-4);
                float eyeDelta = max(sceneEye - surfaceEye, 0.0);
                float3 scenePositionWS = positionWS + rayPerEye * eyeDelta;
                float verticalDepth = max(positionWS.y - scenePositionWS.y, 0.0);
                float viewPath = length(rayPerEye) * eyeDelta;

                // --- refraction, rejecting samples of objects in front of the water ---
                float2 refractUV = screenUV + normalWS.xz * _RefractionStrength * saturate(verticalDepth);
                float refractEye = SceneEyeDepth(SampleSceneDepth(refractUV));
                if (refractEye < surfaceEye) refractUV = screenUV;
                float3 sceneColor = SampleSceneColor(refractUV);

                // --- absorption: shallow water shows the ground, deep water its own colour ---
                float optical = verticalDepth + viewPath * _ViewPathWeight;
                float3 transmittance = exp(-optical * _Absorption.rgb);
                float3 waterColor = lerp(_DeepColor.rgb, sceneColor * _ShallowTint.rgb, transmittance);

                // --- lighting ---
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float nDotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = _FresnelBias + (1.0 - _FresnelBias) * pow(1.0 - nDotV, 5.0);
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                float3 environment = GlossyEnvironmentReflection(reflectDir, 1.0 - _Smoothness, 1.0);
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float glint = pow(saturate(dot(normalWS, halfDir)), _SpecularPower) * _SpecularStrength * shadow;

                float3 color = lerp(waterColor, environment, saturate(fresnel * _ReflectionStrength));
                color += glint * mainLight.color;

                // --- foam ---
                OceanShoreSample shore = SampleOceanShore(xz);
                float2 foamUV = xz * _FoamTiling;
                float foamTexture = 0.5 * SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV + float2(0.021, 0.013) * t).r
                                  + 0.5 * SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV * 1.73 + float2(-0.017, 0.024) * t).r;

                float contactFoam = 1.0 - smoothstep(0.0, _ContactFoamDepth, verticalDepth);
                float breakerFoam = OceanShoreBreakerFoam(shore, xz) * _BreakerFoam;
                float whitecapFoam = smoothstep(0.55, 1.0, input.whitecap) * _WhitecapFoam;
                float foamAmount = saturate(contactFoam * 0.85 + breakerFoam + whitecapFoam);
                float foam = smoothstep(1.0 - foamAmount, 1.0 - foamAmount + _FoamSoftness, foamTexture)
                           * saturate(foamAmount * 4.0);

                float3 ambient = SampleSH(normalWS);
                float3 foamLit = _FoamColor.rgb
                    * (mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * 0.6 + 0.4) * shadow + ambient);
                color = lerp(color, foamLit, foam);

                // --- soft seam: transparent exactly where the water meets the ground ---
                float alpha = smoothstep(0.0, _EdgeFade, verticalDepth);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
