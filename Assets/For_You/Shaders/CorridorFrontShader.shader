Shader "Wakeup/CorridorFrontShader"
{
    Properties
    {
        _MainTex ("Current Image", 2D) = "white" {}
        _NextTex ("Next Image", 2D) = "white" {}
        _TransitionProgress ("Transition Progress", Range(0, 1)) = 0
        _TransitionMode ("Transition Mode (0:SoftRoundEcho, 1:Strips, 2:Fluid, 3:Portal, 4:Data)", Float) = 0
        _GlitchIntensity ("Pixel Corruption Intensity", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            Texture2D _NextTex;
            SamplerState sampler_NextTex;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _TransitionProgress;
                float _TransitionMode;
                float _GlitchIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            // 圆角矩形 Sdf 柔边计算
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;
                float progress = saturate(_TransitionProgress);

                float2 uvA = uv;
                float2 uvB = uv;
                float blendAlpha = progress;

                // ─────────────────────────────────────────────────────────────
                // 模式 0：浪漫柔角圆润套娃扩散 (Cute Soft Rounded Echo - 参考图 1 & 4 风格)
                // ─────────────────────────────────────────────────────────────
                if (_TransitionMode < 0.5)
                {
                    float2 p = uv - 0.5;
                    float d = sdRoundedBox(p, float2(0.35, 0.35) * progress, 0.15);
                    blendAlpha = smoothstep(0.01, -0.01, d);

                    // 弧形涟漪缩放
                    float waveScale = 1.0 + (1.0 - progress) * 0.15 * sin(length(p) * 20.0 - time * 4.0);
                    uvA = p * waveScale + 0.5;
                }
                // ─────────────────────────────────────────────────────────────
                // 模式 1：横向条纹柔和切片 (Horizontal Soft Strip)
                // ─────────────────────────────────────────────────────────────
                else if (_TransitionMode < 1.5)
                {
                    float stripCount = 10.0;
                    float stripID = floor(uv.y * stripCount);
                    float r = hash(float2(stripID, 1.23));
                    float dir = frac(stripID * 0.5) > 0.25 ? 1.0 : -1.0;

                    float offset = dir * (1.0 - progress) * 0.2 * (0.5 + r);
                    uvA.x += offset;
                    uvB.x -= dir * progress * 0.2 * (0.5 + r);
                    blendAlpha = progress;
                }
                // ─────────────────────────────────────────────────────────────
                // 模式 2：油彩流体浪漫涂抹 (Oil Paint Organic Liquid Dissolve - 参考图 2 风格)
                // ─────────────────────────────────────────────────────────────
                else if (_TransitionMode < 2.5)
                {
                    float n = noise(uv * 6.0 + time * 1.2);
                    float edge = progress * 1.3 - 0.15;
                    blendAlpha = smoothstep(edge - 0.2, edge + 0.2, n);

                    float2 fluidOffset = float2(n - 0.5, sin(n * 6.28)) * 0.03 * (1.0 - abs(progress - 0.5) * 2.0);
                    uvA += fluidOffset;
                    uvB -= fluidOffset;
                }
                // ─────────────────────────────────────────────────────────────
                // 模式 3：软心传送门膨胀 (Soft Portal Burst Expansion)
                // ─────────────────────────────────────────────────────────────
                else if (_TransitionMode < 3.5)
                {
                    float2 centerUV = uv - 0.5;
                    float scaleA = 1.0 + progress * 0.4;
                    float scaleB = 0.3 + (1.0 - progress) * 0.7;
                    uvA = centerUV / scaleA + 0.5;
                    uvB = centerUV / scaleB + 0.5;
                    blendAlpha = smoothstep(0.2, 0.8, progress);
                }
                // ─────────────────────────────────────────────────────────────
                // 模式 4：高频数据错位重影 (Data Displacement Overlay)
                // ─────────────────────────────────────────────────────────────
                else
                {
                    float d = hash(floor(uv * 18.0) + time);
                    float offset = (d - 0.5) * 0.06 * (1.0 - progress);
                    uvA.x += offset;
                    uvB.y += offset;
                    blendAlpha = progress;
                }

                // 像素 Glitch 扰动 (Phase 3 开启)
                if (_GlitchIntensity > 0.01)
                {
                    float blocks = lerp(150.0, 25.0, _GlitchIntensity);
                    float2 bUV = floor(uv * blocks) / blocks;
                    float n = hash(bUV + floor(time * 12.0));
                    if (n < _GlitchIntensity * 0.5)
                    {
                        uvA += float2(sin(n * 6.28), cos(n * 6.28)) * 0.06;
                        uvB += float2(cos(n * 6.28), sin(n * 6.28)) * 0.06;
                    }
                }

                float4 colA = _MainTex.Sample(sampler_MainTex, uvA);
                float4 colB = _NextTex.Sample(sampler_NextTex, uvB);

                float4 finalCol = lerp(colA, colB, saturate(blendAlpha));

                return finalCol;
            }
            ENDHLSL
        }
    }
}
