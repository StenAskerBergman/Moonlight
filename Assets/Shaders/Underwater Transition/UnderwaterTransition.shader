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

            // 0 = surfaced, 1 = fully submerged. Continuous through the crossing,
            // unlike _TransitionAmount which only animates during isTransitioning.
            float _TransitionProgress;
            float _GodRayIntensity;
            float _DebrisDensity;

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

            // Cheap animated voronoi: distance from p to the nearest of 9 jittered
            // feature points (one per neighboring cell), each orbiting its cell center
            // over time so the pattern shimmers instead of just scrolling.
            float VoronoiCaustic(float2 p, float t)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float minDist = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 rnd = float2(Hash21(cell + neighbor), Hash21(cell + neighbor + 19.19));
                        float2 featurePoint = neighbor + 0.5 + 0.5 * sin(t + rnd * 6.2831853);
                        minDist = min(minDist, length(featurePoint - f));
                    }
                }

                return minDist;
            }

            // Two voronoi layers at different scale/speed, summed and brightened, so the
            // thin bright cell-boundary veins from each layer cross and reinforce each
            // other -- the broken, refracting look of real sunlight through a moving
            // surface rather than one clean repeating pattern.
            float CalculateCaustics(float2 worldXZ, float t)
            {
                float layer1 = VoronoiCaustic(worldXZ * _CausticsScale, t * _CausticsSpeed);
                float layer2 = VoronoiCaustic(worldXZ * _CausticsScale * 1.7 + 13.7, t * _CausticsSpeed * -1.35);

                float bright1 = pow(saturate(1.0 - layer1 * 2.2), 3.0);
                float bright2 = pow(saturate(1.0 - layer2 * 2.2), 3.0);

                return saturate((bright1 + bright2) * 1.35);
            }

            // Classic screen-space radial light shafts: march a handful of samples from
            // each pixel toward a screen-space "sun" point near the top of the frame,
            // accumulating scene brightness along the way. Bright surface glints smear
            // into shafts; the whole thing fades with transition progress and camera depth
            // so it reads strongest just under the surface and dies out toward the abyss.
            float3 ComputeGodRays(float2 uv, float2 warpedUv, float progress, float camDepth, float time)
            {
                if (progress < 0.001 || _GodRayIntensity < 0.001)
                    return float3(0, 0, 0);

                float2 lightPos = float2(0.5 + sin(time * 0.05) * 0.03, 0.97);
                float2 toLight = lightPos - uv;

                const int SAMPLES = 10;
                float2 rayStep = toLight / SAMPLES * 0.65;

                float2 samplePos = warpedUv;
                float accum = 0.0;
                float weight = 1.0;

                [unroll]
                for (int i = 0; i < SAMPLES; i++)
                {
                    samplePos += rayStep;
                    float3 s = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(samplePos)).rgb;
                    float luma = dot(s, half3(0.299, 0.587, 0.114));
                    accum += smoothstep(0.55, 1.0, luma) * weight;
                    weight *= 0.9;
                }
                accum /= SAMPLES;

                float radialFalloff = saturate(1.0 - length(toLight) * 0.55);
                float depthFade = exp(-progress * 2.0) * exp(-camDepth * 0.04);
                float shimmer = 0.85 + 0.15 * sin(time * 3.0 + uv.x * 20.0);

                return accum * radialFalloff * depthFade * shimmer * _GodRayIntensity * half3(0.55, 0.85, 0.95);
            }

            // One drifting speck layer: a sparse grid of jittered dots that scroll slowly
            // in screen space. Three layers at different scale/speed (called below) fake
            // parallax -- the "nearer" layer is bigger and drifts faster.
            float DebrisLayer(float2 uv, float aspect, float scale, float speed, float seed, float time)
            {
                float2 p = uv * float2(aspect, 1.0) * scale;
                p.y -= time * speed;
                float2 cell = floor(p);
                float2 f = frac(p) - 0.5;

                float rnd = Hash21(cell + seed);
                float2 jitter = (float2(Hash21(cell + seed + 1.7), Hash21(cell + seed + 3.1)) - 0.5) * 0.6;
                float dist = length(f - jitter);
                float speck = 1.0 - smoothstep(0.02, 0.09, dist);
                return speck * step(0.82, rnd);
            }

            // Procedural plankton/sediment: no particle system, just animated noise dots
            // in screen space, layered for a sense of depth as they drift upward.
            float3 ComputeDebris(float2 uv, float progress, float camDepth, float time)
            {
                if (_DebrisDensity < 0.001)
                    return float3(0, 0, 0);

                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float debris = 0.0;
                debris += DebrisLayer(uv, aspect, 14.0, 0.015, 11.0, time) * 1.0;
                debris += DebrisLayer(uv, aspect, 22.0, 0.028, 47.0, time) * 0.8;
                debris += DebrisLayer(uv, aspect, 34.0, 0.045, 91.0, time) * 0.6;

                float fade = saturate(progress * 1.4) * exp(-camDepth * 0.03);
                return saturate(debris) * _DebrisDensity * fade * half3(0.85, 0.95, 0.9);
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

                // Chromatic aberration: only during the crossing window (progress 0.3-0.7),
                // settling back to a clean single sample once the crossing is over.
                float caWindow = smoothstep(0.3, 0.4, _TransitionProgress) * (1.0 - smoothstep(0.6, 0.7, _TransitionProgress));
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUv);
                if (caWindow > 0.001)
                {
                    float2 caOffset = (1.0 / _ScreenParams.xy) * lerp(1.0, 2.0, caWindow) * caWindow;
                    scene.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUv + caOffset).r;
                    scene.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUv - caOffset).b;
                }

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

                // 1. Exponential Light Absorption (Beer-Lambert Law), per channel.
                // Real seawater strips red first, then green, leaving blue-green in the
                // abyss -- _AbsorptionCoefficients.r is tuned well above .b for this.
                // _TransitionProgress adds a simulated extra column of water on top of
                // the real per-pixel waterDistance, so the crossing itself reads as
                // "going deeper" even before geometry provides much actual depth.
                float3 betaExt = _AbsorptionCoefficients.xyz;
                float progressDepth = _TransitionProgress * 8.0;
                float3 transmittance = exp(-betaExt * (waterDistance + progressDepth) * _FogDensity * 14.0);

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
                    float causticVal = CalculateCaustics(positionWS.xz, time);
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

                // 5. Murk / Turbidity: desaturate and pull toward a uniform ambient tone
                // as fog builds up and the transition deepens. Clear near the surface,
                // a flat blue-green soup once fogAmount and progress both ramp up.
                float murkAmount = saturate(fogAmount * (0.6 + 0.4 * tDeep) + _TransitionProgress * 0.15);
                float murkLuma = dot(underwaterView, half3(0.299, 0.587, 0.114));
                underwaterView = lerp(underwaterView, murkLuma.xxx, murkAmount * 0.5);
                underwaterView = lerp(underwaterView, ambientWaterColor, murkAmount * 0.35);

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

                // 6. Surface Plane: a bright shimmering band right at the waterline during
                // the 0.3-0.7 crossing window -- "breaking the surface". The edge/foam
                // term above already marks the line; this just makes it flare brighter
                // right when the camera is actually passing through it.
                float bandWindow = smoothstep(0.3, 0.4, _TransitionProgress) * (1.0 - smoothstep(0.6, 0.7, _TransitionProgress));
                float shimmer = sin(uv.x * 60.0 + time * 6.0) * 0.5 + 0.5;
                color += edge * shimmer * (_ShallowWaterColor.rgb * 1.6 + half3(0.15, 0.2, 0.2)) * bandWindow * 1.5;

                color = lerp(color, half3(0.82, 0.96, 1.0), bubbles * 0.92);

                // 7. God Rays & Debris, confined to the underwater side of the frame by
                // the same tintAmount used to blend the underwater view in above.
                color += ComputeGodRays(uv, warpedUv, _TransitionProgress, camDepth, time) * tintAmount;
                color += ComputeDebris(uv, _TransitionProgress, camDepth, time) * tintAmount;

                return half4(color, scene.a);
            }
            ENDHLSL
        }
    }
}

