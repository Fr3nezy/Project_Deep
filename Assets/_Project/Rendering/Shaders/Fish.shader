// Fish: URP lit shader for the ambient fish (lanternfish, hatchetfish), swimming animated in the
// vertex shader instead of bones. The mesh must lie along +Z (head toward +Z, pivot near the centre).
// Startle: _GlowBoost (per instance) flares the photophores when the fish bolts.
// Swim: a lateral wave (object X) that travels from head to tail, amplitude growing toward the tail,
// plus a static bend for turning. Phase, amount and bend come per instance from the Creature component
// (MaterialPropertyBlock, GPU instancing), so every fish beats its tail at its own speed.
// Surface: Substance Painter "Unity URP (Metallic)" maps: albedo with alpha (fin membranes are clipped,
// optionally dithered), normal, metallic in R with smoothness in A, emission (photophores).
Shader "Deeplonauts/Fish"
{
    Properties
    {
        _BaseMap ("Albedo (A = fin alpha)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic Scale", Range(0, 1)) = 1
        _Smoothness ("Smoothness Scale", Range(0, 1)) = 1
        _EmissionMap ("Emission", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0

        [Header(Alpha)]
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.35
        [Toggle(_FISH_DITHER)] _Dither ("Dithered Alpha (soft fins)", Float) = 1

        [Header(Swim)]
        _HeadZ ("Head Z (object space, m)", Float) = 0.155
        _BodyLength ("Body Length (m)", Float) = 0.31
        _SwimAmplitude ("Tail Amplitude (m)", Range(0, 0.2)) = 0.035
        _HeadAmplitude ("Head Amplitude (fraction of tail)", Range(0, 1)) = 0.12
        _SwimWaves ("Waves along the Body", Range(0, 2)) = 0.8
        _BendAmount ("Bend at the Tail (m per unit of bend)", Range(0, 0.3)) = 0.08
        _SwimPhase ("Swim Phase (rad, per instance)", Float) = 0
        _SwimAmount ("Swim Amount (per instance)", Float) = 1
        _SwimBend ("Swim Bend (per instance, -1.6..1.6)", Float) = 0
        _GlowBoost ("Glow Boost (per instance, startle flash)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Metallic;
            half _Smoothness;
            half4 _EmissionColor;
            half _Cutoff;
            float _HeadZ;
            float _BodyLength;
            float _SwimAmplitude;
            float _HeadAmplitude;
            float _SwimWaves;
            float _BendAmount;
        CBUFFER_END

        // Per-instance swim state written by Creature through a MaterialPropertyBlock.
        UNITY_INSTANCING_BUFFER_START(FishProps)
            UNITY_DEFINE_INSTANCED_PROP(float, _SwimPhase)
            UNITY_DEFINE_INSTANCED_PROP(float, _SwimAmount)
            UNITY_DEFINE_INSTANCED_PROP(float, _SwimBend)
            UNITY_DEFINE_INSTANCED_PROP(float, _GlowBoost)
        UNITY_INSTANCING_BUFFER_END(FishProps)

        // Lateral swim offset along object X and its slope along Z (for the normals).
        // u = 0 at the head, 1 at the tail; the wave runs toward the tail as the phase grows.
        void FishSwim(inout float3 positionOS, inout float3 normalOS)
        {
            float phase = UNITY_ACCESS_INSTANCED_PROP(FishProps, _SwimPhase);
            float amount = UNITY_ACCESS_INSTANCED_PROP(FishProps, _SwimAmount);
            float bend = UNITY_ACCESS_INSTANCED_PROP(FishProps, _SwimBend);

            float bodyLength = max(_BodyLength, 1e-3);
            float u = saturate((_HeadZ - positionOS.z) / bodyLength);
            float k = _SwimWaves * 6.2831853;
            float envelope = lerp(_HeadAmplitude, 1.0, u * u);
            float envelopeDu = 2.0 * u * (1.0 - _HeadAmplitude);
            float s = sin(phase - u * k);
            float c = cos(phase - u * k);

            float offset = _SwimAmplitude * amount * envelope * s + _BendAmount * bend * u * u;
            float dOffsetDu = _SwimAmplitude * amount * (envelopeDu * s - envelope * k * c) + _BendAmount * bend * 2.0 * u;
            // du/dz = -1 / length
            float slope = -dOffsetDu / bodyLength;

            positionOS.x += offset;
            // (x, y, z) -> (x + f(z), y, z): normals transform by the inverse transpose.
            normalOS = normalize(float3(normalOS.x, normalOS.y, normalOS.z - slope * normalOS.x));
        }

        float2 FishUV(float2 uv)
        {
            return uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
        }

        // Clip the fin membranes. With _FISH_DITHER the cutoff is spread over a 4x4 Bayer pattern
        // between 0 and 2 x _Cutoff, so soft alpha reads as a screen-door gradient (it matches the
        // dither of the analog finish) instead of a hard edge.
        void FishClip(float2 uv, float4 positionCS)
        {
            half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, FishUV(uv)).a * _BaseColor.a;
            #if defined(_FISH_DITHER)
                uint2 p = uint2(positionCS.xy) & 3;
                const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                float threshold = (bayer[p.y * 4 + p.x] + 0.5) / 16.0 * 2.0 * _Cutoff;
                clip(a - threshold);
            #else
                clip(a - _Cutoff);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _FISH_DITHER
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                FishSwim(positionOS, normalOS);
                VertexPositionInputs p = GetVertexPositionInputs(positionOS);
                VertexNormalInputs n = GetVertexNormalInputs(normalOS, input.tangentOS);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = n.normalWS;
                output.tangentWS = float4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                FishClip(input.uv, input.positionCS);

                float2 uv = FishUV(input.uv);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half4 mg = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);

                // double sided fins: flip the normal on back faces
                half faceSign = IS_FRONT_VFACE(face, 1.0h, -1.0h);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                float3 normalWS = input.normalWS * faceSign;
                float3 tangentWS = input.tangentWS.xyz;
                float3 bitangentWS = input.tangentWS.w * cross(input.normalWS, tangentWS);
                normalWS = TransformTangentToWorld(normalTS, half3x3(tangentWS, bitangentWS, normalWS));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = baseSample.rgb * _BaseColor.rgb;
                surface.metallic = mg.r * _Metallic;
                surface.smoothness = mg.a * _Smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;
                surface.normalTS = normalTS;
                // startle flash: the photophores flare up to (1 + _GlowBoost) times, driven by Creature
                half glowBoost = UNITY_ACCESS_INSTANCED_PROP(FishProps, _GlowBoost);
                surface.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb
                    * (1.0h + glowBoost);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return half4(color.rgb, 1);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _FISH_DITHER
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                FishSwim(positionOS, normalOS);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FishClip(input.uv, input.positionCS);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _FISH_DITHER
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                FishSwim(positionOS, normalOS);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = input.uv;
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FishClip(input.uv, input.positionCS);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _FISH_DITHER
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                FishSwim(positionOS, normalOS);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FishClip(input.uv, input.positionCS);
                float3 n = input.normalWS * IS_FRONT_VFACE(face, 1.0, -1.0);
                return half4(NormalizeNormalPerPixel(n), 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
