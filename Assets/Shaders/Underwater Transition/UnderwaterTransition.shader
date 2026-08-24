Shader "Hidden/Moonlight/UnderwaterTransition"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "Underwater Transition"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _TransitionAmount;
            float _TransitionDirection;
            float _UnderwaterAmount;
            float4 _UnderwaterColor;
            float _DistortionStrength;
            float _EdgeWidth;
            float _WaterLevel;

            float4 _ShallowWaterColor;
            float4 _DeepWaterColor;
            float4 _AbyssalColor;
            float4 _AbsorptionCoefficients;
            float _FogDensity;
            float _DeepDepthThreshold;
            float _AbyssDepthThreshold;
            float _SunScatteringIntensity;
            float _SunDepthExtinction;

            float _CausticsStrength;
            float _CausticsScale;
            float _CausticsSpeed;
            float _CausticsFadeDepth;

            float _MarineSnowIntensity;
            float _MarineSnowScale;
            float _MarineSnowSpeed;

            float4x4 _InverseViewProjection;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + 1), f.x), f.y);
            }

            float CalculateCaustics(float2 p, float t)
            {
                float2 p1 = p + float2(t * 0.7, t * 0.5);
                float2 p2 = p * 1.3 - float2(t * 0.6, -t * 0.8);
                float c1 = sin(p1.x * 3.14 + sin(p1.y * 2.71 + t)) + cos(p1.y * 3.14 + sin(p1.x * 2.71 - t));
                float c2 = sin(p2.x * 4.14 + cos(p2.y * 3.71 - t)) + cos(p2.y * 4.14 + cos(p2.x * 3.71 + t));
                float caustic = pow(saturate(0.5 + 0.25 * (c1 + c2)), 3.5) * 2.2;
                return caustic;
            }

            float CalculateMarineSnow(float3 wsPos, float t)
            {
                float3 drift = float3(sin(t * 0.6 + wsPos.y * 0.5) * 0.25, -t * 0.5, cos(t * 0.5 + wsPos.x * 0.5) * 0.25);
                float3 p = (wsPos + drift) * _MarineSnowScale;
                float3 cell = floor(p);
                float3 fracP = frac(p) - 0.5;

                float rnd = Hash21(cell.xy + float2(cell.z * 17.13, cell.z * 31.79));
                float3 particlePos = (float3(Hash21(cell.xy), Hash21(cell.yz), Hash21(cell.zx)) - 0.5) * 0.75;
                float dist = length(fracP - particlePos);
                float sparkle = 1.0 - smoothstep(0.015, 0.075, dist);
                return sparkle * step(0.68, rnd);
            }

            float BubbleField(float2 uv, float progress)
            {
                float bubbles = 0.0;
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);

                [unroll]
                for (int bubbleIndex = 0; bubbleIndex < 20; bubbleIndex++)
                {
                    float seed = Hash21(float2(bubbleIndex * 1.37, bubbleIndex * 7.91));
                    float x = frac(seed * 11.73 + bubbleIndex * 0.173);
                    float rise = progress * (1.35 + frac(seed * 5.17) * 0.55);
                    float y = -0.16 + frac(seed * 17.31) * 0.42 + rise;
                    x += sin(progress * 8.0 + seed * 19.0) * 0.025;

                    float radius = lerp(0.012, 0.045, frac(seed * 29.41));
                    float2 delta = float2((uv.x - x) * aspect, uv.y - y);
                    float distanceToBubble = length(delta);
                    float outer = 1.0 - smoothstep(radius, radius + 0.008, distanceToBubble);
                    float inner = 1.0 - smoothstep(radius * 0.58, radius * 0.76, distanceToBubble);
                    float ring = saturate(outer - inner);
                    float highlight = 1.0 - smoothstep(radius * 0.16, radius * 0.32,
                        length(delta - float2(-radius * 0.28, radius * 0.3)));
                    bubbles += ring + highlight * 0.75;
                }

                return saturate(bubbles);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float time = _Time.y;
                float wave = sin(uv.x * 17.0 + time * 4.3) * 0.012
                           + sin(uv.x * 31.0 - time * 2.7) * 0.006;
                wave += (ValueNoise(float2(uv.x * 9.0, time * 0.8)) - 0.5) * 0.018;

                // Entry travels down the screen; exit travels upward.
                float surfaceLine = lerp(1.12, -0.12, _TransitionAmount);
                if (_TransitionDirection < 0.0)
                    surfaceLine = 1.0 - surfaceLine;

                float distanceToSurface = abs(uv.y - (surfaceLine + wave));
                float edge = 1.0 - smoothstep(0.0, max(_EdgeWidth, 0.001), distanceToSurface);
                float distortion = (_UnderwaterAmount * 0.35 + edge) * _DistortionStrength;
                float2 warpedUv = uv;
                warpedUv.x += sin(uv.y * 42.0 + time * 3.2) * distortion;
                warpedUv.y += cos(uv.x * 35.0 - time * 2.4) * distortion * 0.45;

                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUv);

                // --- Depth-Aware Bathymetry & Underwater Volumetric Fog ---
                float rawDepth = SampleSceneDepth(warpedUv);
                #if UNITY_REVERSED_Z
                bool isBackground = rawDepth <= 0.00005;
                #else
                bool isBackground = rawDepth >= 0.99995;
                #endif

                float3 positionWS = ComputeWorldSpacePosition(warpedUv, rawDepth, _InverseViewProjection);
                float3 camPos = _WorldSpaceCameraPos;
                float3 rayDir = normalize(positionWS - camPos);
                float rayDist = length(positionWS - camPos);

                if (isBackground)
                {
                    rayDist = 200.0;
                    positionWS = camPos + rayDir * rayDist;
                }

                float camDepth = max(0.0, _WaterLevel - camPos.y);
                float pixelDepth = max(0.0, _WaterLevel - positionWS.y);

                // Calculate distance traveled underwater
                float waterDistance = rayDist;
                if (camPos.y >= _WaterLevel)
                {
                    if (positionWS.y < _WaterLevel && rayDir.y < -0.001)
                    {
                        float tWater = (_WaterLevel - camPos.y) / rayDir.y;
                        waterDistance = max(0.0, rayDist - tWater);
                    }
                    else
                    {
                        waterDistance = 0.0;
                    }
                }
                else
                {
                    if (positionWS.y > _WaterLevel && rayDir.y > 0.001)
                    {
                        float tExit = (_WaterLevel - camPos.y) / rayDir.y;
                        waterDistance = min(rayDist, tExit);
                    }
                }

                // 1. Exponential Light Absorption (Beer-Lambert Law)
                float3 betaExt = _AbsorptionCoefficients.xyz;
                float3 transmittance = exp(-betaExt * waterDistance * _FogDensity * 14.0);

                // 2. Depth-Sensitive In-Scattering (Volumetric Water Column)
                float meanDepth = 0.5 * (camDepth + pixelDepth);
                float tDeep = saturate(meanDepth / max(_DeepDepthThreshold, 0.1));
                float3 ambientWaterColor = lerp(_ShallowWaterColor.rgb, _DeepWaterColor.rgb, tDeep);
                float tAbyss = saturate((meanDepth - _DeepDepthThreshold) / max(_AbyssDepthThreshold - _DeepDepthThreshold, 0.1));
                ambientWaterColor = lerp(ambientWaterColor, _AbyssalColor.rgb, tAbyss);

                float sunLightAtten = exp(-_SunDepthExtinction * min(camDepth, pixelDepth));
                Light mainLight = GetMainLight();
                float sunDot = saturate(dot(rayDir, mainLight.direction));
                float sunScatter = 1.0 + _SunScatteringIntensity * pow(sunDot, 4.0);

                float fogAmount = 1.0 - exp(-_FogDensity * waterDistance);
                float3 inscattering = ambientWaterColor * sunScatter * sunLightAtten * fogAmount;

                // 3. Submerged Surface Caustics
                float3 caustics = float3(0, 0, 0);
                if (_CausticsStrength > 0.001 && !isBackground && positionWS.y < _WaterLevel)
                {
                    float causticVal = CalculateCaustics(positionWS.xz * _CausticsScale, time * _CausticsSpeed);
                    float depthFade = exp(-pixelDepth / max(_CausticsFadeDepth, 0.5));
                    caustics = _ShallowWaterColor.rgb * causticVal * _CausticsStrength * depthFade * sunLightAtten;
                }

                // 4. Marine Snow / Suspended Micro-Particles
                float3 marineSnow = float3(0, 0, 0);
                if (_MarineSnowIntensity > 0.001 && waterDistance > 0.3)
                {
                    float snowVal = CalculateMarineSnow(positionWS, time * _MarineSnowSpeed);
                    float3 snowColor = lerp(_ShallowWaterColor.rgb, half3(0.9, 0.98, 1.0), 0.6);
                    marineSnow = snowColor * snowVal * _MarineSnowIntensity * sunLightAtten * saturate(waterDistance * 0.15);
                }

                // Assemble underwater image
                half3 litScene = scene.rgb + caustics;
                half3 underwaterView = litScene * transmittance + inscattering + marineSnow;

                if (isBackground)
                {
                    float verticalGaze = rayDir.y;
                    float3 backgroundAtmosphere = lerp(_AbyssalColor.rgb, _DeepWaterColor.rgb, saturate(verticalGaze * 0.5 + 0.5));
                    if (verticalGaze > 0.0)
                    {
                        backgroundAtmosphere = lerp(backgroundAtmosphere, _ShallowWaterColor.rgb, pow(verticalGaze, 1.5) * sunLightAtten);
                    }
                    underwaterView = backgroundAtmosphere + marineSnow;
                }

                // Transition blending across waterline
                float transitionSide = smoothstep(
                    surfaceLine + wave - _EdgeWidth, surfaceLine + wave + _EdgeWidth, uv.y);
                float tintAmount = saturate(max(_UnderwaterAmount, transitionSide));
                half3 color = lerp(scene.rgb, underwaterView, tintAmount);

                float diveOnly = step(0.0, _TransitionDirection);
                float bubbleFade = diveOnly * sin(saturate(_TransitionAmount) * 3.14159265);
                float bubbles = BubbleField(uv, saturate(_TransitionAmount)) * bubbleFade;
                float foamNoise = 0.65 + ValueNoise(float2(uv.x * 38.0, time * 3.0)) * 0.65;

                color += edge * foamNoise * _ShallowWaterColor.rgb * 0.8;
                color = lerp(color, half3(0.82, 0.96, 1.0), bubbles * 0.92);
                return half4(color, scene.a);
            }
            ENDHLSL
        }
    }
}

