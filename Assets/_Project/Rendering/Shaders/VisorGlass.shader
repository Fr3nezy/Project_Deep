// Vetro del visore del casco, visto da dentro: unlit additivo che schiarisce solo dove il vetro è di taglio
// (Fresnel), così il centro resta libero e non toglie visibilità. Additivo: niente ordinamento, non scurisce mai.
// Cull Front perché la camera sta dentro il guscio e le normali guardano fuori. La mesh non ha UV: niente mappe.
Shader "Deeplonauts/VisorGlass"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.55, 0.6, 0.65, 1)
        _Intensity ("Intensity", Float) = 0.35
        _RimPower ("Edge Falloff", Range(0.5, 8)) = 3
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
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
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
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0h - saturate(abs(dot(n, v))), _RimPower);
                return half4(_Color.rgb * rim * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
