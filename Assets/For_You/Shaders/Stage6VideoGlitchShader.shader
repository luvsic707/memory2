Shader "Wakeup/Stage6VideoGlitchShader"
{
    Properties
    {
        _MainTex ("Normal Base Texture", 2D) = "white" {}
        _VideoTex ("Stage 5 Video Matrix Texture", 2D) = "white" {}
        _GlitchBlend ("Matrix Blend Factor", Range(0, 1)) = 0
        _GlitchIntensity ("Pixel Corruption Intensity", Range(0, 1)) = 0
        _RGBShift ("RGB Shift", Range(0, 0.05)) = 0.02
        _WaveSpeed ("Matrix Wave Speed", Float) = 1.5
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
            Texture2D _VideoTex;
            SamplerState sampler_VideoTex;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _GlitchBlend;
                float _GlitchIntensity;
                float _RGBShift;
                float _WaveSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
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
                float blend = saturate(_GlitchBlend);

                // 1. 正常材质采样
                float4 colNormal = _MainTex.Sample(sampler_MainTex, uv);

                // 2. Stage 5 视频矩阵采样 (带流体波动与动态拉伸)
                float2 videoUV = uv;
                videoUV.y = frac(videoUV.y * 1.5 - time * _WaveSpeed * 0.3);
                
                // 波动与像素错位
                if (_GlitchIntensity > 0.01)
                {
                    float blocks = lerp(120.0, 25.0, _GlitchIntensity);
                    float2 bUV = floor(uv * blocks) / blocks;
                    float n = hash(bUV + floor(time * 12.0));
                    if (n < _GlitchIntensity * 0.4)
                    {
                        videoUV += float2(sin(n * 6.28), cos(n * 6.28)) * 0.08;
                    }
                }

                videoUV = frac(abs(videoUV));

                // 3. 视频矩阵 RGB 色差
                float4 colVideo;
                if (_RGBShift > 0.0001)
                {
                    float2 shift = float2(_RGBShift, 0);
                    float r = _VideoTex.Sample(sampler_VideoTex, frac(videoUV + shift)).r;
                    float g = _VideoTex.Sample(sampler_VideoTex, videoUV).g;
                    float b = _VideoTex.Sample(sampler_VideoTex, frac(videoUV - shift)).b;
                    colVideo = float4(r, g, b, 1.0);
                }
                else
                {
                    colVideo = _VideoTex.Sample(sampler_VideoTex, videoUV);
                }

                // 4. 正常材质与 Stage 5 视频矩阵的动态交织
                float4 finalCol = lerp(colNormal, colVideo, blend);

                // 5. 闪烁故障时的彩虹像素杂色
                if (_GlitchIntensity > 0.1 && blend > 0.3)
                {
                    float2 glitchBlock = floor(uv * float2(25.0, 15.0));
                    float noiseVal = hash(glitchBlock + floor(time * 10.0));
                    if (noiseVal > 0.90)
                    {
                        float3 rainbow = float3(hash(glitchBlock + 1.1), hash(glitchBlock + 2.2), hash(glitchBlock + 3.3));
                        finalCol.rgb = lerp(finalCol.rgb, rainbow, 0.75);
                    }
                }

                return finalCol;
            }
            ENDHLSL
        }
    }
}
