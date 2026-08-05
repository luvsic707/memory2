Shader "Wakeup/Stage6VideoGlitchShader"
{
    Properties
    {
        _MainTex ("Normal Base Texture", 2D) = "white" {}
        _VideoTex ("Stage 5 Video Matrix Texture", 2D) = "white" {}
        _GlitchBlend ("Matrix Blend Factor", Range(0, 1)) = 0
        _GlitchIntensity ("Pixel Corruption Intensity", Range(0, 1)) = 0
        _SmearTurbulence ("Turbulence Smear Strength", Range(0, 5)) = 0
        _RGBShift ("RGB Shift", Range(0, 0.08)) = 0.02
        _WaveSpeed ("Matrix Wave Speed", Float) = 2.5
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
                float3 normalOS : NORMAL;
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
                float _SmearTurbulence;
                float _RGBShift;
                float _WaveSpeed;
            CBUFFER_END

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y;

                // 🌟 1. 顶点湍流拉伸 (Vertex Smear Turbulence - 图 1 效果)
                // 当 Glitch 触发时，沿环形与法线方向剧烈拉伸，让相邻模型熔融连成一片！
                if (_SmearTurbulence > 0.01)
                {
                    float smearNoise = hash(posWS.xz * 0.1 + floor(time * 8.0));
                    float3 dir = normalize(float3(-posWS.z, sin(posWS.x * 2.0 + time * 3.0), posWS.x));
                    posWS += dir * (_SmearTurbulence * (smearNoise * 0.8 + 0.2));
                }

                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;
                float blend = saturate(_GlitchBlend);

                // 🌟 2. 湍流线条拉丝 (Turbulent Pixel Smearing - 图 1 湍流拉丝)
                if (_SmearTurbulence > 0.05)
                {
                    float smearFactor = sin(input.positionWS.y * 3.0 + time * _WaveSpeed) * 0.5 + 0.5;
                    uv.y = lerp(uv.y, floor(uv.y * 8.0 + time * 4.0) / 8.0, _SmearTurbulence * 0.3 * smearFactor);
                    uv.x += sin(uv.y * 30.0 + time * 8.0) * 0.04 * _SmearTurbulence;
                }

                // 正常材质采样
                float4 colNormal = _MainTex.Sample(sampler_MainTex, uv);

                // Stage 5 视频矩阵采样
                float2 videoUV = uv;
                videoUV.y = frac(videoUV.y * 1.5 - time * _WaveSpeed * 0.3);

                if (_GlitchIntensity > 0.01)
                {
                    float blocks = lerp(120.0, 20.0, _GlitchIntensity);
                    float2 bUV = floor(uv * blocks) / blocks;
                    float n = hash(bUV + floor(time * 12.0));
                    if (n < _GlitchIntensity * 0.45)
                    {
                        videoUV += float2(sin(n * 6.28), cos(n * 6.28)) * 0.12;
                    }
                }

                videoUV = frac(abs(videoUV));

                // 3. 视频 RGB 色差与湍流混合
                float4 colVideo;
                float2 shift = float2(_RGBShift * (1.0 + _SmearTurbulence * 0.5), 0);
                float r = _VideoTex.Sample(sampler_VideoTex, frac(videoUV + shift)).r;
                float g = _VideoTex.Sample(sampler_VideoTex, videoUV).g;
                float b = _VideoTex.Sample(sampler_VideoTex, frac(videoUV - shift)).b;
                colVideo = float4(r, g, b, 1.0);

                // 4. 正常材质与视频矩阵拉丝混合
                float4 finalCol = lerp(colNormal, colVideo, blend);

                // 5. 彩虹像素湍流闪烁
                if (_SmearTurbulence > 0.2 || _GlitchIntensity > 0.2)
                {
                    float2 glitchBlock = floor(uv * float2(30.0, 10.0));
                    float noiseVal = hash(glitchBlock + floor(time * 15.0));
                    if (noiseVal > 0.88)
                    {
                        float3 rainbow = float3(hash(glitchBlock + 1.1), hash(glitchBlock + 2.2), hash(glitchBlock + 3.3));
                        finalCol.rgb = lerp(finalCol.rgb, rainbow, 0.8f);
                    }
                }

                return finalCol;
            }
            ENDHLSL
        }
    }
}
