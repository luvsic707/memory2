Shader "Wakeup/CorridorWallShader"
{
    Properties
    {
        _MainTex ("Current Video Texture", 2D) = "white" {}
        _SubTex ("Last Video/Media Texture", 2D) = "white" {}
        _FlowSpeed ("Fluid Flow Speed", Float) = 0.8
        _StretchScale ("Depth Stretch Scale", Float) = 1.2
        _GlitchAmount ("Pixel Glitch Intensity", Range(0, 1)) = 0
        _RGBShift ("RGB Chromatic Shift", Range(0, 0.05)) = 0.0
        _WaveWarp ("Wave Warp Distortion", Range(0, 2)) = 0.2
        
        _JellyAmount ("Jelly Soft Deformation", Range(0, 1)) = 0.4
        _CuteWaveFreq ("Cute Wave Frequency", Float) = 3.14
        _FlowAngle ("Flow Diagonal Angle", Range(-3.14, 3.14)) = 0.0
        _VortexAmount ("Vortex Shear Amount", Range(0, 2)) = 0
        _SliceOffset ("Slice Shift Offset", Range(0, 1)) = 0
        _BorderFade ("Border Feather & Melt Amount", Range(0, 1)) = 0

        // 参考图 1 & 2 顶级艺术拖尾与消融
        _OilSmearArc ("Oil Smear Arc Sweep (Pic 1)", Range(0, 1)) = 0
        _ExplosiveRadialTrails ("Explosive Radial Speed Trails (Pic 2)", Range(0, 1)) = 0
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
                float3 positionWS : TEXCOORD1;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            Texture2D _SubTex;
            SamplerState sampler_SubTex;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _FlowSpeed;
                float _StretchScale;
                float _GlitchAmount;
                float _RGBShift;
                float _WaveWarp;
                float _JellyAmount;
                float _CuteWaveFreq;
                float _FlowAngle;
                float _VortexAmount;
                float _SliceOffset;
                float _BorderFade;
                float _OilSmearArc;
                float _ExplosiveRadialTrails;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz;
                float time = _Time.y;

                if (_JellyAmount > 0.01)
                {
                    float waveX = sin(posOS.y * _CuteWaveFreq + time * 1.5) * cos(posOS.z * 1.2 + time * 1.2);
                    float waveY = cos(posOS.x * _CuteWaveFreq + time * 1.3) * sin(posOS.z * 1.5 + time * 1.1);
                    posOS.xy += float2(waveX, waveY) * 0.08 * _JellyAmount;
                }

                output.positionWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformObjectToHClip(posOS);
                output.uv = input.uv;
                return output;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;

                // 1. 参考图 1：油彩弧形流体抹平 (Oil Smear Arc Sweep)
                if (_OilSmearArc > 0.01)
                {
                    float2 centerDist = uv - 0.5;
                    float angle = atan2(centerDist.y, centerDist.x);
                    float r = length(centerDist);
                    
                    float arcOffset = sin(angle * 3.0 + r * 10.0 - time * 2.5) * 0.15 * _OilSmearArc;
                    uv += float2(cos(angle + arcOffset), sin(angle + arcOffset)) * arcOffset;
                }

                float2 baseUV = uv;
                baseUV.y = baseUV.y * _StretchScale - time * _FlowSpeed * 0.3;

                // 2. 有机波动与水波
                if (_WaveWarp > 0.001 || _JellyAmount > 0.01)
                {
                    float waveFactor = _WaveWarp > 0.001 ? _WaveWarp : (_JellyAmount * 0.4);
                    float wave1 = sin(uv.y * 6.28 + time * 2.0) * 0.04 * waveFactor;
                    float wave2 = cos(uv.x * 6.28 - time * 1.8) * 0.04 * waveFactor;
                    baseUV += float2(wave1, wave2);
                }

                // 3. 画中画与切片
                float sliceID = floor(uv.y * 8.0);
                float isSubVideo = frac(sliceID * 0.382) > 0.5 ? 1.0 : 0.0;
                float2 subUV = baseUV + float2(sin(sliceID * 1.5), cos(sliceID * 2.1)) * 0.1;

                if (_VortexAmount > 0.01)
                {
                    float2 dist = uv - 0.5;
                    float radius = length(dist);
                    float angle = radius * _VortexAmount;
                    float s = sin(angle);
                    float c = cos(angle);
                    baseUV += float2(dist.x * c - dist.y * s, dist.x * s + dist.y * c) * 0.15;
                }

                if (_SliceOffset > 0.01)
                {
                    float r = hash(float2(sliceID, floor(time * 8.0)));
                    float shift = (r - 0.5) * 2.0 * _SliceOffset;
                    baseUV.x += shift * 0.25;
                }

                // 4. 梦幻 Glitch 像素块
                if (_GlitchAmount > 0.01)
                {
                    float blocks = lerp(180.0, 20.0, _GlitchAmount);
                    float2 blockUV = floor(uv * blocks) / blocks;
                    float timeSpeed = lerp(4.0, 18.0, _GlitchAmount);
                    float n = hash(blockUV + floor(time * timeSpeed));

                    if (n < _GlitchAmount * 0.5)
                    {
                        float glitchIntensity = lerp(0.02, 0.12, _GlitchAmount);
                        float2 glitchShift = float2(sin(n * 6.28), cos(n * 6.28)) * glitchIntensity;
                        baseUV += glitchShift;
                    }
                }

                baseUV = frac(abs(baseUV));
                subUV = frac(abs(subUV));

                // 🌟 5. 核心升级：参考图 2 爆炸式 360 度极速拖尾 (Explosive Radial Speed Trails)
                float4 col = float4(0, 0, 0, 1);
                if (_ExplosiveRadialTrails > 0.01)
                {
                    float2 dir = baseUV - float2(0.5, 0.5);
                    float4 accumCol = float4(0, 0, 0, 0);
                    int samples = 10;
                    float blurScale = 0.04 * _ExplosiveRadialTrails;

                    for (int i = 0; i < samples; i++)
                    {
                        float2 sampleUV = frac(abs(baseUV - dir * (float)i * blurScale));
                        accumCol += _MainTex.Sample(sampler_MainTex, sampleUV);
                    }
                    col = accumCol / (float)samples;
                }
                else
                {
                    if (_RGBShift > 0.0001)
                    {
                        float2 shift = float2(_RGBShift, 0);
                        float rA = _MainTex.Sample(sampler_MainTex, frac(baseUV + shift)).r;
                        float gA = _MainTex.Sample(sampler_MainTex, baseUV).g;
                        float bA = _MainTex.Sample(sampler_MainTex, frac(baseUV - shift)).b;
                        col = float4(rA, gA, bA, 1.0);
                    }
                    else
                    {
                        col = _MainTex.Sample(sampler_MainTex, baseUV);
                    }
                }



                // 6. 边缘消融与油彩渗透
                if (_BorderFade > 0.01)
                {
                    float edgeDist = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                    float edgeAlpha = smoothstep(0.0, 0.25 * _BorderFade, edgeDist);
                    float3 meltColor = lerp(col.rgb, col.gbr, 0.5 + 0.5 * sin(time * 2.5 + uv.x * 10.0));
                    col.rgb = lerp(meltColor, col.rgb, edgeAlpha);
                }

                return col;
            }
            ENDHLSL
        }
    }
}
