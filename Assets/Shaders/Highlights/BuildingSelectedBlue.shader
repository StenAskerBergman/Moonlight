Shader "Moonlight/Highlights/BuildingSelectedBlue"
{
    Properties
    {
        _HighlightColor ("Highlight Color", Color) = (0.1, 0.55, 1.0, 1.0)
        _OutlineWidth ("Outline Width", Float) = 0.08
        _RimPower ("Rim Power", Float) = 2.5
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Silhouette outline
        Pass
        {
            Name "Outline"

            Cull Front
            ZWrite Off
            ZTest LEqual

            Blend SrcAlpha One

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _HighlightColor;
                float _OutlineWidth;
                float _RimPower;
                float _RimAlpha;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                positionWS += normalWS * _OutlineWidth;

                output.positionHCS = TransformWorldToHClip(positionWS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _HighlightColor;
            }

            ENDHLSL
        }

        // Blue edge/rim across visible building surface
        Pass
        {
            Name "Rim"

            Cull Back
            ZWrite Off
            ZTest LEqual

            Blend SrcAlpha One

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _HighlightColor;
                float _OutlineWidth;
                float _RimPower;
                float _RimAlpha;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);

                output.normalWS =
                    TransformObjectToWorldNormal(input.normalOS);

                output.positionHCS =
                    TransformWorldToHClip(output.positionWS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                float3 viewDirection =
                    normalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                float rim =
                    1.0 - saturate(dot(normalWS, viewDirection));

                rim = pow(rim, _RimPower);

                return half4(
                    _HighlightColor.rgb,
                    rim * _RimAlpha
                );
            }

            ENDHLSL
        }
    }
}
