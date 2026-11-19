// KnockRush sky: bright cartoon gradient (deep blue zenith → pale horizon), a soft sun glow and
// gentle procedural cloud bands. Cheap enough for mobile (no textures, a few ALU ops per pixel).
Shader "KnockRush/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.13, 0.47, 0.98, 1)
        _HorizonColor ("Horizon", Color) = (0.78, 0.93, 1, 1)
        _GroundColor ("Below Horizon", Color) = (0.55, 0.82, 1, 1)
        _SunColor ("Sun Glow", Color) = (1, 0.96, 0.82, 1)
        _SunSize ("Sun Glow Size", Range(1, 64)) = 10
        _HorizonSharpness ("Horizon Sharpness", Range(0.5, 8)) = 2.2
        _CloudColor ("Clouds", Color) = (1, 1, 1, 1)
        _CloudAmount ("Cloud Amount", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _SunColor;
                half _SunSize;
                half _HorizonSharpness;
                half4 _CloudColor;
                half _CloudAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float up = dir.y;

                half3 sky = up >= 0
                    ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(up), 1.0 / _HorizonSharpness))
                    : lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-up * 4.0));

                // Soft puffy cloud band just above the horizon (projected onto a dome).
                if (up > 0.0)
                {
                    float2 uv = dir.xz / (up + 0.25) * 2.2;
                    float n = Noise(uv) * 0.6 + Noise(uv * 2.3 + 7.1) * 0.3 + Noise(uv * 5.1 + 3.7) * 0.1;
                    float band = smoothstep(0.02, 0.18, up) * (1.0 - smoothstep(0.35, 0.75, up));
                    float cloud = smoothstep(1.0 - _CloudAmount, 1.0 - _CloudAmount + 0.18, n) * band;
                    sky = lerp(sky, _CloudColor.rgb, cloud * 0.9);
                }

                Light sun = GetMainLight();
                float sunDot = saturate(dot(dir, sun.direction));
                sky += _SunColor.rgb * (pow(sunDot, _SunSize * 8.0) * 1.2 + pow(sunDot, _SunSize) * 0.25);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
