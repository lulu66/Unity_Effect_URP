#ifndef SIMPLE_PBR_BRDF_INCLUDE
#define SIMPLE_PBR_BRDF_INCLUDE

half3 Indirect_Reflection(half3 refDir, half roughness, half occlusion)
{
	half perceptualRoughness = roughness * (1.7 - 0.7 * roughness);

	half mip = perceptualRoughness * UNITY_SPECCUBE_LOD_STEPS;

	half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, refDir, mip);

#if !defined(UNITY_USE_NATIVE_HDR)

	half3 irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);

#else

	half3 irradiance = encodedIrradiance.rbg;

#endif

	return irradiance * occlusion;
}

half3 Indirect_PlanarReflection(float2 uv, half roughness, half occlusion)
{
	half3 irradiance = SAMPLE_TEXTURE2D(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, uv).rgb;

	irradiance = lerp(0, irradiance,roughness);

	return irradiance * occlusion;
}

half3 Indirect_BRDF(half3 diffuseColor, half3 specularColor,half3 grazingTerm, half3 indirectDiffuse, half3 indirectSpecular, half fresnelTerm, half roughness2)
{
	half surfaceReduction = 1.0 / (roughness2 + 1.0);

	return (diffuseColor * indirectDiffuse + surfaceReduction * indirectSpecular * lerp(specularColor, grazingTerm, fresnelTerm));
}

half Direct_BRDF_Specular(half3 lightDirWS, half3 viewDirWS, half3 normalWS, half roughness, half roughness2)
{
	float3 halfDir = SafeNormalize(float3(lightDirWS)+float3(viewDirWS));

	float nh = saturate(dot(normalWS, halfDir));
	half lh = saturate(dot(lightDirWS, halfDir));


	float d = nh * nh * (roughness2 - 1.0) + 1.00001f;

	half lh2 = lh * lh;
	half specularTerm = (roughness2) / ((d * d) * max(0.1, lh2) * (roughness * 4.0 + 2.0));

#if defined (SHADER_API_MOBILE) || defined (SHADER_API_SWITCH)
	specularTerm = specularTerm - HALF_MIN;
#endif
	specularTerm = clamp(specularTerm, 0.0, 100.0);

	return specularTerm;
}

half3 Lighting_BRDF(half3 normalWS, half3 lightDirWS, half3 lightColor, half3 viewDirWS, half3 specularColor, half3 diffuseColor, half3 indirectDiffuse, 
	half3 indirectSpecular, half smoothness, half oneMinusReflectivity)
{
	half nl = saturate(dot(normalWS, lightDirWS));

	half perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);

	half roughness = max(perceptualRoughness * perceptualRoughness, HALF_MIN_SQRT);

	half roughness2 = max(roughness * roughness, HALF_MIN);

	half specularTerm = Direct_BRDF_Specular(lightDirWS, viewDirWS, normalWS, roughness, roughness2);

	half3 directBrdfColor = (diffuseColor + specularTerm * specularColor) * lightColor * nl;

	half3 grazingTerm = saturate(smoothness + (1.0 - oneMinusReflectivity));

	half fresnelTerm = Pow4(1.0 - saturate(dot(normalWS, viewDirWS)));

	half3 indirectBrdfColor = Indirect_BRDF(diffuseColor, specularColor, grazingTerm, indirectDiffuse, indirectSpecular, fresnelTerm, roughness2);

	return directBrdfColor + indirectBrdfColor;
}
#endif