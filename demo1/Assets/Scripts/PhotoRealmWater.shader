Shader "PhotoRealm/Water"
{
    Properties { _BaseColor("Water",Color)=(0.06,0.19,0.22,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half fog:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            Varyings vert(Attributes a)
            {
                Varyings o;o.world=TransformObjectToWorld(a.positionOS.xyz);
                o.world.y+=sin(o.world.x*2.1+_Time.y*1.7)*.025+sin(o.world.z*3.2-_Time.y*1.3)*.02;
                o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float dx=cos(i.world.x*2.1+sin(i.world.z*1.7)+_Time.y*1.7)*.055+sin(i.world.z*12+i.world.x*9+_Time.y*3)*.023;
                float dz=cos(i.world.z*3.2+sin(i.world.x*1.3)-_Time.y*1.3)*.052;
                float3 n=normalize(float3(-dx,1,-dz)),v=normalize(GetWorldSpaceViewDir(i.world));
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                float f=pow(1-saturate(dot(n,v)),4);
                float spec=pow(saturate(dot(n,normalize(light.direction+v))),160)*.75;
                half3 color=lerp(_BaseColor.rgb,half3(.38,.47,.51),f*.65);
                color*=.65+saturate(dot(n,light.direction))*.4*light.shadowAttenuation;
                color+=spec*light.color*light.shadowAttenuation;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
