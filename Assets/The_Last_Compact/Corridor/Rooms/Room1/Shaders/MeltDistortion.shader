Shader "Custom/MeltDistortion"
{
    Properties
    {
        _MainTex        ("Albedo (RGB)", 2D)          = "white" {}
        _Color          ("Base Color", Color)          = (1,1,1,1)

        [Header(Melt Control)]
        [Range(0,1)] _MeltProgress ("Melt Progress (0=solid 1=gone)", Float) = 0

        [Header(Vertex Distortion)]
        _VertexStrength ("Vertex Wobble Strength", Range(0, 5))  = 1.5
        _DropSpeed      ("Drip Down Speed", Range(0, 8))         = 2.0
        _NoiseScale     ("Noise Scale", Range(0.01, 3))          = 0.5

        [Header(UV Distortion)]
        _UVDistortAmount("UV Distort Amount", Range(0, 0.5))  = 0.15
        _UVScrollSpeed  ("UV Scroll Speed", Range(0, 3))      = 0.4

        [Header(Dissolve)]
        _DissolveEdge   ("Edge Glow Width", Range(0, 0.3)) = 0.08
        _EdgeColor      ("Edge Glow Color", Color)          = (1, 0.4, 0.05, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off

        Pass
        {
            Name "MeltDistortionPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _MeltProgress;
                float  _VertexStrength;
                float  _DropSpeed;
                float  _NoiseScale;
                float  _UVDistortAmount;
                float  _UVScrollSpeed;
                float  _DissolveEdge;
                float4 _EdgeColor;
            CBUFFER_END

            float _GlobalMeltProgress;

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            // ── 程序化噪声（不需要任何贴图）────────────────────
            float2 _hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Value Noise: 返回 0~1
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = frac(sin(dot(i,              float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1,0),float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0,1),float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1,1),float2(127.1, 311.7))) * 43758.5453);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // FBM (叠加多层噪声，更自然)
            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v   += amp * valueNoise(p);
                    p   *= 2.1;
                    amp *= 0.5;
                }
                return v;
            }
            // ────────────────────────────────────────────────────

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float  melt        : TEXCOORD1;
                float2 worldXZ     : TEXCOORD2;
                float  origY       : TEXCOORD3;
            };

            // ── Vertex Shader ────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // ── 0. 基于物体自身世界坐标，生成 [0~0.5] 的随机延迟 ──────────
                // Transforms object center (0,0,0) to World Space to get a unique position seed for this specific house instance
                float3 objCenter = TransformObjectToWorld(float3(0, 0, 0));
                
                // Generates a random float between 0.0 and 1.0 based on the World Position
                float randomVal = frac(sin(dot(objCenter.xyz, float3(12.9898, 78.233, 45.164))) * 43758.5453);
                
                // 每栋房子最多随机晚 50% 的时间才开始融化，确保它们不会像军训一样整齐划一
                float delay = randomVal * 0.5; 
                
                // 重新计算“本地”进度：比如 delay=0.5 的房子，全局跑到 0.5 它才刚开始动
                float localGlobalProgress = saturate((_GlobalMeltProgress - delay) / (1.0 - delay));

                float melt = saturate(_MeltProgress + localGlobalProgress);
                float3 pos = IN.positionOS.xyz;

                // ── 1. 获取噪声种子（基于物体自身的本地坐标）──────────
                float2 seed    = pos.xz * _NoiseScale;
                float  n1      = fbm(seed + float2(1.3, 2.7));
                float  n2      = fbm(seed + float2(4.1, 0.9));
                float  n3      = fbm(seed * 1.7 + float2(0.5, 3.3));

                // ── 2. 向外膨胀（XZ 鼓起）─────────
                // 以物体坐标原点(0,0,0)为中心向外
                float2 offsetDir = normalize(pos.xz + float2(n1 - 0.5, n2 - 0.5) * 0.1 + 0.001);
                float bulge      = melt * _VertexStrength * (0.5 + n3 * 0.5);
                pos.x += offsetDir.x * bulge;
                pos.z += offsetDir.y * bulge;

                // ── 3. 向下滴落（模拟重点部位塌陷）─────
                float dripAmount = melt * _DropSpeed * (0.5 + n2 * n3 * 1.5);
                pos.y -= dripAmount;

                // ── 4. 整体摇摆
                float sway = fbm(seed * 0.4 + float2(melt * 2.1, 0.6));
                pos.x += (sway - 0.5) * melt * 0.5 * _VertexStrength;

                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.melt        = melt;
                OUT.worldXZ     = pos.xz;
                OUT.origY       = IN.positionOS.y;

                return OUT;
            }

            // ── Fragment Shader ──────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                float melt = IN.melt;

                // ── 随机 Glitch 一瞬间材质错位/掉落 ──
                // 基于时间每秒闪烁十几次，取个随机因子
                float timeStep = floor(_Time.y * 18.0);
                float glitchProb = frac(sin(dot(float2(timeStep, IN.worldXZ.x), float2(12.9898, 78.233))) * 43758.5453);
                
                // 仅当开启融化、且达到了 96% 的超高阈值时，触发仅仅一帧的短促 Glitch
                float isGlitch = step(0.96, glitchProb) * step(0.01, melt) * (1.0 - step(0.99, melt));

                // UV 扰动（贴图错位）——用 melt 驱动偏移量
                float2 distortUV = IN.uv * 2.5 + float2(melt * 3.0, melt * 1.8);
                float  nX        = fbm(distortUV);
                float  nY        = fbm(distortUV + float2(3.7, 1.9));
                float2 uvOffset  = float2(nX - 0.5, nY - 0.5) * 2.0 * _UVDistortAmount * melt;

                // Glitch：在故障那一瞬间，给予剧烈的水平片状撕裂
                float glitchTear = (frac(sin(floor(IN.uv.y * 40.0) + timeStep) * 43758.5) - 0.5) * 1.2;
                uvOffset.x += isGlitch * glitchTear;

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + uvOffset) * _Color;

                // Glitch：模拟 Unity 丢失材质的经典 ERROR 紫粉色！
                if (isGlitch > 0.5 && frac(sin(floor(IN.uv.y * 20.0) - timeStep)*4375) > 0.4)
                {
                    col.rgb = float3(1.0, 0.0, 1.0); // 纯粹的死亡芭比粉
                    col.a = 1.0;                     // 恢复不透明度，让粉块极为清晰
                }

                // 噪声溶解
                float disMask   = fbm(IN.uv * 3.0 + float2(0.5, 0.3));
                float threshold = melt * 1.15 - 0.1;
                float dissolve  = disMask - threshold;

                // 边缘发光
                if (dissolve < _DissolveEdge && dissolve >= 0.0)
                {
                    float edgeT = 1.0 - dissolve / _DissolveEdge;
                    col.rgb = lerp(col.rgb, _EdgeColor.rgb, edgeT * edgeT);
                }

                col.a = saturate(dissolve * (1.0 / max(_DissolveEdge, 0.001)));
                clip(col.a - 0.005);

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
