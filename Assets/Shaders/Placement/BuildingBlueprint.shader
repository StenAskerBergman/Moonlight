Shader "Moonlight/Placement/BuildingBlueprint"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Hologram Tint", Color) = (0.14, 0.94, 0.38, 0.62)
        [HDR] _RimColor ("Rim Color", Color) = (0.55, 1.0, 0.72, 1.0)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.4
        _RimStrength ("Rim Strength", Range(0.0, 3.0)) = 1.45
        _ScanlineFreq ("Scanline Frequency", Float) = 6.5
        _ScanlineSpeed ("Scanline Speed", Float) = 1.8
        _ScanlineStrength ("Scanline Strength", Range(0.0, 0.6)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+40"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // Depth prepass keeps multi-part / overlapping interior meshes from double-blending.
        Pass
        {
            Name "BlueprintDepthPrepass"
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlueprintHologramForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float _ScanlineFreq;
                float _ScanlineSpeed;
                float _ScanlineStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalWS, mainLight.direction) * 0.65h + 0.35h);
                half shade = lerp(0.55h, 1.15h, ndotl);

                half ndotv = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0h - ndotv, (half)_RimPower);

                float wave = sin(input.positionWS.y * _ScanlineFreq - _Time.y * _ScanlineSpeed) * 0.5 + 0.5;
                half scan = lerp(1.0h - (half)_ScanlineStrength, 1.0h + (half)_ScanlineStrength, (half)wave);

                half3 bodyCol = _BaseColor.rgb * shade * scan;
                half3 finalRgb = bodyCol + _RimColor.rgb * (fresnel * (half)_RimStrength);
                half alpha = saturate(_BaseColor.a + fresnel * 0.25h);

                return half4(finalRgb, alpha);
            }
            ENDHLSL
        }
    }
}
