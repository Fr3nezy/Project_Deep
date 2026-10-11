// Coral: URP lit shader for the procedural bamboo coral (and other sessile organisms).
// Branch mode (_CORAL_BRANCH): albedo from a height gradient in object space plus
// node bands drawn per pixel from UV1.x (the node phase written by GN_BambooFan_Skin);
// bands fade on thin twigs through vertex colour R (branch thickness).
// Polyp mode (_ALPHATEST_ON): atlas albedo with alpha clip, normal map, glow from the
// mask R channel, double sided.
// Both modes sway with the current: the offset grows with object-space height and
// every instance gets its own phase from its world position. Optional static lean along
// the current, per-element phase from UV1.x and a wave travelling along the current
// (sea pen patches, where one mesh holds many stems).
// Touch glow (_CORAL_TOUCH_GLOW): bioluminescent waves that start where the diver walked
// (points published by SeaPenTouchGlow) and run outward through the polyps (mask R),
// in two colours picked by mask G, blended toward a shared base colour down the stem (UV1.y).
// Push (_PushStrength): stems near the diver's feet bend away from them (vertex only).
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

        [Header(Touch Glow)]
        [Toggle(_CORAL_TOUCH_GLOW)] _TouchGlow ("Touch Glow (needs SeaPenTouchGlow on the player)", Float) = 0
        [HDR] _TouchGlowColor ("Touch Glow Color (mask G = 0)", Color) = (0.02, 1, 0.25, 1)
        [HDR] _TouchGlowColor2 ("Touch Glow Color 2 (mask G = 1)", Color) = (0, 0.55, 1, 1)
        [HDR] _TouchGlowBaseColor ("Touch Glow Base Color (bottom of the stems)", Color) = (0, 0.25, 1, 1)
        _TouchGradient ("Touch Gradient (0 = flat, 1 = base colour at the root)", Range(0, 1)) = 0
        _TouchBaseLevel ("Touch Glow Level at the Root", Range(0, 1)) = 0.25
        _TouchGlowStrength ("Touch Glow Strength", Range(0, 8)) = 1
        _TouchRadius ("Touch Radius (m)", Float) = 1.2
        _TouchSpeed ("Touch Wave Speed (m/s)", Float) = 1.2
        _TouchWidth ("Touch Wave Front Width (m)", Float) = 0.25
        _TouchAfterglow ("Touch Afterglow behind the Front", Range(0, 1)) = 0.3
        _TouchLifetime ("Touch Lifetime (s)", Float) = 2.5
        _TouchJitter ("Touch Delay per Stem (m, from UV1.x)", Float) = 0.3
        _TouchFlicker ("Touch Flicker Speed (rad/s)", Float) = 9

        [Header(Sway)]
        _SwayDirection ("Current Direction (world)", Vector) = (1, 0, 0, 0)
        _SwayAmount ("Sway Amount at Top (m)", Range(0, 0.3)) = 0.03
        _SwayFrequency ("Sway Frequency (Hz)", Range(0, 2)) = 0.2
        _SwayHeight ("Sway Height (m)", Float) = 1
        _SwayLean ("Lean at Top (m, along the current)", Range(0, 0.5)) = 0
        _SwayPhaseUV1 ("Phase from UV1.x (cycles)", Range(0, 1)) = 0
        _SwayWave ("Wave along the Current (rad per m)", Range(0, 10)) = 0

        [Header(Push)]
        _PushStrength ("Push Away from the Diver (m at Sway Height)", Range(0, 0.5)) = 0
        _PushRadius ("Push Radius (m)", Float) = 0.7
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
            float _SwayLean;
            float _SwayPhaseUV1;
            float _SwayWave;
            half4 _TouchGlowColor;
            half4 _TouchGlowColor2;
            half4 _TouchGlowBaseColor;
            half _TouchGradient;
            half _TouchBaseLevel;
            half _TouchGlowStrength;
            float _TouchRadius;
            float _TouchSpeed;
            float _TouchWidth;
            half _TouchAfterglow;
            float _TouchLifetime;
            float _TouchJitter;
            float _TouchFlicker;
            float _PushStrength;
            float _PushRadius;
        CBUFFER_END

        // Touch points published by SeaPenTouchGlow: xyz = world position (diver's feet),
        // w = Time.time of the touch. The array size must match SeaPenTouchGlow.MaxPoints.
        #define CORAL_TOUCH_POINTS 12
        float4 _SeaPenTouchPoints[CORAL_TOUCH_POINTS];
        float _SeaPenTouchCount;
        float _SeaPenTouchTime;
        // Diver's feet (xyz), w = 1 while SeaPenTouchGlow is active.
        float4 _SeaPenPlayer;

        // Strongest wave over the recent touch points: a bright front that spreads at
        // _TouchSpeed, a dim afterglow behind it, all fading over _TouchLifetime. UV1.x
        // (random per stem) delays the front a little and desyncs the flicker, so the light
        // jumps from stem to stem instead of drawing a perfect circle.
        half CoralTouchGlow(float3 positionWS, float stemRandom)
        {
            half glow = 0;
            int count = (int)min(_SeaPenTouchCount, CORAL_TOUCH_POINTS);
            for (int i = 0; i < count; i++)
            {
                float4 p = _SeaPenTouchPoints[i];
                float age = _SeaPenTouchTime - p.w;
                if (age < 0 || age > _TouchLifetime)
                    continue;
                float d = distance(positionWS.xz, p.xz) + stemRandom * _TouchJitter;
                float front = age * _TouchSpeed;
                float k = (d - front) / max(_TouchWidth, 1e-3);
                float wave = exp(-k * k) + saturate(-k) * _TouchAfterglow;
                float reach = 1.0 - smoothstep(_TouchRadius * 0.6, _TouchRadius, d);
                float fade = 1.0 - age / max(_TouchLifetime, 1e-3);
                float level = saturate(1.5 - abs(positionWS.y - p.y));
                glow = max(glow, wave * reach * fade * fade * level);
            }
            half flicker = 0.75h + 0.25h * sin(_SeaPenTouchTime * _TouchFlicker + stemRandom * 61.7);
            return glow * flicker * _TouchGlowStrength;
        }

        float2 CoralUV(float2 uv0)
        {
            return uv0 * _TexScale * _BaseMap_ST.xy + _BaseMap_ST.zw;
        }

        // World-space position after the current sway. Stiff base, mobile top,
        // per-instance phase from the object origin so a field does not move in unison.
        // UV1.x adds a per-element phase (_SwayPhaseUV1, 0 = off: on the bamboo coral UV1.x
        // is the node phase), _SwayWave a wave that travels along the current, and
        // _SwayLean a static lean with the current. All three default to 0.
        // _PushStrength bends the stems away from the diver's feet with the same stiff root;
        // the total horizontal offset also lowers the vertex a little (arc, not a shear).
        float3 CoralSwayWS(float3 positionOS, float2 uv1)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float h = saturate(positionOS.y / max(_SwayHeight, 1e-3));
            float w = h * h;
            float3 dir = _SwayDirection.xyz;
            dir = dot(dir, dir) > 1e-6 ? normalize(dir) : float3(0, 0, 0);
            float3 origin = GetObjectToWorldMatrix()._m03_m13_m23;
            float phase = dot(origin, float3(1.37, 0.0, 2.11));
            phase += uv1.x * _SwayPhaseUV1 * 6.2831853 - dot(positionWS.xz, dir.xz) * _SwayWave;
            float t = _Time.y * _SwayFrequency * 6.2831853;
            float s = sin(t + phase) * 0.75 + sin(t * 2.3 + phase * 1.7) * 0.25;
            float3 offset = dir * (w * (_SwayAmount * s + _SwayLean));
            float2 away = positionWS.xz - _SeaPenPlayer.xz;
            float dist = length(away);
            float push = _PushStrength * _SeaPenPlayer.w
                * (1.0 - smoothstep(_PushRadius * 0.3, _PushRadius, dist))
                * saturate(1.5 - abs(positionWS.y - _SeaPenPlayer.y));
            offset.xz += away / max(dist, 1e-3) * (push * w);
            offset.y -= dot(offset.xz, offset.xz) / (2.0 * max(positionOS.y, 0.05));
            return positionWS + offset;
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
            #pragma shader_feature_local _CORAL_TOUCH_GLOW
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

                float3 positionWS = CoralSwayWS(input.positionOS.xyz, input.uv1);
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
                #if defined(_CORAL_TOUCH_GLOW)
                    // species colour at the tip, shared base colour and lower level toward the root
                    half up = saturate(input.uv1.y);
                    half3 tip = lerp(_TouchGlowColor.rgb, _TouchGlowColor2.rgb, mask.g);
                    half3 touchColor = lerp(tip, _TouchGlowBaseColor.rgb, (1.0h - up) * _TouchGradient);
                    half touchLevel = lerp(1.0h, lerp(_TouchBaseLevel, 1.0h, up), _TouchGradient);
                    surface.emission += touchColor * (touchLevel * mask.r * CoralTouchGlow(input.positionWS, input.uv1.x));
                #endif

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
                float2 uv1 : TEXCOORD1;
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
                float3 positionWS = CoralSwayWS(input.positionOS.xyz, input.uv1);
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
                float2 uv1 : TEXCOORD1;
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
                output.positionCS = TransformWorldToHClip(CoralSwayWS(input.positionOS.xyz, input.uv1));
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
                float2 uv1 : TEXCOORD1;
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
                output.positionCS = TransformWorldToHClip(CoralSwayWS(input.positionOS.xyz, input.uv1));
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
