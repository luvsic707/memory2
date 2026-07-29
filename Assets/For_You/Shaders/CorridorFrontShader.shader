Shader "Wakeup/CorridorFrontShader"
{
    Properties
    {
        _MainTex ("Current Image", 2D) = "white" {}
        _NextTex ("Next Image", 2D) = "white" {}
        _TransitionProgress ("Transition Progress", Range(0, 1)) = 0
        _TransitionMode ("Transition Mode (0:SlitScan, 1:Burst, 2:Feedback)", Float) = 0
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

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;

                float2 uvA = uv;
                float2 uvB = uv;
                float blendAlpha = _TransitionProgress;

                // ── 模式 0：狭缝流体扫过过渡 (Slit-scan Wave Sweep) ──
                if (_TransitionMode < 0.5)
                {
                    float wave = sin(uv.y * 10.0 + time * 3.0) * 0.05;
                    float threshold = uv.x + wave;
                    blendAlpha = smoothstep(threshold - 0.1, threshold + 0.1, _TransitionProgress * 1.2);
                }
                // ── 模式 1：双重爆裂绽放过渡 (Portal Burst Expansion) ──
                else if (_TransitionMode < 1.5)
                {
                    float2 centerUV = uv - 0.5;
                    float scaleA = 1.0 + _TransitionProgress * 0.5;
                    float scaleB = 0.2 + (1.0 - _TransitionProgress) * 0.8;
                    uvA = centerUV / scaleA + 0.5;
                    uvB = centerUV / scaleB + 0.5;
                }
                // ── 模式 2：光流残影重叠过渡 (Feedback Motion Overlay) ──
                else
                {
                    float offset = sin(time * 5.0) * 0.02 * (1.0 - _TransitionProgress);
                    uvA += float2(offset, -offset);
                    uvB += float2(-offset, offset);
                }

                // 像素 Glitch 扰动
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
