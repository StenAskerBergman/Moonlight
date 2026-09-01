// SeaweedSway.shader - URP lit, two-sided underwater foliage with GPU current motion.
Shader "Custom/SeaweedSway"
{
    Properties
    {
        _MainTex ("Main (Optional)", 2D) = "white" {}
        _BaseTint ("Base Tint", Color) = (0.12,0.32,0.16,1)
        _TipTint ("Tip Tint", Color) = (0.32,0.48,0.20,1)
        _VeinColor ("Vein Color", Color) = (0.055,0.14,0.075,1)
        _TransmissionColor ("Transmission Color", Color) = (0.28,0.48,0.16,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.28
        _Taper ("Tip Taper", Range(0,1)) = 0.48
        _ProfileExp ("Width Profile", Range(0.5,4)) = 2.2
        _EdgeNoiseAmp ("Torn Edge Amount", Range(0,0.5)) = 0.10
        _EdgeNoiseScale ("Torn Edge Scale", Range(1,20)) = 8
        _Smoothness ("Wet Smoothness", Range(0,1)) = 0.48
        _SpecularStrength ("Specular Strength", Range(0,1)) = 0.24
        _Transmission ("Backlight Transmission", Range(0,1)) = 0.38
        _Stiffness ("Base Stiffness", Range(0,1)) = 0.42
        _SwayAmp ("Current Bend", Range(0,2)) = 0.55
        _SwayFreq ("Sway Frequency", Range(0,5)) = 0.62
        _NoiseScale ("Layered Motion", Range(0,2)) = 0.72
        _VeinSeed ("Vein Seed", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "RenderPipeline"="UniversalRenderPipeline" "IgnoreProjector"="True" }
        LOD 200
        Cull Off
        ZWrite On
        AlphaToMask On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half height01 : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseTint)
                UNITY_DEFINE_INSTANCED_PROP(float, _VeinSeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClumpSeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClumpPhase)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ClumpDirWS)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClumpStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClumpFlutter)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClumpTwist)
            UNITY_INSTANCING_BUFFER_END(Props)

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TipTint, _VeinColor, _TransmissionColor;
                float _Cutoff, _Taper, _ProfileExp, _EdgeNoiseAmp, _EdgeNoiseScale;
                float _Smoothness, _SpecularStrength, _Transmission;
                float _Stiffness, _SwayAmp, _SwayFreq, _NoiseScale;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float3 _CurrentDirWS;
            float _CurrentSpeed;
            float _CurrentGustStrength;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float height01 = saturate(IN.uv.y);
                // Cubic weighting leaves the holdfast fixed and concentrates bend at the tip.
                float bendWeight = height01 * height01 * (3.0 - 2.0 * height01);
                bendWeight *= lerp(1.0, 0.32, saturate(_Stiffness) * (1.0 - height01));

                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float clumpSeed = UNITY_ACCESS_INSTANCED_PROP(Props, _ClumpSeed);
                if (clumpSeed <= 0.0) clumpSeed = UNITY_ACCESS_INSTANCED_PROP(Props, _VeinSeed);
                float bladeSeed = frac(IN.color.r + Hash21(originWS.xz * 0.071 + clumpSeed * 13.7));

                float3 packedDirection = UNITY_ACCESS_INSTANCED_PROP(Props, _ClumpDirWS).xyz;
                float3 flowDir = dot(packedDirection.xz, packedDirection.xz) > 0.0001 ? packedDirection : _CurrentDirWS;
                flowDir.y = 0;
                if (dot(flowDir, flowDir) < 0.0001) flowDir = float3(1, 0, 0);
                flowDir = normalize(flowDir);
                float3 sideDir = normalize(cross(float3(0, 1, 0), flowDir));

                float strength = UNITY_ACCESS_INSTANCED_PROP(Props, _ClumpStrength);
                if (strength <= 0.0) strength = 1.0;
                float phaseOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _ClumpPhase);
                float time = _Time.y * max(_SwayFreq, 0.02);
                float phase = time + phaseOffset + bladeSeed * 6.28318 + height01 * 1.35;
                float current = 0.28 + saturate(_CurrentSpeed * 0.72 + _CurrentGustStrength * 0.28);

                // Two slow bands prevent synchronized metronome motion; a small high band ripples tips.
                float primary = sin(phase) + sin(phase * 0.47 + bladeSeed * 4.1) * 0.34;
                float flutter = sin(phase * 1.91 + bladeSeed * 8.3) * _NoiseScale * 0.10;
                float3 offsetWS = flowDir * primary * _SwayAmp * strength * current * bendWeight;
                offsetWS += sideDir * flutter * bendWeight * (0.25 + height01 * 0.75);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz) + offsetWS;
                OUT.positionWS = positionWS;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                OUT.height01 = height01;
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float u = IN.uv.x;
                float y = IN.height01;

                float seed = UNITY_ACCESS_INSTANCED_PROP(Props, _ClumpSeed);
                if (seed <= 0.0) seed = UNITY_ACCESS_INSTANCED_PROP(Props, _VeinSeed);
                seed = Hash21(IN.positionWS.xz * 0.09 + seed * 9.7);

                float halfWidth = max(0.5 * (1.0 - y * _Taper), 0.12);
                float edgeDistance = abs(u - 0.5) / halfWidth;
                float profile = saturate(1.0 - pow(edgeDistance, _ProfileExp));
                float edgeNoise = (Hash21(float2(floor(y * _EdgeNoiseScale * 2.0), seed * 91.0)) - 0.5) * 2.0;
                float damage = step(0.91, Hash21(float2(floor(y * 13.0), seed * 47.0))) * smoothstep(0.68, 1.0, y);
                float alpha = saturate(profile - edgeNoise * _EdgeNoiseAmp - damage * 0.18);
                clip(alpha * tex.a - _Cutoff);

                half4 instanceTint = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseTint);
                half3 albedo = tex.rgb * lerp(instanceTint.rgb * 0.62, _TipTint.rgb, smoothstep(0.08, 1.0, y));
                float midVein = exp(-abs(u - 0.5) * 18.0);
                float sideVeins = pow(saturate(sin((u + y * 0.16 + seed) * 31.0)), 12.0) * 0.24;
                albedo = lerp(albedo, _VeinColor.rgb, saturate(midVein * 0.58 + sideVeins));

                half3 normalWS = normalize(IN.normalWS) * (isFrontFace ? 1.0h : -1.0h);
                half3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half backlight = saturate(dot(-normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS) * albedo;
                half3 diffuse = albedo * mainLight.color * (0.28h + ndotl * mainLight.shadowAttenuation);

                half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half specPower = lerp(18.0h, 96.0h, _Smoothness);
                half spec = pow(saturate(dot(normalWS, halfDir)), specPower) * _SpecularStrength;
                spec *= mainLight.shadowAttenuation * (0.45h + 0.55h * profile);
                half transmission = backlight * backlight * _Transmission * lerp(0.48h, 1.0h, y);
                transmission *= mainLight.shadowAttenuation;

                half3 color = ambient + diffuse;
                color += mainLight.color * spec;
                color += _TransmissionColor.rgb * mainLight.color * transmission;
                color = MixFog(color, IN.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}


