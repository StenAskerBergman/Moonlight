// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/FogShader" {
     Properties {
         _MainTex ("Texture", 2D) = "white" {}
         _Color ("Fog Color", Color) = (1,1,1,1)
        
     }
 
     SubShader {
 
         Tags 
         {
             "Queue"="Transparent"
             "IgnoreProjector"="True"
             "RenderType"="Transparent"
             "PreviewType"="Plane"
         }
         
         Stencil
         {
             Ref 0
             Comp Always
             Pass Keep
             ReadMask 255
             WriteMask 255
         }
         
         Lighting Off 
         Cull Off 
         ZTest Off
         ZWrite Off 
         Blend SrcAlpha OneMinusSrcAlpha

 
         Pass 
         {
             CGPROGRAM
             #pragma vertex vert
             #pragma fragment frag
 
             #include "UnityCG.cginc"
 
             struct appdata_t {
                 float4 vertex : POSITION;
                 fixed4 color : COLOR;
                 float2 texcoord : TEXCOORD0;
             };
 
             struct v2f {
                 float4 vertex : SV_POSITION;
                 fixed4 color : COLOR;
                 float2 texcoord : TEXCOORD0;
             };
 
             sampler2D _MainTex;     // Active texture
             sampler2D _ExploredTex; // Explored texture
             uniform float4 _MainTex_ST;
             uniform fixed4 _Color;

             v2f vert (appdata_t v)
             {
                 v2f o;
                 o.vertex = UnityObjectToClipPos(v.vertex);
                 o.color = v.color * _Color;
                 o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                #ifdef UNITY_HALF_TEXEL_OFFSET
                 o.vertex.xy += (_ScreenParams.zw-1.0)*float2(-1,1);
                #endif
                 return o;
             }
 
            fixed4 frag (v2f i) : SV_Target
            {
                // Sampling both active and explored vision
                float activeAlpha = tex2D(_MainTex, i.texcoord).a;
                float exploredAlpha = tex2D(_ExploredTex, i.texcoord).a;

                fixed4 col = i.color;

                if (activeAlpha > 0.5) // If there's active vision, just use the active vision
                {
                    
                    // Fuzzy Line Fog
                    col.a *= activeAlpha; 
                    
                    // Hard Line Fog
                    // col.a *= 1.0;    

                }
                else if (exploredAlpha > 0.5) // If there's no active vision but there's explored vision
                {
                    col.a *= 0.0; // ACTIVE VISION
                }
                else // If there's neither active nor explored vision
                {
                    col.a = 0.5;
                }

                clip (col.a - 0.01);
                return col;
            }
            ENDCG 
         }
     }
 }