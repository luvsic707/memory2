Shader "Wakeup/CorridorFrontShader"
{
    Properties
    {
        _MainTex ("Current Image", 2D) = "white" {}
        _NextTex ("Next Image", 2D) = "white" {}
        _TransitionProgress ("Transition Progress", Range(0, 1)) = 0
        _TransitionMode ("Transition Mode (0:SoftRoundEcho, 1:Strips, 2:Fluid, 3:Portal, 4:Data)", Float) = 0
        _GlitchIntensity ("Pixel Corruption Intensity", Range(0, 1)) = 0
        
        // Front Wall 与四周融合的核心：爆炸极速拖尾与油彩抹平
        _OilSmearArc ("Oil Smear Arc Sweep (Pic 1)", Range(0, 1)) = 0
        _ExplosiveRadialTrails ("Explosive Radial Speed Trails (Pic 2)", Range(0, 1)) = 0
        _BorderFade ("Border Feather Amount", Range(0, 1)) = 0
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
                float _OilSmearArc;
                float _ExplosiveRadialTrails;
                float _BorderFade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz;
                output.positionCS = TransformObjectToHClip(posOS);
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

                // 1. 油彩弧形抹平 Sweep
                if (_OilSmearArc > 0.01)
                {
                    float2 centerDist = uv - 0.5;
                    float angle = atan2(centerDist.y, centerDist.x);
                    float r = length(centerDist);
                    float arcOffset = sin(angle * 3.0 + r * 10.0 - time * 2.5) * 0.15 * _OilSmearArc;
                    uv += float2(cos(angle + arcOffset), sin(angle + arcOffset)) * arcOffset;
                }

                float2 uvA = uv;
                float2 uvB = uv;
                float blendAlpha = progress;

                // 模式 0：柔角圆润套娃
                if (_TransitionMode < 0.5)
                {
                    float2 p = uv - 0.5;
                    float d = sdRoundedBox(p, float2(0.35, 0.35) * progress, 0.15);
                    blendAlpha = smoothstep(0.01, -0.01, d);

                    float waveScale = 1.0 + (1.0 - progress) * 0.15 * sin(length(p) * 20.0 - time * 4.0);
                    uvA = p * waveScale + 0.5;
                }
                // 模式 1：横向条纹撕裂
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
                // 模式 2：油彩流体涂抹
                else if (_TransitionMode < 2.5)
                {
                    float n = noise(uv * 6.0 + time * 1.2);
                    float edge = progress * 1.3 - 0.15;
                    blendAlpha = smoothstep(edge - 0.2, edge + 0.2, n);

                    float2 fluidOffset = float2(n - 0.5, sin(n * 6.28)) * 0.03 * (1.0 - abs(progress - 0.5) * 2.0);
                    uvA += fluidOffset;
                    uvB -= fluidOffset;
                }
                // 模式 3：传送门爆裂
                else if (_TransitionMode < 3.5)
                {
                    float2 centerUV = uv - 0.5;
                    float scaleA = 1.0 + progress * 0.4;
                    float scaleB = 0.3 + (1.0 - progress) * 0.7;
                    uvA = centerUV / scaleA + 0.5;
                    uvB = centerUV / scaleB + 0.5;
                    blendAlpha = smoothstep(0.2, 0.8, progress);
                }
                // 模式 4：高频数据重影
                else
                {
                    float d = hash(floor(uv * 18.0) + time);
                    float offset = (d - 0.5) * 0.06 * (1.0 - progress);
                    uvA.x += offset;
                    uvB.y += offset;
                    blendAlpha = progress;
                }

                // 像素 Glitch
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

                uvA = frac(abs(uvA));
                uvB = frac(abs(uvB));

                // 🌟 核心升级：爆炸式 360 度向外喷射拖尾 (Explosive Speed Trails directly blending into Side Walls)
                float4 finalCol;
                if (_ExplosiveRadialTrails > 0.01)
                {
                    float2 dir = uvA - float2(0.5, 0.5);
                    float4 accumColA = float4(0, 0, 0, 0);
                    float4 accumColB = float4(0, 0, 0, 0);
                    int samples = 8;
                    float blurScale = 0.04 * _ExplosiveRadialTrails;

                    for (int i = 0; i < samples; i++)
                    {
                        float2 sA = frac(abs(uvA - dir * (float)i * blurScale));
                        float2 sB = frac(abs(uvB - dir * (float)i * blurScale));
                        accumColA += _MainTex.Sample(sampler_MainTex, sA);
                        accumColB += _NextTex.Sample(sampler_NextTex, sB);
                    }
                    float4 cA = accumColA / (float)samples;
                    float4 cB = accumColB / (float)samples;
                    finalCol = lerp(cA, cB, saturate(blendAlpha));
                }
                else
                {
                    float4 colA = _MainTex.Sample(sampler_MainTex, uvA);
                    float4 colB = _NextTex.Sample(sampler_NextTex, uvB);
                    finalCol = lerp(colA, colB, saturate(blendAlpha));
                }



                // 边缘羽化消融与四周连通
                if (_BorderFade > 0.01)
                {
                    float edgeDist = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                    float edgeAlpha = smoothstep(0.0, 0.20 * _BorderFade, edgeDist);
                    float3 meltColor = lerp(finalCol.rgb, finalCol.gbr, 0.6 + 0.4 * sin(time * 3.0 + uv.x * 12.0));
                    finalCol.rgb = lerp(meltColor, finalCol.rgb, edgeAlpha);
                }

                return finalCol;
            }
            ENDHLSL
        }
    }
}
