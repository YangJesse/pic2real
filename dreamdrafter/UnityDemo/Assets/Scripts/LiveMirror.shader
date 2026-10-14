Shader "Dashed/LiveMirror"
{
    Properties { _MainTex("Live nave",2D)="white"{} _DustMask("Erasable dust",2D)="white"{} }
    SubShader
    {
        Tags {"RenderType"="Opaque"} Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; sampler2D _DustMask;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 live=tex2D(_MainTex,i.uv);fixed a=tex2D(_DustMask,i.uv).a;
                float grain=frac(sin(dot(i.uv*600,float2(12.9898,78.233)))*43758.5453);
                return fixed4(lerp(live.rgb,fixed3(.37,.34,.28)+grain*.045,a),1);
            }
            ENDCG
        }
    }
}
