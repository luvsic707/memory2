Shader "Wakeup/FluidTunnelShader"
{
    Properties
    {
        _MainTex ("Current Image", 2D) = "white" {}
        _NextTex ("Next Image", 2D) = "white" {}
        _BlendProgress ("Image Blend", Range(0, 1)) = 0
        
        _PhaseProgress ("Phase Progress (0-1)", Range(0, 1)) = 0
        _TunnelSpeed ("Tunnel Zoom Speed", Float) = 0.8
        _StretchIntensity ("Radial Stretch Intensity", Range(0, 5)) = 1.5
        _GlitchIntensity ("Pixel Corrupt Intensity", Range(0, 1)) = 0
        _VortexDistortion ("Vortex Distortion", Range(0, 3)) = 0
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
                float _BlendProgress;
                float _PhaseProgress;
                float _TunnelSpeed;
                float _StretchIntensity;
                float _GlitchIntensity;
                float _VortexDistortion;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // 伪随机数生成器
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv - 0.5; // 中心为 (0,0)
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);

                // 1. 无限径向缩放 (Infinite Radial Zoom / Tunnel Effect)
                float time = _Time.y * _TunnelSpeed;
                
                // Brandon Eversole 风格：四周极大拉伸，中央保持图像
                float stretchFactor = pow(dist * 2.0, _StretchIntensity);
                float radialUVRadius = frac(dist * 1.5 - time * 0.3);

                // 2. 漩涡与时空折叠 (Vortex / Slit-scan Twist)
                if (_VortexDistortion > 0.01)
                {
                    angle += sin(dist * 10.0 - time * 2.0) * _VortexDistortion * (1.0 - dist);
                }

                // 计算拉伸后的 UV
                float2 warpedUV;
                warpedUV.x = cos(angle) * radialUVRadius + 0.5;
                warpedUV.y = sin(angle) * radialUVRadius + 0.5;

                // 保持中央相对清晰
                float centerMask = smoothstep(0.45, 0.05, dist);
                float2 finalUV = lerp(warpedUV, input.uv, centerMask);

                // 3. Pixel Glitch 腐蚀 (thezhapezhifter 风格，Phase 2/3 逐渐增强)
                if (_GlitchIntensity > 0.01)
                {
                    float blocks = lerp(200.0, 15.0, _GlitchIntensity);
                    float2 blockUV = floor(finalUV * blocks) / blocks;
                    float n = hash(blockUV + floor(time * 10.0));
                    
                    if (n < _GlitchIntensity * 0.6)
                    {
                        // 色块位移与彩虹色变
                        finalUV = blockUV + float2(sin(n * 6.28), cos(n * 6.28)) * 0.05;
                    }
                }

                // 4. 双图像平滑融合 (MainTex -> NextTex)
                float4 colA = _MainTex.Sample(sampler_MainTex, finalUV);
                float4 colB = _NextTex.Sample(sampler_NextTex, finalUV);
                float4 finalColor = lerp(colA, colB, _BlendProgress);

                // 5. 边缘暗角/边缘发光
                float vignette = smoothstep(0.7, 0.2, dist);
                finalColor.rgb *= lerp(0.3, 1.2, vignette);

                // Phase 3 结尾逐渐收缩成纯粹的算法窄管/马赛克
                if (_PhaseProgress > 0.7)
                {
                    float glitchPhase3 = saturate((_PhaseProgress - 0.7) / 0.26);
                    float3 glitchRGB = float3(
                        hash(finalUV + time),
                        hash(finalUV * 2.0 + time),
                        hash(finalUV * 3.0 + time)
                    );
                    finalColor.rgb = lerp(finalColor.rgb, glitchRGB, glitchPhase3 * 0.4);
                }

                return finalColor;
            }
            ENDHLSL
        }
    }
}
