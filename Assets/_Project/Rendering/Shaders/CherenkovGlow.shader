// Bagliore Cherenkov della cella nucleare: guscio unlit additivo attorno alle barre di combustibile.
// Pieno dove la superficie guarda la camera, svanisce sulla silhouette (Fresnel invertito), così si legge come
// acqua illuminata e non come un tubo. Additivo: niente ordinamento con le altre trasparenze, non scurisce.
// Nella nebbia sfuma verso il nero (non verso il colore della nebbia). _Intensity la scrive Grabbable con la velocità.
Shader "Deeplonauts/CherenkovGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.25, 0.55, 1, 1)
        _Intensity ("Intensity", Float) = 2
        _EmissionMap ("Emission Map (RGB)", 2D) = "white" {}
        _MaskMap ("Mask (A)", 2D) = "white" {}
        _FresnelPower ("Edge Falloff", Range(0.1, 8)) = 2
        _FlowSpeed ("Flow Speed", Float) = 0.6
        _FlowScale ("Flow Scale", Float) = 6
        _FlowAmount ("Flow Amount", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionMap_ST;
                half4 _Color;
                half _Intensity;
                half _FresnelPower;
                half _FlowSpeed;
                half _FlowScale;
                half _FlowAmount;
            CBUFFER_END

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
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float objectY : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float2 uv : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.objectY = input.positionOS.y;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                output.uv = TRANSFORM_TEX(input.uv, _EmissionMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = pow(saturate(abs(dot(n, v))), _FresnelPower);

                // Onde lente che salgono lungo la cella: l'acqua che si muove attorno al combustibile.
                half flow = 0.5h + 0.5h * sin(input.objectY * _FlowScale - _Time.y * _FlowSpeed * 6.2831h);
                half glow = facing * lerp(1.0h, flow, _FlowAmount) * _Intensity;

                half visibility = 1;
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    visibility = ComputeFogIntensity(input.fogFactor);
                #endif

                // Mappe opzionali (bianche di default): disegno dell'emissione e maschera in alpha.
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
                half mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv).a;

                return half4(_Color.rgb * emission * mask * glow * visibility, 1);
            }
            ENDHLSL
        }
    }
}
