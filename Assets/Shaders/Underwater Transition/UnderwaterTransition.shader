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
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _TransitionAmount;
            float _TransitionDirection;
            float _UnderwaterAmount;
            float4 _UnderwaterColor;
            float _DistortionStrength;
            float _EdgeWidth;

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
                half3 underwater = scene.rgb * _UnderwaterColor.rgb;
                underwater = lerp(underwater, _UnderwaterColor.rgb, _UnderwaterColor.a);

                float transitionSide = smoothstep(
                    surfaceLine + wave - _EdgeWidth, surfaceLine + wave + _EdgeWidth, uv.y);
                float tintAmount = saturate(max(_UnderwaterAmount, transitionSide));
                half3 color = lerp(scene.rgb, underwater, tintAmount);
                float diveOnly = step(0.0, _TransitionDirection);
                float bubbleFade = diveOnly * sin(saturate(_TransitionAmount) * 3.14159265);
                float bubbles = BubbleField(uv, saturate(_TransitionAmount)) * bubbleFade;
                float foamNoise = 0.65 + ValueNoise(float2(uv.x * 38.0, time * 3.0)) * 0.65;
                color += edge * foamNoise * half3(0.28, 0.72, 0.78);
                color = lerp(color, half3(0.82, 0.96, 1.0), bubbles * 0.92);
                return half4(color, scene.a);
            }
            ENDHLSL
        }
    }
}
