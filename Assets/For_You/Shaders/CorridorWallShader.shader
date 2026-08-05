Shader "Wakeup/CorridorWallShader"
{
    Properties
    {
        _MainTex ("Fluid Base Texture", 2D) = "white" {}
        _FlowSpeed ("Fluid Flow Speed", Float) = 0.8
        _StretchScale ("Depth Stretch Scale", Float) = 4.0
        _GlitchAmount ("Pixel Glitch Intensity", Range(0, 1)) = 0
        _RGBShift ("RGB Chromatic Shift", Range(0, 0.05)) = 0.0
        _WaveWarp ("Wave Warp Distortion", Range(0, 2)) = 0.0
        
        // 有机浪漫形变与软体圆润控制 (Phase 1&2 浪漫可爱)
        _JellyAmount ("Jelly Soft Deformation", Range(0, 1)) = 0.4
        _CuteWaveFreq ("Cute Wave Frequency", Float) = 3.14
        _FlowAngle ("Flow Diagonal Angle", Range(-3.14, 3.14)) = 0.785
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

                // Phase 1&2 有机软体圆润膨胀形变 (Jelly Organic Soft Squash)
                if (_JellyAmount > 0.01)
                {
                    float waveX = sin(posOS.y * _CuteWaveFreq + time * 1.5) * cos(posOS.z * 1.2 + time * 1.2);
                    float waveY = cos(posOS.x * _CuteWaveFreq + time * 1.3) * sin(posOS.z * 1.5 + time * 1.1);
                    posOS.xy += float2(waveX, waveY) * 0.12 * _JellyAmount;
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

                // 1. 深度与流体倾斜
                float sinA = sin(_FlowAngle);
                float cosA = cos(_FlowAngle);
                float2 rotatedUV = float2(
                    uv.x * cosA - uv.y * sinA,
                    uv.x * sinA + uv.y * cosA
                );

                float2 depthUV = float2(rotatedUV.x, rotatedUV.y * _StretchScale - time * _FlowSpeed);

                // 2. Phase 1&2 浪漫水波与柔和涟漪 (Romantic Wave Echo - 参考图 1 & 4)
                if (_WaveWarp > 0.001 || _JellyAmount > 0.01)
                {
                    float waveFactor = _WaveWarp > 0.001 ? _WaveWarp : (_JellyAmount * 0.3);
                    float wave1 = sin(depthUV.y * 3.14 + time * 2.0) * 0.08 * waveFactor;
                    float wave2 = cos(depthUV.x * 4.0 - time * 1.8) * 0.06 * waveFactor;
                    depthUV += float2(wave1, wave2);
                }

                // 3. 漩涡与切片
                if (_VortexAmount > 0.01)
                {
                    float2 dist = uv - 0.5;
                    float radius = length(dist);
                    float angle = radius * _VortexAmount;
                    float s = sin(angle);
                    float c = cos(angle);
                    depthUV += float2(dist.x * c - dist.y * s, dist.x * s + dist.y * c) * 0.2;
                }

                if (_SliceOffset > 0.01)
                {
                    float sliceID = floor(uv.y * 12.0);
                    float shift = frac(sliceID * 0.382) > 0.5 ? _SliceOffset : -_SliceOffset;
                    depthUV.x += shift * 0.15;
                }

                // 4. 色差拖尾与采样
                float4 col;
                if (_RGBShift > 0.0001)
                {
                    float2 shift = float2(_RGBShift, 0);
                    float r = _MainTex.Sample(sampler_MainTex, depthUV + shift).r;
                    float g = _MainTex.Sample(sampler_MainTex, depthUV).g;
                    float b = _MainTex.Sample(sampler_MainTex, depthUV - shift).b;
                    col = float4(r, g, b, 1.0);
                }
                else
                {
                    col = _MainTex.Sample(sampler_MainTex, depthUV);
                }

                return col;
            }
            ENDHLSL
        }
    }
}
