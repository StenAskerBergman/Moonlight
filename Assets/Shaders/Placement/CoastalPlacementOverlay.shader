Shader "Moonlight/Placement/CoastalOverlay"
{
    Properties
    {
        _PlacementMask ("Placement Mask", 2D) = "black" {}
        _PlacementOverlayState ("Placement Overlay State", Vector) = (0, 0, 0, 1)
        _PlacementGridParams ("Placement Grid Params", Vector) = (100, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CoastalPlacementOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PlacementOverlayCommon.hlsl"

            TEXTURE2D(_PlacementMask);
            SAMPLER(sampler_PlacementMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _PlacementMask_ST;
                float4 _PlacementOverlayState;
                float4 _PlacementGridParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 cellCoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.cellCoord = input.positionOS.xz / max(_PlacementGridParams.y, 1e-4);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 tintColor;
                half tintWeight;
                half3 emissionColor;
                half waterAlpha;

                EvaluatePlacementOverlayCore(
                    TEXTURE2D_ARGS(_PlacementMask, sampler_PlacementMask),
                    input.cellCoord,
                    _PlacementGridParams.x,
                    _PlacementOverlayState,
                    tintColor,
                    tintWeight,
                    emissionColor,
                    waterAlpha);

                if (waterAlpha <= 0.005h)
                    discard;

                half3 outRgb = lerp(tintColor, tintColor + emissionColor, 0.75h);
                return half4(outRgb, waterAlpha);
            }
            ENDHLSL
        }
    }
}
