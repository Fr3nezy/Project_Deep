// Coral: URP lit shader for the procedural bamboo coral (and other sessile organisms).
// Branch mode (_CORAL_BRANCH): albedo from a height gradient in object space plus
// node bands drawn per pixel from UV1.x (the node phase written by GN_BambooFan_Skin);
// bands fade on thin twigs through vertex colour R (branch thickness).
// Polyp mode (_ALPHATEST_ON): atlas albedo with alpha clip, normal map, glow from the
// mask R channel, double sided.
// Both modes sway with the current: the offset grows with object-space height and
// every instance gets its own phase from its world position.
Shader "Deeplonauts/Coral"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _MaskMap ("Mask (R glow)", 2D) = "black" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
        _TexScale ("Texture Tiling (UV0 multiplier)", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Alpha)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Branch)]
        [Toggle(_CORAL_BRANCH)] _Branch ("Branch Mode", Float) = 0
        _BottomColor ("Bottom Color", Color) = (0.70, 0.64, 0.50, 1)
        _TopColor ("Top Color", Color) = (0.92, 0.90, 0.84, 1)
        _GradientHeight ("Gradient Height (m)", Float) = 1
        _NodeColor ("Node Color", Color) = (0.16, 0.09, 0.08, 1)
        _NodeWidth ("Node Width (phase)", Range(0, 0.5)) = 0.05
        _NodeSoftness ("Node Softness (phase)", Range(0.001, 0.2)) = 0.02
        _NodeFadeMin ("Node Fade Min (vertex R)", Range(0, 1)) = 0.25
        _NodeFadeMax ("Node Fade Max (vertex R)", Range(0, 1)) = 0.58

        [Header(Glow)]
        [HDR] _GlowColor ("Glow Color", Color) = (0.25, 0.85, 1, 1)
        _GlowStrength ("Glow Strength", Range(0, 4)) = 0.15
        [HDR] _LatticeGlowColor ("Lattice Glow Color (mask B)", Color) = (0.55, 0.85, 1, 1)
        _LatticeGlow ("Lattice Glow Base", Range(0, 2)) = 0
        _PulseStrength ("Lattice Pulse Strength", Range(0, 4)) = 0
        _PulseSpeed ("Pulse Speed (UV0.y per s)", Float) = 0.3
        _PulseSpacing ("Pulse Spacing (UV0.y)", Float) = 3
        _PulseTail ("Pulse Tail (fraction of spacing)", Range(0.01, 1)) = 0.15

        [Header(Rim and Back Faces)]
        _RimColor ("Rim Color", Color) = (0.85, 0.95, 1, 1)
        _RimStrength ("Rim Strength (x lit light)", Range(0, 4)) = 0
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _BackfaceTint ("Back Face Tint", Color) = (1, 1, 1, 1)

        [Header(Translucency)]
        [Toggle(_CORAL_TRANSLUCENT)] _Translucent ("Translucent (Transparent queue, needs Opaque Texture)", Float) = 0
        _OpacityFacing ("Opacity Facing", Range(0, 1)) = 0.55
        _OpacityGrazing ("Opacity Grazing", Range(0, 1)) = 0.95
        _HoleTransmission ("Hole Transmission (mask G)", Range(0, 1)) = 0.6
        _TransmissionTint ("Transmission Tint", Color) = (0.85, 0.92, 1, 1)
        _Refraction ("Refraction (screen UV)", Range(0, 0.1)) = 0.02
        _InteriorFill ("Interior Fill (fake back wall)", Range(0, 1)) = 0.55

        [Header(Sway)]
        _SwayDirection ("Current Direction (world)", Vector) = (1, 0, 0, 0)
        _SwayAmount ("Sway Amount at Top (m)", Range(0, 0.3)) = 0.03
        _SwayFrequency ("Sway Frequency (Hz)", Range(0, 2)) = 0.2
        _SwayHeight ("Sway Height (m)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Smoothness;
            float _TexScale;
            half _Cutoff;
            half4 _BottomColor;
            half4 _TopColor;
            float _GradientHeight;
            half4 _NodeColor;
            half _NodeWidth;
            half _NodeSoftness;
            half _NodeFadeMin;
            half _NodeFadeMax;
            half4 _GlowColor;
            half _GlowStrength;
            half4 _LatticeGlowColor;
            half _LatticeGlow;
            half _PulseStrength;
            float _PulseSpeed;
            float _PulseSpacing;
            half _PulseTail;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half4 _BackfaceTint;
            half _OpacityFacing;
            half _OpacityGrazing;
            half _HoleTransmission;
            half4 _TransmissionTint;
            half _Refraction;
            half _InteriorFill;
            float4 _SwayDirection;
            float _SwayAmount;
            float _SwayFrequency;
            float _SwayHeight;
        CBUFFER_END

        float2 CoralUV(float2 uv0)
        {
            return uv0 * _TexScale * _BaseMap_ST.xy + _BaseMap_ST.zw;
        }

        // World-space position after the current sway. Stiff base, mobile top,
        // per-instance phase from the object origin so a field does not move in unison.
        float3 CoralSwayWS(float3 positionOS)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float h = saturate(positionOS.y / max(_SwayHeight, 1e-3));
            float w = h * h;
            float3 origin = GetObjectToWorldMatrix()._m03_m13_m23;
            float phase = dot(origin, float3(1.37, 0.0, 2.11));
            float t = _Time.y * _SwayFrequency * 6.2831853;
            float s = sin(t + phase) * 0.75 + sin(t * 2.3 + phase * 1.7) * 0.25;
            float3 dir = _SwayDirection.xyz;
            dir = dot(dir, dir) > 1e-6 ? normalize(dir) : float3(0, 0, 0);
            return positionWS + dir * (_SwayAmount * w * s);
        }

        void CoralClip(float2 uv0)
        {
            #if defined(_ALPHATEST_ON)
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, CoralUV(uv0)).a * _BaseColor.a;
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

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _CORAL_BRANCH
            #pragma shader_feature_local _CORAL_TRANSLUCENT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif
            #if defined(_CORAL_TRANSLUCENT)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv0 : TEXCOORD3;
                float2 uv1 : TEXCOORD4;
                float heightOS : TEXCOORD5;
                half thickness : TEXCOORD6;
                float fogFactor : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = CoralSwayWS(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = n.normalWS;
                output.tangentWS = float4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.heightOS = input.positionOS.y;
                output.thickness = input.color.r;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif

                float2 uv = CoralUV(input.uv0);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                #if defined(_ALPHATEST_ON)
                    clip(baseSample.a * _BaseColor.a - _Cutoff);
                #endif

                half3 albedo = baseSample.rgb * _BaseColor.rgb;
                #if defined(_CORAL_BRANCH)
                    half g = saturate(input.heightOS / max(_GradientHeight, 1e-3));
                    albedo *= lerp(_BottomColor.rgb, _TopColor.rgb, g);
                    float f = frac(input.uv1.x);
                    float d = min(f, 1.0 - f);
                    half band = 1.0h - smoothstep(_NodeWidth, _NodeWidth + _NodeSoftness, d);
                    band *= smoothstep(_NodeFadeMin, _NodeFadeMax, input.thickness);
                    albedo = lerp(albedo, _NodeColor.rgb, band);
                #endif

                // tangent-space normal, flipped on back faces (double sided polyps, sponge interiors)
                half faceSign = IS_FRONT_VFACE(face, 1.0h, -1.0h);
                albedo *= faceSign > 0 ? half3(1, 1, 1) : _BackfaceTint.rgb;
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
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;
                surface.normalTS = normalTS;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
                surface.emission = _GlowColor.rgb * (mask.r * _GlowStrength);

                // Lattice glow (mask B): faint light along the silica bars plus a pulse that
                // climbs each tube along UV0.y, sharp front and fading tail. Per-instance phase
                // from the object origin, per-tube phase from UV1.x (random per tube, baked by
                // GN_GlassSponge_Skin; on the coral UV1.x is the node phase, harmless there
                // because the pulse is off).
                float3 origin = GetObjectToWorldMatrix()._m03_m13_m23;
                float instPhase = frac(dot(origin, float3(0.131, 0.0, 0.173)));
                float spacing = max(_PulseSpacing, 1e-3);
                float pulsePhase = frac(_Time.y * _PulseSpeed / spacing - input.uv0.y / spacing + instPhase + input.uv1.x);
                half pulse = exp(-pulsePhase / max(_PulseTail, 1e-3));
                surface.emission += _LatticeGlowColor.rgb * (mask.b * (_LatticeGlow + _PulseStrength * pulse));

                half4 color = UniversalFragmentPBR(inputData, surface);
                // Rim: bright fringe at grazing angles, scaled by how lit the pixel already is,
                // so it reads as the spicule halo under the torch and stays dark in the dark.
                half ndv = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half rim = pow(1.0h - ndv, _RimPower) * _RimStrength;
                half litLevel = dot(color.rgb - surface.emission, half3(0.3333h, 0.3333h, 0.3333h));
                color.rgb += _RimColor.rgb * (rim * max(litLevel, 0.0h));
                color.rgb = MixFog(color.rgb, input.fogFactor);

                #if defined(_CORAL_TRANSLUCENT)
                    // Thin translucent wall: the scene behind shows through, more where the wall is
                    // seen face on and through the lattice holes, less at grazing angles. The scene
                    // colour already carries its own fog, so it is blended after MixFog.
                    half opacity = lerp(_OpacityFacing, _OpacityGrazing, (1.0h - ndv) * (1.0h - ndv));
                    opacity *= 1.0h - mask.g * _HoleTransmission;
                    float2 sceneUV = inputData.normalizedScreenSpaceUV + normalTS.xy * _Refraction;
                    half3 behind = SampleSceneColor(sceneUV) * _TransmissionTint.rgb;
                    // The opaque texture does not contain this object, so through a hole we would
                    // see the floor instead of the tube's own back wall: fake that wall with the
                    // lit colour darkened by the back face tint.
                    behind = lerp(behind, color.rgb * _BackfaceTint.rgb, _InteriorFill);
                    color.rgb = lerp(behind, color.rgb, saturate(opacity));
                #endif
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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0 : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 positionWS = CoralSwayWS(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                output.uv0 = input.uv0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                CoralClip(input.uv0);
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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0 : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformWorldToHClip(CoralSwayWS(input.positionOS.xyz));
                output.uv0 = input.uv0;
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                CoralClip(input.uv0);
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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv0 : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformWorldToHClip(CoralSwayWS(input.positionOS.xyz));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv0 = input.uv0;
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                CoralClip(input.uv0);
                float3 n = input.normalWS * IS_FRONT_VFACE(face, 1.0, -1.0);
                return half4(NormalizeNormalPerPixel(n), 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
