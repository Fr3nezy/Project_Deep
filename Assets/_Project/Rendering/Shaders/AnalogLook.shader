// Look "analogico" a schermo intero: pixel grossi, posterizzazione con dither Bayer 4x4, scanline.
// Usato da Deeploration.Rendering.AnalogLookFeature dopo il post-processing (colore già in LDR).
Shader "Hidden/Deeplonauts/AnalogLook"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "AnalogLook"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _AnalogParams;  // x: intensity, y: pixel size (px), z: colour levels, w: dither
            float4 _AnalogParams2; // x: scanlines

            static const float Bayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // pixel virtuali: campiona il centro della cella
                float px = max(_AnalogParams.y, 1.0);
                float2 cell = floor(uv * _ScreenParams.xy / px);
                float2 uvCell = (cell + 0.5) * px / _ScreenParams.xy;
                half3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uvCell).rgb;

                // posterizzazione in spazio percettivo con dither ordinato per cella
                uint2 b = (uint2)cell % 4;
                float threshold = (Bayer4[b.y * 4 + b.x] + 0.5) / 16.0 - 0.5;
                float levels = max(_AnalogParams.z - 1.0, 1.0);
                float3 g = pow(saturate(col), 1.0 / 2.2);
                g = floor(g * levels + 0.5 + threshold * _AnalogParams.w) / levels;
                col = pow(saturate(g), 2.2);

                // scanline: una riga scura ogni due righe di pixel virtuali
                float scan = frac(input.positionCS.y / (px * 2.0));
                col *= 1.0 - _AnalogParams2.x * step(0.5, scan);

                return half4(lerp(original, col, _AnalogParams.x), 1.0);
            }
            ENDHLSL
        }
    }
}
