﻿Shader "Unlit/vertexOffset"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Data ("Data",vector) = (0,0,0,0)
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
                float3 normal : NORMAL;
                float4 color :COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 color :COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Data;

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPosition = mul (unity_ObjectToWorld, v.vertex).xyz;
                float dist = distance(float3(0,0,0),worldPosition);
                o.color.r = dist;
                float2 offsetTex = tex2Dlod(_MainTex, float4(dist*_Data.x,0.0 , 0.0, 0.0));

                o.vertex = UnityObjectToClipPos(v.vertex + v.normal * offsetTex.x);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return i.color.r;
            }
            ENDCG
        }
    }
}
