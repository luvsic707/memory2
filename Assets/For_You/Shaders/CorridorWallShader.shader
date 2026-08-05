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

                // 1. 保留具象视频比例 (适度拉伸，绝不过度扯成单条线条)
                float2 baseUV = uv;
                baseUV.y = frac(baseUV.y * _StretchScale - time * _FlowSpeed * 0.3);

                // 2. 有机波动 (Phase 1&2 浪漫水波)
                if (_WaveWarp > 0.001 || _JellyAmount > 0.01)
                {
                    float waveFactor = _WaveWarp > 0.001 ? _WaveWarp : (_JellyAmount * 0.4);
                    float wave1 = sin(uv.y * 6.28 + time * 2.0) * 0.04 * waveFactor;
                    float wave2 = cos(uv.x * 6.28 - time * 1.8) * 0.04 * waveFactor;
                    baseUV += float2(wave1, wave2);
                }

                // 3. 多重视频片段夹杂拼贴 (Interleaved Video Sub-Clips)
                float sliceID = floor(uv.y * 8.0);
                float isSubVideo = frac(sliceID * 0.382) > 0.5 ? 1.0 : 0.0;
                float2 subUV = baseUV + float2(sin(sliceID * 1.5), cos(sliceID * 2.1)) * 0.1;

                // 4. 漩涡与切片
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

                // 5. Phase 3 狂乱像素 Glitch 马赛克
                if (_GlitchAmount > 0.01)
                {
                    float blocks = lerp(120.0, 18.0, _GlitchAmount);
                    float2 blockUV = floor(uv * blocks) / blocks;
                    float n = hash(blockUV + floor(time * 15.0));

                    if (n < _GlitchAmount * 0.6)
                    {
                        float2 glitchShift = float2(sin(n * 6.28), cos(n * 6.28)) * 0.12 * _GlitchAmount;
                        baseUV += glitchShift;
                    }
                }

                // 6. 双视频/图像交错采样 (Main Video vs Sub Video)
                float4 colMain;
                float4 colSub;

                if (_RGBShift > 0.0001)
                {
                    float2 shift = float2(_RGBShift, 0);
                    float rA = _MainTex.Sample(sampler_MainTex, baseUV + shift).r;
                    float gA = _MainTex.Sample(sampler_MainTex, baseUV).g;
                    float bA = _MainTex.Sample(sampler_MainTex, baseUV - shift).b;
                    colMain = float4(rA, gA, bA, 1.0);

                    float rB = _SubTex.Sample(sampler_SubTex, subUV + shift).r;
                    float gB = _SubTex.Sample(sampler_SubTex, subUV).g;
                    float bB = _SubTex.Sample(sampler_SubTex, subUV - shift).b;
                    colSub = float4(rB, gB, bB, 1.0);
                }
                else
                {
                    colMain = _MainTex.Sample(sampler_MainTex, baseUV);
                    colSub = _SubTex.Sample(sampler_SubTex, subUV);
                }

                float4 col = lerp(colMain, colSub, isSubVideo * 0.45);

                // Phase 3 像素彩虹杂色贴花
                if (_GlitchAmount > 0.2)
                {
                    float2 glitchBlock = floor(uv * float2(30.0, 20.0));
                    float noiseVal = hash(glitchBlock + floor(time * 12.0));
                    if (noiseVal > 0.88)
                    {
                        float3 rainbowNoise = float3(
                            hash(glitchBlock + 1.1),
                            hash(glitchBlock + 2.2),
                            hash(glitchBlock + 3.3)
                        );
                        col.rgb = lerp(col.rgb, rainbowNoise, (_GlitchAmount - 0.2) * 0.8);
                    }
                }

                return col;
            }
            ENDHLSL
        }
    }
}
