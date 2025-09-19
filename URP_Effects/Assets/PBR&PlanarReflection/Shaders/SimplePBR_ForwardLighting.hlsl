#ifndef SIMPLE_PBR_FORWARDLIGHTING_INCLUDE
#define SIMPLE_PBR_FORWARDLIGHTING_INCLUDE

#include "SimplePBR_Input.hlsl"
#include "SimplePBR_BRDF.hlsl"

v2f vert(appdata v)
{
    v2f o;

    VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, v.tangentOS);

    o.positionCS = vertexInput.positionCS;
    o.normalWS.xyz = normalInput.normalWS;
    o.tangentWS.xyz = normalInput.tangentWS;
    o.binormalWS.xyz = normalInput.bitangentWS;

    o.texcoord.xy = v.texcoord;
    o.texcoord.zw = TRANSFORM_TEX(v.texcoord, NormalMap1);
    o.texcoord1.xy = TRANSFORM_TEX(v.texcoord, NormalMap2);

    float3 posWS = TransformObjectToWorld(v.positionOS.xyz);

    o.normalWS.w = posWS.x;
    o.tangentWS.w = posWS.y;
    o.binormalWS.w = posWS.z;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    o.shadowCoord = GetShadowCoord(vertexInput); 
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    o.shadowCoord = TransformWorldToShadowCoord(posWS);
#else
    o.shadowCoord = float4(0, 0, 0, 0);
#endif
    o.screenPos = ComputeScreenPos(o.positionCS);

    OUTPUT_LIGHTMAP_UV(v.texcoord1, unity_LightmapST, o.ambientOrLightmapUV);
    OUTPUT_SH(o.normalWS.xyz, o.ambientOrLightmapUV);

    return o;
}

half4 frag(v2f i) : SV_Target
{
    half4 finalColor = half4(0,0,0,1);

    float3 worldPos = float3(i.normalWS.w, i.tangentWS.w, i.binormalWS.w);
    half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.texcoord.xy) * _Color;

    half3 normal1TS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap1, sampler_NormalMap1, i.texcoord.zw));
    half3 normal2TS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap2, sampler_NormalMap2, i.texcoord1.xy));
    half3 worldNormal1 = TransformTangentToWorld(normal1TS,half3x3(i.tangentWS.xyz, i.binormalWS.xyz, i.normalWS.xyz));
    half3 worldNormal2 = TransformTangentToWorld(normal2TS, half3x3(i.tangentWS.xyz, i.binormalWS.xyz, i.normalWS.xyz));

    half3 worldNormal = normalize(worldNormal1 + worldNormal2);

    half3 worldViewDir = GetWorldSpaceViewDir(worldPos);

    half4 pbrInfo = SAMPLE_TEXTURE2D(_PbrMap, sampler_PbrMap, i.texcoord.xy);

    half smoothness = pbrInfo.g * _Smoothness;
    half metallic = _Metallic;
    half occlusion = LerpWhiteTo(pbrInfo.a, _Occlusion);


    half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);

    half3 diffuseColor = albedo.rgb * oneMinusReflectivity;
    half3 specularColor = lerp(kDieletricSpec.rgb, albedo, metallic);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    half4 shadowCoord = i.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    half4 shadowCoord = TransformWorldToShadowCoord(worldPos);
#else
    half4 shadowCoord = float4(0, 0, 0, 0);
#endif

    Light mainLight = GetMainLight(shadowCoord);
    half3 lightColor = mainLight.color * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
    half3 lightDirWS = mainLight.direction;

    half3 refDir = reflect(-worldViewDir, worldNormal);

    half3 indirectDiffuse = SAMPLE_GI(i.ambientOrLightmapUV, i.ambientOrLightmapUV, worldNormal) * occlusion;

    //half3 indirectSpecular = Indirect_Reflection(refDir, 1.0 - smoothness, occlusion);
    float2 screenUV = i.screenPos.xy / i.screenPos.w;
	half3 indirectSpecular = Indirect_PlanarReflection(screenUV, 1 - smoothness, occlusion);
    half3 color = Lighting_BRDF(worldNormal, lightDirWS, lightColor, worldViewDir, specularColor, diffuseColor, indirectDiffuse,
        indirectSpecular, smoothness, oneMinusReflectivity);

    return half4(color,1);
}
#endif