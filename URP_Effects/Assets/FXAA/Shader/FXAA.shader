Shader "Hidden/Anti-Aliasing/FXAA"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    CGINCLUDE

        #include "UnityCG.cginc"
        
        #define FXAA_PC 1

        #define FXAA_HLSL_3 1
        #define FXAA_QUALITY__PRESET 12

        #define FXAA_GREEN_AS_LUMA 1

        #pragma target 3.0
        #include "./FXAA3.cginc"

        float3 _QualitySettings;
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;

        struct AttributesDefault
        {
            float4 vertex : POSITION;
            float4 texcoord : TEXCOORD0;
        };

        struct VaryingsDefault
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        VaryingsDefault VertDefault(AttributesDefault v)
        {
            VaryingsDefault o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord.xy;
            return o;
        }

        half4 Frag(VaryingsDefault i) : SV_Target
        {

		    //half4 color = FxaaPixelShader(UnityStereoScreenSpaceUVAdjust(i.uv, float4(1, 1, 0, 0)), 
      //                                    0,
      //                                    _MainTex, _MainTex, _MainTex, _MainTex_TexelSize.xy,
      //                                    0, 0, 0,
      //                                    _QualitySettings.x, _QualitySettings.y, _QualitySettings.z,
      //                                    0, 0, 0, 0);

                        half4 color = FxaaPixelShader(i.uv,
                                          _MainTex, _MainTex, _MainTex, _MainTex_TexelSize.xy,
                                          _QualitySettings.x, _QualitySettings.y, _QualitySettings.z);
            return color;
        }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

                #pragma vertex VertDefault
                #pragma fragment Frag

            ENDCG
        }
    }
}
