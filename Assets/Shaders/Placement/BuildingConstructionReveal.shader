Shader "Moonlight/Placement/BuildingConstructionReveal"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.85, 0.85, 0.88, 1.0)
        [HDR] _ScanColor ("Construction Laser Color", Color) = (0.25, 1.65, 1.45, 1.0)
        _GhostColor ("Unbuilt Silhouette Color", Color) = (0.16, 0.72, 0.92, 0.22)
        _BuildProgress ("Build Progress", Range(0.0, 1.0)) = 0.0
        _BoundsMinY ("Bounds Min Y", Float) = 0.0
        _BoundsMaxY ("Bounds Max Y", Float) = 3.0
        _ScanBandWidth ("Laser Band Width", Range(0.05, 1.0)) = 0.28
        _GridScale ("Blueprint Grid Scale", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // Pass 0: Constructed solid portion below the rising build plane.
        Pass
        {
            Name "RevealSolidForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ScanColor;
                half4 _GhostColor;
                float _BuildProgress;
                float _BoundsMinY;
                float _BoundsMaxY;
                float _ScanBandWidth;
                float _GridScale;
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
                float fogCoord : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs norm = GetVertexNormalInputs(input.normalOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = norm.normalWS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float revealY = lerp(_BoundsMinY - 0.04, _BoundsMaxY + 0.04, saturate(_BuildProgress));
                clip(revealY - input.positionWS.y);

                half4 texSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = texSample.rgb * _BaseColor.rgb;

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 litColor = albedo * (ambient + mainLight.color * (ndotl * mainLight.shadowAttenuation));

                // Rising construction laser band just underneath revealY.
                float distToPlane = saturate((revealY - input.positionWS.y) / max(0.02, _ScanBandWidth));
                float bandMask = pow(1.0 - distToPlane, 2.5);
                half3 finalRgb = litColor + _ScanColor.rgb * (half)(bandMask * 1.15);
                finalRgb = MixFog(finalRgb, input.fogCoord);

                return half4(finalRgb, 1.0h);
            }
            ENDHLSL
        }

        // Pass 1: Faint holographic wireframe / blueprint silhouette above the rising build plane.
        Pass
        {
            Name "RevealGhostSilhouette"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex VertGhost
            #pragma fragment FragGhost
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ScanColor;
                half4 _GhostColor;
                float _BuildProgress;
                float _BoundsMinY;
                float _BoundsMaxY;
                float _ScanBandWidth;
                float _GridScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings VertGhost(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs norm = GetVertexNormalInputs(input.normalOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = norm.normalWS;
                return o;
            }

            half4 FragGhost(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float revealY = lerp(_BoundsMinY - 0.04, _BoundsMaxY + 0.04, saturate(_BuildProgress));
                clip(input.positionWS.y - revealY);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 2.2h);

                float3 scaled = input.positionWS * max(0.5, _GridScale);
                float3 fracXYZ = abs(frac(scaled) - 0.5);
                float wire = step(0.44, max(fracXYZ.x, max(fracXYZ.y, fracXYZ.z)));

                float nearLaser = 1.0 - saturate((input.positionWS.y - revealY) / max(0.05, _ScanBandWidth * 0.75));
                half3 rgb = _GhostColor.rgb + _ScanColor.rgb * (half)(fresnel * 0.55 + wire * 0.35 + nearLaser * 0.7);
                half alpha = saturate(_GhostColor.a * 0.45h + fresnel * 0.32h + (half)(wire * 0.16 + nearLaser * 0.28));

                return half4(rgb, alpha);
            }
            ENDHLSL
        }

        // Pass 2: Growing shadow caster matching constructed height.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ScanColor;
                half4 _GhostColor;
                float _BuildProgress;
                float _BoundsMinY;
                float _BoundsMaxY;
                float _ScanBandWidth;
                float _GridScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                float revealY = lerp(_BoundsMinY - 0.04, _BoundsMaxY + 0.04, saturate(_BuildProgress));
                clip(revealY - input.positionWS.y);
                return 0;
            }
            ENDHLSL
        }
    }
}
