Shader "VirtualLine/Delivered Surface" {

Properties {
 _Color ("Color", Color) = (1,1,1,1)
 _MainTex ("Base Color", 2D) = "white" {}
 _Metallic ("Metallic", Range(0,1)) = 0
 _Glossiness ("Smoothness", Range(0,1)) = 0.2
 _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
 _Cull ("Cull", Float) = 0
 _EmissionColor ("Emission", Color) = (0,0,0,0)
}
SubShader { Tags { "RenderType"="Opaque" }

 Cull [_Cull]
 CGPROGRAM
 #pragma target 3.0
 #pragma surface surf Standard fullforwardshadows
 #pragma shader_feature_local _ALPHATEST_ON
 #pragma shader_feature_local _EMISSION
 sampler2D _MainTex;
 fixed4 _Color;
 half _Metallic;
 half _Glossiness;
 half _Cutoff;
 fixed4 _EmissionColor;
 struct Input { float2 uv_MainTex; };
 void surf(Input IN, inout SurfaceOutputStandard o) {
  fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
  #ifdef _ALPHATEST_ON
   clip(c.a - _Cutoff);
  #endif
  o.Albedo = c.rgb;
  o.Metallic = _Metallic;
  o.Smoothness = _Glossiness;
  o.Alpha = c.a;
  #ifdef _EMISSION
   o.Emission = _EmissionColor.rgb;
  #endif
 }
 ENDCG
}
Fallback "Diffuse"
}
