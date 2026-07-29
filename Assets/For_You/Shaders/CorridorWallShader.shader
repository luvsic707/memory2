Shader "Wakeup/CorridorWallShader"
{
    Properties
    {
        _MainTex ("Wall Texture", 2D) = "white" {}
        _FlowSpeed ("Fluid Flow Speed", Float) = 1.2
        _StretchScale ("Depth Stretch Scale", Float) = 4.0
        _GlitchAmount ("Pixel Glitch Intensity", Range(0, 1)) = 0
        _RGBShift ("RGB Chromatic Shift", Range(0, 0.05)) = 0.0
        _WaveWarp ("Wave Warp Distortion", Range(0, 2)) = 0.0
        
        // 新增多维流动控制
        _FlowAngle ("Flow Diagonal Angle", Range(-3.14, 3.14)) = 0.785
        _VortexAmount ("Vortex Shear Amount", Range(0, 2)) = 0.2
        _SliceOffset ("Slit Strip Shift", Range(0, 1)) = 0
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
                float _FlowAngle;
                float _VortexAmount;
                float _SliceOffset;
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

                // 1. 有机呼吸流动速度
                float pulseSpeed = _FlowSpeed * (1.0 + sin(time * 1.8) * 0.4);

                // 2. 切片错位 (Slit Strip Shift - 横向/纵向条纹错位)
                if (_SliceOffset > 0.01)
                {
                    float strips = 16.0;
                    float stripID = floor(uv.x * strips);
                    float shiftDir = frac(stripID * 0.5) > 0.25 ? 1.0 : -1.0;
                    uv.y += shiftDir * time * 0.3 * _SliceOffset;
                }

                // 3. 斜向与双向复合流动 (Diagonal & Angular Flow)
                float cosA = cos(_FlowAngle);
                float sinA = sin(_FlowAngle);
                float2 dir = float2(cosA, sinA);
                
                // 结合 Z 轴深度拉伸
                uv.y *= _StretchScale;
                uv += dir * time * pulseSpeed * 0.5;
                uv = frac(uv);

                // 4. 水面漩涡扭曲 (Vortex Shear)
                if (_VortexAmount > 0.01)
                {
                    float2 cent = uv - 0.5;
                    float r = length(cent);
                    float a = atan2(cent.y, cent.x);
                    a += sin(r * 12.0 - time * 2.0) * _VortexAmount * _WaveWarp;
                    uv = float2(cos(a), sin(a)) * r + 0.5;
                }

                // 5. 类似 thezhapezhifter 的彩虹像素腐蚀
                if (_GlitchAmount > 0.01)
                {
                    float blocks = lerp(120.0, 20.0, _GlitchAmount);
                    float2 blockUV = floor(uv * blocks) / blocks;
                    float n = hash(blockUV + floor(time * 8.0));
                    if (n < _GlitchAmount * 0.55)
                    {
                        uv = blockUV + float2(sin(n * 6.28), cos(n * 6.28)) * 0.05;
                    }
                }

                // 6. RGB 色彩分离 (Chromatic Shift)
                float shift = _RGBShift * (1.0 + _GlitchAmount * 2.0);
                float rCol = _MainTex.Sample(sampler_MainTex, uv + float2(shift, shift * 0.5)).r;
                float gCol = _MainTex.Sample(sampler_MainTex, uv).g;
                float bCol = _MainTex.Sample(sampler_MainTex, uv - float2(shift, shift * 0.5)).b;

                float4 col = float4(rCol, gCol, bCol, 1.0);

                // 7. 深度暗角与光芒
                float depthGlow = smoothstep(0.0, 0.85, input.uv.y);
                col.rgb *= lerp(0.35, 1.35, depthGlow);

                return col;
            }
            ENDHLSL
        }
    }
}
