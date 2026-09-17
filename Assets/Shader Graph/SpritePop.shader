Shader "Custom/SpritePop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color      ("Tint (HDR)", Color) = (1,1,1,1)
        _Saturation ("Saturation", Range(0,3)) = 1.6
        _Contrast   ("Contrast",   Range(0,3)) = 1.25
        _Brightness ("Brightness", Range(0,3)) = 1.0
        _RimBoost   ("Edge Boost", Range(0,2)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Saturation;
                float  _Contrast;
                float  _Brightness;
                float  _RimBoost;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                clip(tex.a - 0.01);

                float3 c = tex.rgb;

                // saturation: push away from luminance
                float lum = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(float3(lum, lum, lum), c, _Saturation);

                // contrast around mid grey
                c = (c - 0.5) * _Contrast + 0.5;

                c *= _Brightness;
                c *= _Color.rgb;          // HDR tint — values >1 push into bloom

                // optional: brighten the sprite's own bright areas further
                c += pow(saturate(lum), 3.0) * _RimBoost;

                return half4(max(c, 0), tex.a * _Color.a);
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}