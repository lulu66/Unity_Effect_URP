#ifndef SIMPLE_PBR_INCLUDE
#define SIMPLE_PBR_INCLUDE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

CBUFFER_START(UnityPerMaterial)
half4 _Color;
half4 NormalMap1_ST;
half4 NormalMap2_ST;
half _Smoothness;
half _Metallic;
half _Occlusion;
half _DistorbSpeed;
CBUFFER_END

TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap1);    SAMPLER(sampler_NormalMap1);
TEXTURE2D(_NormalMap2);    SAMPLER(sampler_NormalMap2);
TEXTURE2D(_PbrMap);    SAMPLER(sampler_PbrMap);
TEXTURE2D(_PlanarReflectionTexture);    SAMPLER(sampler_PlanarReflectionTexture);
struct appdata
{
	float4 positionOS  :  POSITION;
	float4 normalOS    :  NORMAL;
	float2 texcoord    :  TEXCOORD;
	float4 tangentOS   :  TANGENT;
	float4 texcoord1  :  TEXCOORD1; //lightmap uv
};

struct v2f
{
	float4 positionCS  :  SV_POSITION;
	float4 texcoord    :  TEXCOORD;
	float4 texcoord1   :  TEXCOORD1;
	float4 normalWS    :  TEXCOORD2;
	float4 tangentWS   :  TEXCOORD3;
	float4 binormalWS  :  TEXCOORD4;
	float4 shadowCoord :  TEXCOORD5;
	float4 ambientOrLightmapUV : TEXCOORD6;
	float4 screenPos   :  TEXCOORD7;
};

#endif