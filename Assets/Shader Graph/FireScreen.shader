Shader "Custom/FireScreen"
{
    Properties
    {
        [HDR] _TipColor  ("Flame Tip (coolest)", Color) = (0.45, 0.04, 0.01, 1)
        [HDR] _MidColor  ("Flame Mid", Color) = (1.0, 0.22, 0.02, 1)
        [HDR] _CoreColor ("Flame Core (hottest)", Color) = (1.0, 0.62, 0.12, 1)
        [HDR] _WhiteHot  ("White Hot", Color) = (1.0, 0.92, 0.65, 1)

        _VignettePower     ("Edge Falloff", Range(0.5, 8)) = 2.2
        _VignetteIntensity ("Edge Intensity", Range(0, 4)) = 1.4
        _SideWeight        ("Side/Top Weight", Range(0, 1)) = 0.5
        _BottomReach       ("Bottom Reach", Range(0.5, 4)) = 1.7

        _NoiseScale   ("Noise Scale (fine)", Range(1, 60)) = 22
        _NoiseSpeed   ("Noise Speed (fine)", Range(0, 4)) = 0.45
        _NoiseScale2  ("Noise Scale (broad)", Range(1, 30)) = 6
        _NoiseSpeed2  ("Noise Speed (broad)", Range(0, 4)) = 0.18
        _NoiseStretch ("Flame Stretch", Range(0.05, 1)) = 0.22
        _NoiseFloor   ("Noise Floor", Range(0, 1)) = 0.35
        _SpeedRamp    ("Speed Ramp with Heat", Range(0, 3)) = 1.2

        _TurbAmount ("Turbulence", Range(0, 0.3)) = 0.09
        _TurbScale  ("Turbulence Scale", Range(1, 20)) = 5

        _PulseSpeed  ("Pulse Speed", Range(0, 6)) = 1.3
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.14

        _Contrast  ("Flame Contrast", Range(0.5, 4)) = 1.8
        _EmberRate ("Ember Amount", Range(0, 1)) = 0.35

        _FireAmount ("Fire Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest  Always
        Cull   Off
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FireScreen"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TipColor;
                float4 _MidColor;
                float4 _CoreColor;
                float4 _WhiteHot;
                float  _VignettePower;
                float  _VignetteIntensity;
                float  _SideWeight;
                float  _BottomReach;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _NoiseScale2;
                float  _NoiseSpeed2;
                float  _NoiseStretch;
                float  _NoiseFloor;
                float  _SpeedRamp;
                float  _TurbAmount;
                float  _TurbScale;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _Contrast;
                float  _EmberRate;
                float  _FireAmount;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p, int octaves)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll(5)]
                for (int i = 0; i < octaves; i++)
                {
                    v += valueNoise(p) * amp;
                    p *= 2.02;          // non-integer so octaves don't align
                    amp *= 0.5;
                }
                return v;
            }

            // maps 0..1 through the four fire colours
            float3 fireRamp(float x)
            {
                float3 c = lerp(_TipColor.rgb,  _MidColor.rgb,   smoothstep(0.00, 0.35, x));
                c        = lerp(c,              _CoreColor.rgb,  smoothstep(0.35, 0.70, x));
                c        = lerp(c,              _WhiteHot.rgb,   smoothstep(0.78, 1.00, x));
                return c;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float heat = saturate(_FireAmount);
                if (heat <= 0.001) return half4(0, 0, 0, 0);

                float2 uv = input.texcoord;
                float  t  = _Time.y;

                // ---------- edge mask ----------
                float bottom = 1.0 - saturate(uv.y * _BottomReach);
                float sides  = 1.0 - saturate(min(uv.x, 1.0 - uv.x) * 2.4);
                float top    = 1.0 - saturate((1.0 - uv.y) * 2.6);

                float mask = max(bottom, max(sides, top) * _SideWeight);
                mask = pow(saturate(mask), _VignettePower) * _VignetteIntensity;
                mask *= lerp(0.6, 1.4, heat);

                // ---------- domain warp: makes flames curl instead of scroll ----------
                float speed = 1.0 + heat * _SpeedRamp;

                float2 warp;
                warp.x = valueNoise(uv * _TurbScale + float2(0.0, -t * 0.25)) - 0.5;
                warp.y = valueNoise(uv * _TurbScale + float2(5.2, -t * 0.31)) - 0.5;
                float2 wuv = uv + warp * _TurbAmount;

                // ---------- two stretched noise layers ----------
                float2 uv1 = wuv + float2(0.0, -t * _NoiseSpeed * speed);
                float2 uv2 = wuv + float2(0.04 * sin(t * 0.3), -t * _NoiseSpeed2 * speed);

                float n1 = fbm(float2(uv1.x * _NoiseScale,
                                      uv1.y * _NoiseScale * _NoiseStretch), 4);
                float n2 = fbm(float2(uv2.x * _NoiseScale2,
                                      uv2.y * _NoiseScale2 * _NoiseStretch), 2);

                n1 = lerp(_NoiseFloor, 1.4, saturate(n1));
                n2 = lerp(_NoiseFloor, 1.25, saturate(n2));

                float pulse = 1.0 + sin(t * _PulseSpeed * speed) * _PulseAmount * heat;

                float raw = mask * n1 * n2 * pulse * heat;

                // contrast: pushes flames into distinct tongues with gaps between,
                // rather than one continuous wash
                float alpha = saturate(pow(saturate(raw), _Contrast) * _Contrast);

                // ---------- colour from LOCAL intensity, not global heat ----------
                // this is what gives each flame dark red tips and a bright base
                float ramp = saturate(raw * 1.6);

                // base of the screen is hotter than the reaching tips
                ramp *= lerp(0.55, 1.15, bottom);
                // and everything runs hotter as temperature climbs
                ramp = saturate(ramp * lerp(0.7, 1.25, heat));

                float3 col = fireRamp(ramp);

                // ---------- embers: sparse bright specks drifting up ----------
                float2 ev = uv * float2(60.0, 22.0) + float2(0.0, -t * 1.6 * speed);
                float  e  = hash21(floor(ev));
                float  emberMask = step(1.0 - _EmberRate * 0.06, e);
                float  ember = emberMask * saturate(1.0 - length(frac(ev) - 0.5) * 3.0);
                ember *= mask * heat;

                col += ember * _WhiteHot.rgb * 2.0;
                alpha = saturate(alpha + ember * 0.8);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}