Shader "FEasyUIMask/Precision/AlphaExtraction"
{
    Properties
    {
        _MainTex ("Sprite Alpha", 2D) = "white" {}
        _Threshold ("Alpha Threshold", Range(0.001, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Threshold;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                // z stores the original homogeneous W: retain perspective-correct UVs.
                o.position = float4(v.vertex.xy * v.vertex.z, 0, v.vertex.z);
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y = -o.position.y;
                #endif
                o.uv = v.uv;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                return float4(step(_Threshold, tex2D(_MainTex, i.uv).a), 0, 0, 1);
            }
            ENDCG
        }
    }
}
