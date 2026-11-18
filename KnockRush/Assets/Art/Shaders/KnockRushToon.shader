// KnockRush stylised toon shader for URP (mobile friendly, SRP Batcher compatible).
// - Two-tone ramp lighting from the main light (with shadows) + SH ambient
// - Rim light and a small cartoon specular highlight
// - Optional vertex colours: RGB = albedo, A = tint mask (1 = multiply by _BaseColor)
Shader "KnockRush/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0.42, 0.1, 1)
        _BaseMap ("Base Map", 2D) = "white" {}
        [Toggle] _UseVertexColor ("Use Vertex Colour (RGB albedo, A tint mask)", Float) = 0
        _ShadeColor ("Shade Tint", Color) = (0.62, 0.58, 0.78, 1)
        _RampThreshold ("Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmoothness ("Ramp Smoothness", Range(0.001, 0.5)) = 0.06
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.6
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3
        _SpecularSize ("Specular Size", Range(0, 0.2)) = 0.04
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.35
        [IntRange] _PatternType ("Pattern (0 none, 1 stripes, 2 dots, 3 zigzag, 4 stars, 5 hearts, 6 checker, 7 flames)", Range(0, 7)) = 0
        _PatternColor ("Pattern Color", Color) = (1, 1, 1, 1)
        _PatternScale ("Pattern Scale", Range(0.25, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float _UseVertexColor;
            float4 _ShadeColor;
            float _RampThreshold;
            float _RampSmoothness;
            float _AmbientStrength;
            float4 _RimColor;
            float _RimPower;
            float _RimStrength;
            float _SpecularSize;
            float _SpecularStrength;
            float _PatternType;
            float4 _PatternColor;
            float _PatternScale;
        CBUFFER_END

        // Procedural cosmetic patterns in object space (no textures). Returns 0..1 coverage.
        float PatternMask(float3 positionOS, float type)
        {
            if (type < 0.5) return 0;
            float3 p = positionOS * _PatternScale;
            float u = atan2(p.x, p.z) / 6.2831853 + 0.5; // around the body
            float v = p.y;                               // up the body
            float2 cell;
            float2 local;
            if (type < 1.5) // stripes
            {
                return step(0.5, frac(v * 5.0));
            }
            if (type < 2.5) // polka dots
            {
                cell = float2(u * 14.0, v * 7.0);
                local = frac(cell) - 0.5;
                return 1.0 - smoothstep(0.24, 0.28, length(local));
            }
            if (type < 3.5) // zigzag
            {
                return step(0.5, frac(v * 5.0 + abs(frac(u * 10.0) - 0.5) * 1.2));
            }
            if (type < 4.5) // stars
            {
                cell = float2(u * 10.0, v * 5.0);
                local = (frac(cell) - 0.5) * 2.0;
                float r = length(local);
                float a = atan2(local.y, local.x);
                float star = 0.42 * (0.55 + 0.45 * cos(5.0 * a));
                return 1.0 - smoothstep(star, star + 0.06, r);
            }
            if (type < 5.5) // hearts
            {
                cell = float2(u * 10.0, v * 5.0);
                local = (frac(cell) - 0.5) * 3.0;
                local.y += 0.25;
                float h = pow(local.x * local.x + local.y * local.y - 1.0, 3.0) - local.x * local.x * local.y * local.y * local.y;
                return step(h, 0.0);
            }
            if (type < 6.5) // checker
            {
                return fmod(floor(u * 12.0) + floor(v * 6.0), 2.0);
            }
            // flames rising from the bottom
            return step(v, 0.55 + 0.18 * sin(u * 40.0 + v * 9.0) + 0.08 * sin(u * 97.0));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 color : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float3 positionOS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 vertexRgb = input.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    vertexRgb = SRGBToLinear(vertexRgb);
                #endif

                half3 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 vertexAlbedo = lerp(vertexRgb, vertexRgb * _BaseColor.rgb, input.color.a);
                half3 albedo = lerp(_BaseColor.rgb, vertexAlbedo, _UseVertexColor) * texColor;
                half tintMask = lerp(1.0h, input.color.a, _UseVertexColor);
                half pattern = PatternMask(input.positionOS, _PatternType) * tintMask;
                albedo = lerp(albedo, _PatternColor.rgb * lerp(1.0h, vertexRgb, _UseVertexColor), pattern);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half halfLambert = dot(normalWS, mainLight.direction) * 0.5h + 0.5h;
                half ramp = smoothstep(_RampThreshold - _RampSmoothness, _RampThreshold + _RampSmoothness, halfLambert);
                half shadow = smoothstep(0.35h, 0.65h, mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half lightAmount = min(ramp, shadow);

                half3 lit = albedo * mainLight.color * lerp(_ShadeColor.rgb, half3(1, 1, 1), lightAmount);
                half3 ambient = SampleSH(normalWS) * albedo * _AmbientStrength;

                half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower) * _RimStrength * (0.35h + 0.65h * lightAmount);

                half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half specEdge = 1.0h - _SpecularSize;
                half spec = smoothstep(specEdge, specEdge + 0.01h, dot(normalWS, halfDir)) * _SpecularStrength * lightAmount;

                half3 color = lit + ambient + rim * _RimColor.rgb + spec * mainLight.color;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normalWS);
                    float2 remapped = saturate(octNormal * 0.5 + 0.5);
                    return half4(PackFloat2To888(remapped), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
