Shader "Custom/URPStainedGlass" {
    Properties {
        _BaseMap("Base Map", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}
        _BumpAmt("Bump Amount", Range(0, 128)) = 10
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float3 normal : NORMAL;
            };

            sampler2D _BaseMap;
            sampler2D _BumpMap;
            float _BumpAmt;

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = v.normal;
                return o;
            }

            half4 frag(v2f i) : SV_Target {
                float3 normal = normalize(i.normal);
                float2 bump = tex2D(_BumpMap, normal.xy).rg;
                float2 offset = bump * _BumpAmt;
                half4 col = tex2D(_BaseMap, normal.xy + offset);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
