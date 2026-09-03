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
            float _LowerApronFadeStart;
            float _LowerApronFadeEnd;
            float _LowerApronFadeStrength;

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
            float _DebrisBrightness;
            float _DebrisDriftSpeed;
            float _DropletIntensity;
            float _DropletFallSpeed;

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

            // Thin irregular refracted caustics network using cellular edge distance (F2 - F1)
            // combined with wave coordinate perturbation.
            float VoronoiEdgeDistance(float2 p, float t)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float d1 = 8.0;
                float d2 = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 rnd = float2(Hash21(cell + neighbor), Hash21(cell + neighbor + 19.19));
                        float2 featurePoint = neighbor + 0.5 + 0.35 * sin(t + rnd * 6.2831853);
                        float dist = length(featurePoint - f);
                        if (dist < d1)
                        {
                            d2 = d1;
                            d1 = dist;
                        }
                        else if (dist < d2)
                        {
                            d2 = dist;
                        }
                    }
                }

                return d2 - d1;
            }

            // Dual-layer refracted caustic network: evaluates cellular edge difference (F2 - F1)
            // at wave-perturbed world coordinates. Cell centers are dark; light is focused strictly
            // into thin irregular filaments that intersect and shimmer with temporal wave motion.
            float CalculateCaustics(float2 worldXZ, float t)
            {
                // Wave-induced coordinate refraction
                float2 wave1 = float2(
                    sin(worldXZ.y * 1.8 + t * 2.2) + cos(worldXZ.x * 1.4 - t * 1.7),
                    cos(worldXZ.x * 1.8 + t * 2.5) + sin(worldXZ.y * 1.4 - t * 1.9)
                ) * 0.15;

                float2 wave2 = float2(
                    cos(worldXZ.y * 2.4 - t * 1.8) + sin(worldXZ.x * 2.1 + t * 2.1),
                    sin(worldXZ.x * 2.4 - t * 2.2) + cos(worldXZ.y * 2.1 + t * 1.6)
                ) * 0.12;

                float2 uv1 = worldXZ * _CausticsScale + wave1;
                float2 uv2 = worldXZ * (_CausticsScale * 1.45) + 17.3 + wave2;

                float edge1 = VoronoiEdgeDistance(uv1, t * _CausticsSpeed);
                float edge2 = VoronoiEdgeDistance(uv2, t * (_CausticsSpeed * -1.25));

                // Sharp thin lines along cell boundaries (F2 - F1 close to 0)
                float line1 = pow(saturate(1.0 - edge1 / 0.14), 3.0);
                float line2 = pow(saturate(1.0 - edge2 / 0.14), 3.0);

                // Combining intersecting caustic veins
                float caustic = saturate(line1 * 0.75 + line2 * 0.75 + pow(line1 * line2, 0.5) * 1.5);
                return caustic;
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

            // World-anchored suspended organic sediment / debris in the water volume.
            // Tests candidate 3D cells along the camera view ray within the water column.
            // Each cell's speck has a fixed world coordinate Q with subtle micro-drift.
            // Camera translation and rotation reveal different specks with full 3D parallax,
            // rather than dragging screen-space dots or sticking to geometry surfaces.
            float Hash31(float3 p)
            {
                p = frac(p * float3(123.34, 456.21, 789.13));
                p += dot(p, p.yzx + 45.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 ComputeWorldDebris(float3 camPos, float3 rayDir, float sceneDist, float camDepth, float progress, float time)
            {
                if (_DebrisDensity < 0.001 || progress < 0.001)
                    return float3(0, 0, 0);

                // Determine ray segment inside water volume
                float tEnter = 0.35; // Near clip buffer
                if (camPos.y >= _WaterLevel)
                {
                    if (rayDir.y >= -0.001)
                        return float3(0, 0, 0); // Ray pointing away/parallel above water
                    tEnter = max(0.35, (_WaterLevel - camPos.y) / rayDir.y);
                }

                float tExit = min(sceneDist, 14.0); // Debris visible within near-to-mid water column
                if (rayDir.y > 0.001 && camPos.y < _WaterLevel)
                {
                    float tSurface = (_WaterLevel - camPos.y) / rayDir.y;
                    tExit = min(tExit, tSurface);
                }

                if (tEnter >= tExit)
                    return float3(0, 0, 0);

                const float cellSize = 1.35;
                const int STEPS = 8;
                float stepSize = (tExit - tEnter) / (float)STEPS;
                
                float totalDebris = 0.0;
                float3 prevCell = float3(-999.0, -999.0, -999.0);

                [unroll]
                for (int i = 0; i < STEPS; i++)
                {
                    float tSample = tEnter + (i + 0.5) * stepSize;
                    float3 sampleWs = camPos + rayDir * tSample;
                    float3 cell = floor(sampleWs / cellSize);

                    if (all(cell == prevCell))
                        continue;
                    prevCell = cell;

                    float rnd = Hash31(cell);
                    // Sparse presence: only ~16% of cells contain a sediment speck
                    if (rnd > 0.84)
                    {
                        // Fixed base position in world space within this cell
                        float3 jitter = float3(
                            Hash21(cell.xy + 3.17),
                            Hash21(cell.yz + 7.81),
                            Hash21(cell.zx + 11.43)
                        ) - 0.5;

                        // Extremely subtle local drift to read as gently suspended dirt/sediment
                        float3 drift = float3(
                            sin(time * 0.25 * _DebrisDriftSpeed + cell.y * 0.5 + rnd * 6.28) * 0.035,
                            cos(time * 0.20 * _DebrisDriftSpeed + cell.z * 0.5 + rnd * 4.12) * 0.025,
                            sin(time * 0.22 * _DebrisDriftSpeed + cell.x * 0.5 + rnd * 5.31) * 0.035
                        );

                        float3 speckPosWS = (cell + 0.5 + jitter * 0.75) * cellSize + drift;

                        // Ensure speck is submerged
                        if (speckPosWS.y < _WaterLevel)
                        {
                            // Closest distance from camera ray to the world speck
                            float3 toSpeck = speckPosWS - camPos;
                            float tProj = dot(toSpeck, rayDir);

                            if (tProj >= tEnter && tProj <= tExit)
                            {
                                float distSq = dot(toSpeck, toSpeck) - tProj * tProj;
                                float speckRadius = lerp(0.015, 0.035, frac(rnd * 23.47));
                                float radiusSq = speckRadius * speckRadius;

                                if (distSq < radiusSq)
                                {
                                    // Falloff across speck disc
                                    float profile = smoothstep(radiusSq, radiusSq * 0.15, distSq);
                                    
                                    // Distance fade and water extinction
                                    float distFade = saturate(1.0 - tProj / 13.0);
                                    float waterFade = exp(-_FogDensity * tProj * 3.5);
                                    
                                    // Subtle shimmer
                                    float shimmer = 0.75 + 0.25 * sin(time * 0.8 + rnd * 10.0);
                                    
                                    totalDebris += profile * distFade * waterFade * shimmer * lerp(0.35, 0.85, frac(rnd * 13.7));
                                }
                            }
                        }
                    }
                }

                float transitionFade = saturate(progress * 1.5) * exp(-camDepth * 0.03);
                half3 debrisColor = lerp(_ShallowWaterColor.rgb, half3(0.72, 0.82, 0.85), 0.55);
                return saturate(totalDebris) * _DebrisDensity * _DebrisBrightness * transitionFade * debrisColor;
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
                float sparkle = 1.0 - smoothstep(0.01, 0.055, dist);
                float pulse = 0.35 + 0.65 * saturate(sin(t * 1.7 + rnd * 12.0) * 0.5 + 0.5);
                return sparkle * step(0.86, rnd) * pulse;
            }

            float SurfaceDroplets(float2 uv, float progress)
            {
                if (_TransitionDirection >= 0.0 || _DropletIntensity < 0.001)
                    return 0.0;

                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float droplets = 0.0;

                [unroll]
                for (int dropletIndex = 0; dropletIndex < 18; dropletIndex++)
                {
                    float seed = Hash21(float2(dropletIndex * 2.17, dropletIndex * 9.31));
                    float x = frac(seed * 13.71 + dropletIndex * 0.137);
                    float speed = _DropletFallSpeed * lerp(0.55, 1.25, frac(seed * 23.17));
                    float y = 1.08 - frac(seed * 7.91) * 0.32 - progress * speed;
                    float radius = lerp(0.008, 0.022, frac(seed * 31.47));
                    float2 delta = float2((uv.x - x) * aspect, uv.y - y);

                    float head = 1.0 - smoothstep(radius * 0.45, radius, length(float2(delta.x, delta.y * 0.68)));
                    float trailX = 1.0 - smoothstep(radius * 0.18, radius * 0.65, abs(delta.x));
                    float trailY = smoothstep(-radius * 0.2, radius * 0.4, delta.y)
                        * (1.0 - smoothstep(radius * 1.5, radius * 7.0, delta.y));
                    droplets += head + trailX * trailY * 0.32;
                }

                float appear = smoothstep(0.02, 0.14, progress);
                float drain = 1.0 - smoothstep(0.62, 1.0, progress);
                return saturate(droplets) * appear * drain * _DropletIntensity;
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

                // Diving fills the frame from bottom to top. Surfacing retraces that
                // coverage from top to bottom as TransitionProgress falls back to zero.
                float surfaceLine = lerp(-0.12, 1.12, _TransitionProgress);

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

                // 3. Submerged Surface Caustics (World-anchored with depth fade and slope rejection)
                float3 caustics = float3(0, 0, 0);
                if (_CausticsStrength > 0.001 && !isBackground && positionWS.y < _WaterLevel)
                {
                    // Derive world surface normal from screen derivatives of reconstructed world position
                    float3 ddxPos = ddx(positionWS);
                    float3 ddyPos = ddy(positionWS);
                    float3 worldNormal = normalize(cross(ddxPos, ddyPos));
                    if (dot(worldNormal, rayDir) > 0.0)
                        worldNormal = -worldNormal;

                    // Reject steep/vertical surfaces: caustics only project onto upward-facing surfaces (seabed),
                    // strongly attenuating on slopes and completely vanishing on vertical cliff faces.
                    float slopeFactor = smoothstep(0.45, 0.8, saturate(worldNormal.y));

                    // Strong depth fade: caustics extinguish rapidly as depth below water increases
                    float depthBelowWater = max(0.0, _WaterLevel - positionWS.y);
                    float depthFade = saturate(1.0 - depthBelowWater / max(_CausticsFadeDepth, 0.5));
                    depthFade = depthFade * depthFade * exp(-depthBelowWater * 0.35);
                    depthFade *= exp(-waterDistance * _FogDensity * 1.2);

                    float causticVal = CalculateCaustics(positionWS.xz, time);

                    // Natural sunlight tint: avoid globally washing terrain cyan; caustics read as focused sun rays
                    half3 sunCausticColor = lerp(half3(1.0, 0.98, 0.92), _ShallowWaterColor.rgb, 0.22) * mainLight.color;
                    caustics = sunCausticColor * causticVal * _CausticsStrength * depthFade * sunLightAtten * slopeFactor;
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

                // Let the deepest part of the generated lower apron disappear into
                // abyssal water instead of ending in a visibly flat/static boundary.
                float lowerApronFade = smoothstep(
                    _LowerApronFadeStart,
                    max(_LowerApronFadeEnd, _LowerApronFadeStart + 0.1),
                    pixelDepth) * _LowerApronFadeStrength;
                underwaterView = lerp(underwaterView, _AbyssalColor.rgb, lowerApronFade);

                // 5. Murk / Turbidity: desaturate and pull toward a uniform ambient tone
                // as fog builds up and the transition deepens. Clear near the surface,
                // a flat blue-green soup once fogAmount and progress both ramp up.
                float murkAmount = saturate(fogAmount * (0.6 + 0.4 * tDeep) + _TransitionProgress * 0.15);
                float murkLuma = dot(underwaterView, half3(0.299, 0.587, 0.114));
                underwaterView = lerp(underwaterView, murkLuma.xxx, murkAmount * 0.5);
                underwaterView = lerp(underwaterView, ambientWaterColor, murkAmount * 0.35);

                // Transition blending across waterline
                float transitionSide = 1.0 - smoothstep(
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

                // Lens water appears only while surfacing, then drains downward and
                // clears before the transition finishes.
                float droplets = SurfaceDroplets(uv, saturate(_TransitionAmount));
                half3 dropletColor = lerp(color * 0.72, _ShallowWaterColor.rgb, 0.28);
                color = lerp(color, dropletColor, droplets * 0.55);
                color += droplets * half3(0.12, 0.2, 0.22);

                // 7. God Rays & World Debris, confined to the underwater side of the frame by
                // the same tintAmount used to blend the underwater view in above.
                color += ComputeGodRays(uv, warpedUv, _TransitionProgress, camDepth, time) * tintAmount;
                color += ComputeWorldDebris(camPos, rayDir, rayDist, camDepth, _TransitionProgress, time) * tintAmount;

                return half4(color, scene.a);
            }
            ENDHLSL
        }
    }
}
