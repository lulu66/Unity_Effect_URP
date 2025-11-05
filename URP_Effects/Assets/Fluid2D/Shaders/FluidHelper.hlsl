#ifndef FLUID_HELPER_H

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

half4 Tex2dNearest(TEXTURE2D_PARAM(tex, sampler_tex), half2 uv, float4 texSize, half4 warpmask)
{
	// 纹理坐标包装：根据warpmask决定如何对uv包装，clamp还是repeat; frac实现重复包装
	uv.x = lerp(uv.x, frac(uv.x), warpmask.x);
	uv.y = lerp(uv.y, frac(uv.y), warpmask.y);

	// 边界检查：
	int bx = lerp(1, step(0, uv.x) - step(1, uv.x), warpmask.z); // u只有在[0,1]才=1，其余=0
	int by = lerp(1, step(0, uv.y) - step(1, uv.y), warpmask.w); // v只有在[0,1]才=1，其余=0

	// 将坐标限制在纹素中心
	uv.x = clamp(uv.x,texSize.x * 0.5, 1 - texSize.x * 0.5);
	uv.y = clamp(uv.y, texSize.y * 0.5, 1 - texSize.y * 0.5);

	// 最终采样
	return lerp(0, SAMPLE_TEXTURE2D(tex, sampler_tex, uv), bx*by);
}

half4 Tex2dBilinear(TEXTURE2D_PARAM(tex, sampler_tex), half2 uv, float4 texSize, half4 warpmask)
{
	// 处理uv,让uv处于边界，而非纹素中心
	half2 st = uv * texSize.zw - 0.5;
	half2 f = frac(st);
	uv = floor(st);

	// 取相邻四个像素纹理坐标的左下角和右上角
	half4 uvMinMax = half4((uv+0.5), (uv+1.5))/texSize.zwzw;

	// 取纹素值
	half4 texA = Tex2dNearest(TEXTURE2D_ARGS(tex, sampler_tex), uvMinMax.xy, texSize, warpmask);
	half4 texB = Tex2dNearest(TEXTURE2D_ARGS(tex, sampler_tex), uvMinMax.xw, texSize, warpmask);
	half4 texC = Tex2dNearest(TEXTURE2D_ARGS(tex, sampler_tex), uvMinMax.zy, texSize, warpmask);
	half4 texD = Tex2dNearest(TEXTURE2D_ARGS(tex, sampler_tex), uvMinMax.zw, texSize, warpmask);

	// 双线性插值
	return lerp(lerp(texA, texB,f.y), lerp(texC, texD, f.y),f.x);
}

half SquareFalloff(in half2 uv, in half falloff)
{
	half2 marquee = max((abs(uv - 0.5) * 2 - (1 - falloff)) / falloff, 0);
	return saturate(1 - length(marquee));
}

#endif

