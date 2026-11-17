// Alpha-blended particle shader for hydrothermal vent plumes and bubbles.
//
// The ocean ("Ocean Shader Graph") is a transparent surface whose alpha saturates to 1 within
// ~0.1 units of water depth: it shows the seabed by re-drawing the opaque texture (SceneColor)
// tinted by its Albedo, not by being see-through. Transparent particles never reach that
// texture, so the ocean can neither tint them nor hide them correctly - drawn before it they
// vanish entirely, drawn after it they sit on top looking like they float above the sea.
//
// This shader therefore draws BEFORE the ocean (Transparent-10): seen from above, the ocean's
// near-opaque surface is blended over the plume and hides it like any other underwater detail.
// It also discards everything above _WaterLevel (world Y), so no part of a particle quad can
// poke through the surface, and feathers/tints the plume so it reads as underwater when the
// camera is below the surface (the ocean only renders its front face, so it does not cover the
// plume from underneath).
Shader "Moonlight/Underwater Vent Particle"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Water Masking)]
        _WaterLevel ("Water Surface Height (World Y)", Float) = 0
        _SurfaceFadeDistance ("Surface Fade Distance", Float) = 0.6
        _WaterTint ("Water Tint", Color) = (0.46, 0.70, 0.87, 1)
        _WaterTintStrength ("Water Tint Strength", Range(0, 1)) = 0.75
        _DepthFadeDistance ("Depth Fade Distance", Float) = 6
        _DeepAlphaMultiplier ("Deep Alpha Multiplier", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "UnderwaterParticle"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _WaterLevel;
                float _SurfaceFadeDistance;
                half4 _WaterTint;
                half _WaterTintStrength;
                float _DepthFadeDistance;
                half _DeepAlphaMultiplier;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Depth below the surface; anything at or above the water plane is masked.
                float depth = _WaterLevel - input.positionWS.y;
                clip(depth);

                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;

                half surfaceFade = (half)saturate(depth / max(_SurfaceFadeDistance, 1e-3));
                half deep = (half)saturate(depth / max(_DepthFadeDistance, 1e-3));
                col.a *= surfaceFade * lerp((half)1.0, _DeepAlphaMultiplier, deep);

                col.rgb *= lerp(half3(1.0, 1.0, 1.0), _WaterTint.rgb, _WaterTintStrength);
                col.rgb = MixFog(col.rgb, input.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
