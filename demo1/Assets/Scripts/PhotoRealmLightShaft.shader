Shader "PhotoRealm/LightShaft"
{
    Properties { _Tint("Warm light",Color)=(1,0.73,0.37,0.16) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            Varyings vert(Attributes a){Varyings o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float edge=pow(abs(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world)))),.8);
                float lengthFade=smoothstep(1,.65,i.uv.y)*lerp(1,.25,i.uv.y);
                return half4(_Tint.rgb,_Tint.a*edge*lengthFade);
            }
            ENDHLSL
        }
    }
}
