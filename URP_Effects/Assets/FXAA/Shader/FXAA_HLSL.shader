Shader "Hidden/Anti-Aliasing/FXAA_HLSL"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        ZTest Always ZWrite Off Cull back

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize;
            float3 _QualitySettings;

            #define FXAA_QUALITY__PS 5
            #define FXAA_QUALITY__P0 1.0
            #define FXAA_QUALITY__P1 1.5
            #define FXAA_QUALITY__P2 2.0
            #define FXAA_QUALITY__P3 4.0
            #define FXAA_QUALITY__P4 12.0

//#define FxaaTexTop(tex, uv) SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

//#define FxaaTexOff(tex, uv, offset, fxaaQualityRcpFrameXY) SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + (offset * fxaaQualityRcpFrameXY));

            inline half4 FxaaTexTop(half2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_LinearClamp, uv, 0);
            }

            inline half4 FxaaTexOff(half2 uv, half2 offset, half2 fxaaQualityRcpFrameXY)
            {
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_LinearClamp, uv + (offset * fxaaQualityRcpFrameXY), 0);
            }

            inline half FxaaLuma(half4 rgba) { return rgba.y; }

            half4 FxaaPixelShader(half2 pos, half2 fxaaQualityRcpFrame, half fxaaQualitySubpix,half fxaaQualityEdgeThreshold, half fxaaQualityEdgeThresholdMin)
            {
                /*--------------------------------------------------------------------------*/
                half2 posM;
                posM.x = pos.x;
                posM.y = pos.y;

                float4 rgbyM = FxaaTexTop(posM);
#define lumaM rgbyM.y
                half lumaS = FxaaLuma(FxaaTexOff(posM, half2(0, 1), fxaaQualityRcpFrame.xy));
                half lumaE = FxaaLuma(FxaaTexOff(posM, half2(1, 0), fxaaQualityRcpFrame.xy));
                half lumaN = FxaaLuma(FxaaTexOff(posM, half2(0, -1), fxaaQualityRcpFrame.xy));
                half lumaW = FxaaLuma(FxaaTexOff(posM, half2(-1, 0), fxaaQualityRcpFrame.xy));

                /*--------------------------------------------------------------------------*/
                half maxSM = max(lumaS, lumaM);
                half minSM = min(lumaS, lumaM);
                half maxESM = max(lumaE, maxSM);
                half minESM = min(lumaE, minSM);
                half maxWN = max(lumaN, lumaW);
                half minWN = min(lumaN, lumaW);
                half rangeMax = max(maxWN, maxESM);
                half rangeMin = min(minWN, minESM);
                half rangeMaxScaled = rangeMax * fxaaQualityEdgeThreshold;
                half range = rangeMax - rangeMin;
                half rangeMaxClamped = max(fxaaQualityEdgeThresholdMin, rangeMaxScaled);
                bool earlyExit = range < rangeMaxClamped;
                /*--------------------------------------------------------------------------*/
                if (earlyExit)
                    return rgbyM;
                /*--------------------------------------------------------------------------*/
                half lumaNW = FxaaLuma(FxaaTexOff(posM, half2(-1, -1), fxaaQualityRcpFrame.xy));
                half lumaSE = FxaaLuma(FxaaTexOff(posM, half2(1, 1), fxaaQualityRcpFrame.xy));
                half lumaNE = FxaaLuma(FxaaTexOff(posM, half2(1, -1), fxaaQualityRcpFrame.xy));
                half lumaSW = FxaaLuma(FxaaTexOff(posM, half2(-1, 1), fxaaQualityRcpFrame.xy));




                /*--------------------------------------------------------------------------*/
                half lumaNS = lumaN + lumaS;
                half lumaWE = lumaW + lumaE;
                half subpixRcpRange = 1.0 / range;
                half subpixNSWE = lumaNS + lumaWE;
                half edgeHorz1 = (-2.0 * lumaM) + lumaNS;
                half edgeVert1 = (-2.0 * lumaM) + lumaWE;
                /*--------------------------------------------------------------------------*/
                half lumaNESE = lumaNE + lumaSE;
                half lumaNWNE = lumaNW + lumaNE;
                half edgeHorz2 = (-2.0 * lumaE) + lumaNESE;
                half edgeVert2 = (-2.0 * lumaN) + lumaNWNE;
                /*--------------------------------------------------------------------------*/
                half lumaNWSW = lumaNW + lumaSW;
                half lumaSWSE = lumaSW + lumaSE;
                half edgeHorz4 = (abs(edgeHorz1) * 2.0) + abs(edgeHorz2);
                half edgeVert4 = (abs(edgeVert1) * 2.0) + abs(edgeVert2);
                half edgeHorz3 = (-2.0 * lumaW) + lumaNWSW;
                half edgeVert3 = (-2.0 * lumaS) + lumaSWSE;
                half edgeHorz = abs(edgeHorz3) + edgeHorz4;
                half edgeVert = abs(edgeVert3) + edgeVert4;
                /*--------------------------------------------------------------------------*/
                half subpixNWSWNESE = lumaNWSW + lumaNESE;
                half lengthSign = fxaaQualityRcpFrame.x;
                bool horzSpan = edgeHorz >= edgeVert;
                half subpixA = subpixNSWE * 2.0 + subpixNWSWNESE;
                /*--------------------------------------------------------------------------*/
                if (!horzSpan) lumaN = lumaW;
                if (!horzSpan) lumaS = lumaE;
                if (horzSpan) lengthSign = fxaaQualityRcpFrame.y;
                half subpixB = (subpixA * (1.0 / 12.0)) - lumaM;
                /*--------------------------------------------------------------------------*/
                half gradientN = lumaN - lumaM;
                half gradientS = lumaS - lumaM;
                half lumaNN = lumaN + lumaM;
                half lumaSS = lumaS + lumaM;
                bool pairN = abs(gradientN) >= abs(gradientS);
                half gradient = max(abs(gradientN), abs(gradientS));
                if (pairN) lengthSign = -lengthSign;
                half subpixC = saturate(abs(subpixB) * subpixRcpRange);
                /*--------------------------------------------------------------------------*/
                float2 posB;
                posB.x = posM.x;
                posB.y = posM.y;
                float2 offNP;
                offNP.x = (!horzSpan) ? 0.0 : fxaaQualityRcpFrame.x;
                offNP.y = (horzSpan) ? 0.0 : fxaaQualityRcpFrame.y;
                if (!horzSpan) posB.x += lengthSign * 0.5;
                if (horzSpan) posB.y += lengthSign * 0.5;
                /*--------------------------------------------------------------------------*/
                float2 posN;
                posN.x = posB.x - offNP.x * FXAA_QUALITY__P0;
                posN.y = posB.y - offNP.y * FXAA_QUALITY__P0;
                float2 posP;
                posP.x = posB.x + offNP.x * FXAA_QUALITY__P0;
                posP.y = posB.y + offNP.y * FXAA_QUALITY__P0;
                half subpixD = ((-2.0) * subpixC) + 3.0;
                half lumaEndN = FxaaLuma(FxaaTexTop(posN));
                half subpixE = subpixC * subpixC;
                half lumaEndP = FxaaLuma(FxaaTexTop(posP));
                /*--------------------------------------------------------------------------*/
                if (!pairN) lumaNN = lumaSS;
                half gradientScaled = gradient * 1.0 / 4.0;
                half lumaMM = lumaM - lumaNN * 0.5;
                half subpixF = subpixD * subpixE;
                bool lumaMLTZero = lumaMM < 0.0;
                /*--------------------------------------------------------------------------*/
                lumaEndN -= lumaNN * 0.5;
                lumaEndP -= lumaNN * 0.5;
                bool doneN = abs(lumaEndN) >= gradientScaled;
                bool doneP = abs(lumaEndP) >= gradientScaled;
                if (!doneN) posN.x -= offNP.x * FXAA_QUALITY__P1;
                if (!doneN) posN.y -= offNP.y * FXAA_QUALITY__P1;
                bool doneNP = (!doneN) || (!doneP);
                if (!doneP) posP.x += offNP.x * FXAA_QUALITY__P1;
                if (!doneP) posP.y += offNP.y * FXAA_QUALITY__P1;
                /*--------------------------------------------------------------------------*/
                if (doneNP)
                {
                    if (!doneN) lumaEndN = FxaaLuma(FxaaTexTop(posN.xy));
                    if (!doneP) lumaEndP = FxaaLuma(FxaaTexTop(posP.xy));
                    if (!doneN) lumaEndN = lumaEndN - lumaNN * 0.5;
                    if (!doneP) lumaEndP = lumaEndP - lumaNN * 0.5;
                    doneN = abs(lumaEndN) >= gradientScaled;
                    doneP = abs(lumaEndP) >= gradientScaled;
                    if (!doneN) posN.x -= offNP.x * FXAA_QUALITY__P2;
                    if (!doneN) posN.y -= offNP.y * FXAA_QUALITY__P2;
                    doneNP = (!doneN) || (!doneP);
                    if (!doneP) posP.x += offNP.x * FXAA_QUALITY__P2;
                    if (!doneP) posP.y += offNP.y * FXAA_QUALITY__P2;
                    /*--------------------------------------------------------------------------*/

                    if (doneNP)
                    {
                        if (!doneN) lumaEndN = FxaaLuma(FxaaTexTop(posN.xy));
                        if (!doneP) lumaEndP = FxaaLuma(FxaaTexTop(posP.xy));
                        if (!doneN) lumaEndN = lumaEndN - lumaNN * 0.5;
                        if (!doneP) lumaEndP = lumaEndP - lumaNN * 0.5;
                        doneN = abs(lumaEndN) >= gradientScaled;
                        doneP = abs(lumaEndP) >= gradientScaled;
                        if (!doneN) posN.x -= offNP.x * FXAA_QUALITY__P4;
                        if (!doneN) posN.y -= offNP.y * FXAA_QUALITY__P4;
                        doneNP = (!doneN) || (!doneP);
                        if (!doneP) posP.x += offNP.x * FXAA_QUALITY__P4;
                        if (!doneP) posP.y += offNP.y * FXAA_QUALITY__P4;
                        /*--------------------------------------------------------------------------*/
                        /*--------------------------------------------------------------------------*/
                    }
                    /*--------------------------------------------------------------------------*/
                }
                /*--------------------------------------------------------------------------*/



                half dstN = posM.x - posN.x;
                half dstP = posP.x - posM.x;
                if (!horzSpan) dstN = posM.y - posN.y;
                if (!horzSpan) dstP = posP.y - posM.y;
                /*--------------------------------------------------------------------------*/
                bool goodSpanN = (lumaEndN < 0.0) != lumaMLTZero;
                half spanLength = (dstP + dstN);
                bool goodSpanP = (lumaEndP < 0.0) != lumaMLTZero;
                half spanLengthRcp = 1.0 / spanLength;
                /*--------------------------------------------------------------------------*/
                bool directionN = dstN < dstP;
                half dst = min(dstN, dstP);
                bool goodSpan = directionN ? goodSpanN : goodSpanP;
                half subpixG = subpixF * subpixF;
                half pixelOffset = (dst * (-spanLengthRcp)) + 0.5;
                half subpixH = subpixG * fxaaQualitySubpix;
                /*--------------------------------------------------------------------------*/
                half pixelOffsetGood = goodSpan ? pixelOffset : 0.0;
                half pixelOffsetSubpix = max(pixelOffsetGood, subpixH);
                if (!horzSpan) posM.x += pixelOffsetSubpix * lengthSign;
                if (horzSpan) posM.y += pixelOffsetSubpix * lengthSign;

                return half4(FxaaTexTop(posM).xyz, lumaM);
                }

            half4 Frag(Varyings input) : SV_Target
            {
                half2 uv = input.uv;
                half2 positionNDC = uv;
                half2 positionSS = uv * _MainTex_TexelSize.zw;

                half4 color = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_LinearClamp, uv, 0);

                color = FxaaPixelShader(uv, _MainTex_TexelSize.xy, _QualitySettings.x, _QualitySettings.y, _QualitySettings.z);

                return color;
            }

        ENDHLSL
        Pass
        {
            Name "Easy FXAA"

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment Frag

            ENDHLSL
        }
    }
}
