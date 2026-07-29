Shader "Wakeup/CorridorWallShader"
{
    Properties
    {
        _MainTex ("Wall Texture", 2D) = "white" {}
        _FlowSpeed ("Fluid Flow Speed", Float) = 1.2
        _StretchScale ("Depth Stretch Scale", Float) = 4.0
        _GlitchAmount ("Pixel Glitch Intensity", Range(0, 1)) = 0
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

                // 沿通道深度 (V 轴) 进行流体拉伸与滚动
                float time = _Time.y * _FlowSpeed;
                uv.y = frac(uv.y * _StretchScale - time);

                // Phase 2/3 加入类似 thezhapezhifter 的像素腐蚀
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

                float4 col = _MainTex.Sample(sampler_MainTex, uv);

                // 越靠近深处尽头越亮，越靠近玩家边缘越带暗角拖尾
                float depthGlow = smoothstep(0.0, 0.8, input.uv.y);
                col.rgb *= lerp(0.4, 1.2, depthGlow);

                return col;
            }
            ENDHLSL
        }
    }
}
