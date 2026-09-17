// URP version of the original Built-in "Custom/ImageBlendEffect" shader.
// Written for URP's Full Screen Pass Renderer Feature (URP 14+ / Unity 2022.2+).
// The source screen image now comes in as _BlitTexture (via Blit.hlsl) instead of
// _MainTex/OnRenderImage. Everything else (blend math) is unchanged from the original.

Shader "Custom/ImageBlendEffect_URP"
{
	Properties
	{
		_BlendTex ("Image", 2D) = "white" {}
		_BumpMap ("Normalmap", 2D) = "bump" {}
		_BlendAmount ("Blend Amount", Range(0,1)) = 0.5
		_EdgeSharpness ("Edge Sharpness", Float) = 1
		_SeeThroughness ("See Throughness", Range(0,1)) = 0.2
		_Distortion ("Distortion", Float) = 0.1
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
		LOD 100
		ZWrite Off Cull Off ZTest Always

		Pass
		{
			Name "FrostBlendPass"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			// Blit.hlsl provides the vertex shader (Vert), Attributes/Varyings structs,
			// and the _BlitTexture the Renderer Feature fills in with the screen so far.
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			TEXTURE2D(_BlendTex);
			SAMPLER(sampler_BlendTex);
			TEXTURE2D(_BumpMap);
			SAMPLER(sampler_BumpMap);

			float _BlendAmount;
			float _EdgeSharpness;
			float _SeeThroughness;
			float _Distortion;

			half4 frag (Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float2 uv = input.texcoord;

				float4 mainColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
				float4 blendColor = SAMPLE_TEXTURE2D(_BlendTex, sampler_BlendTex, uv);

				blendColor.a = blendColor.a + (_BlendAmount * 2 - 1);
				blendColor.a = saturate(blendColor.a * _EdgeSharpness - (_EdgeSharpness - 1) * 0.5);

				// Distortion
				half2 bump = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv)).rg;
				mainColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + bump * blendColor.a * _Distortion);

				float4 overlayColor = blendColor;
				overlayColor.rgb = mainColor.rgb * (blendColor.rgb + 0.5) * (blendColor.rgb + 0.5); // double overlay

				blendColor = lerp(blendColor, overlayColor, _SeeThroughness);

				return lerp(mainColor, blendColor, blendColor.a);
			}
			ENDHLSL
		}
	}

	Fallback off
}
