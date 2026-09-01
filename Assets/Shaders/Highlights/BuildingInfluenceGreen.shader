Shader "Moonlight/Highlights/BuildingInfluenceGreen"
{
    Properties
    {
        _HighlightColor ("Highlight Color", Color) = (0.0, 1.0, 0.08, 1.0)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.55
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "InfluenceOverlay"

            Cull Back
            ZWrite Off
            ZTest LEqual

            Blend SrcAlpha OneMinusSrcAlpha

            Offset -1, -1

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _HighlightColor;
                float _FillAlpha;
                float _EmissionStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 color =
                    _HighlightColor.rgb * _EmissionStrength;

                return half4(color, _FillAlpha);
            }

            ENDHLSL
        }
    }
}
