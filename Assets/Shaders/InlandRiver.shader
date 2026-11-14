Shader "Moonlight/Inland River"
{
    Properties
    {
        _WaterColor ("Water", Color) = (0.07, 0.20, 0.23, 0.88)
        _ShallowColor ("Shallow water", Color) = (0.16, 0.34, 0.32, 0.78)
        _FoamColor ("Foam", Color) = (0.83, 0.89, 0.86, 1)
        _FlowSpeed ("Flow speed", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _WaterColor, _ShallowColor, _FoamColor;
            float _FlowSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; float4 color : COLOR; float fog : TEXCOORD3; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.uv = v.uv; o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float along = i.uv.y - _Time.y * _FlowSpeed;
                float across = i.uv.x * 2.0 - 1.0;
                float broad = sin(along * 3.1 + sin(along * 0.71 + across * 3.7) * 1.35) * 0.5 + 0.5;
                float detail = sin(along * 8.3 - across * 5.1 + sin(along * 1.9) * 0.8) * 0.5 + 0.5;
                float turbulence = saturate(broad * 0.64 + detail * 0.36);
                float edges = smoothstep(0.72, 1.0, abs(across));
                float curtain = i.color.b;
                float impactFoam = i.color.r * lerp(0.22, 0.88, smoothstep(0.28, 0.82, turbulence));
                float fallingStreaks = curtain * smoothstep(0.42, 0.78,
                    sin(across * 13.0 + along * 1.7) * 0.5 + 0.5) * 0.42;
                float foam = saturate(impactFoam + fallingStreaks + edges * turbulence * 0.2);
                half3 water = lerp(_WaterColor.rgb, _ShallowColor.rgb, saturate(i.color.g * 0.45 + turbulence * 0.12));
                water *= 0.88 + turbulence * 0.13;
                half3 color = lerp(water, _FoamColor.rgb, foam);
                Light light = GetMainLight();
                float3 normalWS = normalize(i.normalWS);
                float3 viewWS = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                float diffuse = 0.52 + 0.48 * abs(dot(normalWS, light.direction));
                float fresnel = pow(1.0 - saturate(abs(dot(normalWS, viewWS))), 4.0);
                float3 halfVector = normalize(light.direction + viewWS);
                float specular = pow(saturate(abs(dot(normalWS, halfVector))), 56.0) * (0.24 + fresnel * 0.76);
                color = color * diffuse * max(light.color, half3(0.6, 0.6, 0.6));
                color += light.color * specular;
                color = lerp(color, _ShallowColor.rgb, fresnel * 0.18);
                color = MixFog(color, i.fog);
                return half4(color, lerp(_WaterColor.a, 1.0, foam) * i.color.a);
            }
            ENDHLSL
        }
    }
}
