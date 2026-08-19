Shader "Unlit/Window"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Size("Size", float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Size;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float S(float a, float b, float t)
            {
                return smoothstep(a, b, t);
            }

            // ... Other converted helper functions

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // ... Rest of the converted code from mainImage

                // Original code
                fixed4 col = 0;
                float2 aspect = float2(2, 1);
                float2 gv = frac(i.uv*_Size*aspect)-.5;
                col.rg = gv;
                // ...

                return col;
            }
            ENDCG
        }
    }
}
