Shader "Wakeup/CorridorWallShader"
{
    Properties
    {
        _MainTex ("Wall Texture", 2D) = "white" {}
        _FlowSpeed ("Fluid Flow Speed", Float) = 1.2
        _StretchScale ("Depth Stretch Scale", Float) = 4.0
        _GlitchAmount ("Pixel Glitch Intensity", Range(0, 1)) = 0
        _RGBShift ("RGB Chromatic Shift", Range(0, 0.05)) = 0.015
        _WaveWarp ("Wave Warp Distortion", Range(0, 2)) = 0.3
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

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _FlowSpeed;
                float _StretchScale;
                float _GlitchAmount;
                float _RGBShift;
                float _WaveWarp;
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

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;

                // 1. 有机呼吸脉动速度 (Pulsing Speed)
                float pulseSpeed = _FlowSpeed * (1.0 + sin(time * 2.0) * 0.35);

                // 2. 沿通道深度 (V 轴) 的 Slit-scan 流体拉伸
                float wave = sin(uv.x * 12.0 + time * 3.0) * 0.04 * _WaveWarp;
                uv.y = frac((uv.y + wave) * _StretchScale - time * pulseSpeed);

                // 3. Phase 2/3 的像素腐蚀
                if (_GlitchAmount > 0.01)
                {
                    float blocks = lerp(120.0, 20.0, _GlitchAmount);
                    float2 blockUV = floor(uv * blocks) / blocks;
                    float n = hash(blockUV + floor(time * 8.0));
                    if (n < _GlitchAmount * 0.5)
                    {
                        uv = blockUV + float2(sin(n * 6.28), cos(n * 6.28)) * 0.04;
                    }
                }

                // 4. RGB 色彩分离/色差拖尾 (Chromatic Aberration)
                float shift = _RGBShift * (1.0 + _GlitchAmount * 2.0);
                float r = _MainTex.Sample(sampler_MainTex, uv + float2(shift, 0.0)).r;
                float g = _MainTex.Sample(sampler_MainTex, uv).g;
                float b = _MainTex.Sample(sampler_MainTex, uv - float2(shift, 0.0)).b;

                float4 col = float4(r, g, b, 1.0);

                // 5. 深度光辉暗角
                float depthGlow = smoothstep(0.0, 0.8, input.uv.y);
                col.rgb *= lerp(0.35, 1.3, depthGlow);

                return col;
            }
            ENDHLSL
        }
    }
}
