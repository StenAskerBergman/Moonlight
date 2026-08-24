Shader "Custom/URPTerrainSplatmap"
{
    Properties
    {
        _BaseMap ("Splat Map (RGBA)", 2D) = "black" {}
        
        [Header(Layer 1)]
        _Color1 ("Color 1 (Grass)", Color) = (0.3, 0.45, 0.15, 1)
        _Tex1 ("Texture 1", 2D) = "white" {}
        
        [Header(Layer 2)]
        _Color2 ("Color 2 (Sand)", Color) = (0.9, 0.85, 0.6, 1)
        _Tex2 ("Texture 2", 2D) = "white" {}
        
        [Header(Layer 3)]
        _Color3 ("Color 3 (Rock)", Color) = (0.4, 0.4, 0.45, 1)
        _Tex3 ("Texture 3", 2D) = "white" {}
        
        [Header(Layer 4)]
        _Color4 ("Color 4 (Snow/Water)", Color) = (0.95, 0.95, 0.98, 1)
        _Tex4 ("Texture 4", 2D) = "white" {}

        _Tiling ("Texture Tiling", Float) = 20.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : NORMAL;
                float2 uv           : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Tex1); SAMPLER(sampler_Tex1);
            TEXTURE2D(_Tex2); SAMPLER(sampler_Tex2);
            TEXTURE2D(_Tex3); SAMPLER(sampler_Tex3);
            TEXTURE2D(_Tex4); SAMPLER(sampler_Tex4);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color1;
                float4 _Color2;
                float4 _Color3;
                float4 _Color4;
                float _Tiling;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample Splatmap
                float4 splat = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                
                // Calculate Tiled UVs (World Space Planar Mapping for seamless tiling)
                float2 tiledUV = input.positionWS.xz * (1.0 / _Tiling);

                // Sample Textures
                half4 col1 = SAMPLE_TEXTURE2D(_Tex1, sampler_Tex1, tiledUV) * _Color1;
                half4 col2 = SAMPLE_TEXTURE2D(_Tex2, sampler_Tex2, tiledUV) * _Color2;
                half4 col3 = SAMPLE_TEXTURE2D(_Tex3, sampler_Tex3, tiledUV) * _Color3;
                half4 col4 = SAMPLE_TEXTURE2D(_Tex4, sampler_Tex4, tiledUV) * _Color4;

                // Blend based on splat weights
                half4 albedo = col1 * splat.r + col2 * splat.g + col3 * splat.b + col4 * splat.a;

                // Basic Lighting
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half3 diffuse = albedo.rgb * mainLight.color * NdotL;
                
                // Ambient
                diffuse += albedo.rgb * half3(0.2, 0.2, 0.2);

                return half4(diffuse, albedo.a);
            }
            ENDHLSL
        }
    }
}
