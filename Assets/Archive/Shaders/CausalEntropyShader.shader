Shader "Custom/CausalEntropyShader_Lit"
{
    Properties
    {
        [Header(Base Textures)]
        _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        _EntropyColor("Entropy Tint Color", Color) = (1,1,1,1)

        [Header(Causal Constraints)]
        _Distortion("Causal Distortion (Vertex)", Range(0, 1)) = 0.0
        _Erosion("System Erosion (Dissolve)", Range(0, 1)) = 0.0
        
        [Header(Settings)]
        _NoiseScale("Noise Scale", Float) = 20.0
        _DistortionSpeed("Distortion Speed", Float) = 5.0
        
        [Header(Spotlight Support)]
        _UseSpot("Use Spotlight Properties (0/1)", Float) = 0
        _SpotLightPos("Spot Light Position (WS)", Vector) = (0,10,0,0)
        _SpotLightDir("Spot Light Direction (WS)", Vector) = (0,-1,0,0)
        _SpotLightColor("Spot Light Color", Color) = (1,1,1,1)
        _SpotLightRange("Spot Light Range", Float) = 10.0
        _SpotLightInnerAngle("Spot Light Inner Angle (deg)", Float) = 25.0
        _SpotLightOuterAngle("Spot Light Outer Angle (deg)", Float) = 30.0
    }

    SubShader
    {
        // 关键修改：增加受光标签
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType" = "Lit" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" } // 允许 URP 传递灯光数据

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 引入 URP 光照计算核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2; 
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _EntropyColor;
                float _Distortion;
                float _Erosion;
                float _NoiseScale;
                float _DistortionSpeed;
                float _UseSpot;
                float4 _SpotLightPos;
                float4 _SpotLightDir;
                float4 _SpotLightColor;
                float _SpotLightRange;
                float _SpotLightInnerAngle;
                float _SpotLightOuterAngle;
            CBUFFER_END

            float hash(float n) { return frac(sin(n) * 43758.5453123); }
            float noise(float3 x)
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n = p.x + p.y * 57.0 + 113.0 * p.z;
                return lerp(lerp(lerp(hash(n + 0.0), hash(n + 1.0), f.x),
                                 lerp(hash(n + 57.0), hash(n + 58.0), f.x), f.y),
                            lerp(lerp(hash(n + 113.0), hash(n + 114.0), f.x),
                                 lerp(hash(n + 170.0), hash(n + 171.0), f.x), f.y), f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 保持原有的扭曲逻辑
                float n = noise(input.positionOS.xyz * _NoiseScale + _Time.y * _DistortionSpeed);
                float3 offset = input.normalOS * n * _Distortion * 0.03; 
                float3 finalPos = input.positionOS.xyz + offset;

                output.positionHCS = TransformObjectToHClip(finalPos);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.worldPos = TransformObjectToWorld(finalPos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 保持原有的溶解逻辑
                float n = noise(float3(input.uv * _NoiseScale, _Time.x));
                clip(n - _Erosion);

                // --- 核心光照计算 (伦勃朗光感) ---
                float3 normal = normalize(input.worldNormal);
                
                // 获取主光源（方向/默认）数据
                Light mainLight = GetMainLight();

                float3 diffuse = 0;

                if (_UseSpot > 0.5)
                {
                    // Spotlight properties path (user provides light position/direction/etc.)
                    float3 lightPos = _SpotLightPos.xyz;
                    float3 lightDirProp = normalize(_SpotLightDir.xyz);
                    float3 toLight = lightPos - input.worldPos;
                    float dist = length(toLight);
                    float3 L = normalize(toLight);

                    // distance attenuation (inverse linear falloff clamped)
                    float att = saturate(1.0 - dist / max(0.0001, _SpotLightRange));

                    // spot cone attenuation
                    float cosInner = cos(radians(_SpotLightInnerAngle));
                    float cosOuter = cos(radians(_SpotLightOuterAngle));
                    // dot between light forward (pointing direction) and vector from light to fragment
                    float spotCos = dot(normalize(-lightDirProp), L);
                    float spotAtt = smoothstep(cosOuter, cosInner, spotCos);

                    float3 lightColor = _SpotLightColor.rgb;
                    diffuse = saturate(dot(normal, L)) * lightColor * att * spotAtt;
                }
                else
                {
                    // Default path: use main directional light provided by URP
                    diffuse = saturate(dot(normal, mainLight.direction)) * mainLight.color;
                }
                
                // 固有色结合颜色偏移
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = texColor.rgb * _EntropyColor.rgb;

                // 最终颜色 = 固有色 * (漫反射光 + 极弱的环境补光)
                // 环境补光设为 0.05 左右，确保暗部有细节但不漏光
                float3 finalRGB = albedo * (diffuse + 0.05);
                
                // 边缘发光逻辑保持
                float edge = smoothstep(_Erosion, _Erosion + 0.05, n);
                finalRGB += (1.0 - edge) * _EntropyColor.rgb * 2.0;

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}