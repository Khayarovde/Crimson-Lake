Shader "Shader Graphs/RDE"
{
    Properties
    {
        _ditherspread    ("Dither Spread",            Float) = 1.0
        _colorresolution ("Color Resolution (steps)", Float) = 8.0

        [HideInInspector][NoScaleOffset] unity_Lightmaps    ("unity_Lightmaps",    2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks  ("unity_ShadowMasks",  2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"      = "UniversalPipeline"
            "ShaderGraphShader"   = "true"
            "ShaderGraphTargetId" = "UniversalFullscreenSubTarget"
        }

        // ─────────────────────────────────────────────────────────────
        // Pass 1 — DrawProcedural
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DrawProcedural"
            ZTest Always  ZWrite Off  Cull Off  Blend Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   4.5

            // Blit.hlsl уже объявляет:
            //   TEXTURE2D_X(_BlitTexture)
            //   Attributes / Varyings structs
            //   Varyings Vert(Attributes input)   <-- НЕ переопределяем!
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Говорим Blit.hlsl НЕ генерировать свой Vert —
            // он нам подходит как есть, просто включаем его целиком.
            // В Unity 6 (URP 17) Vert уже экспортируется из хедера,
            // поэтому pragma vertex Vert выше найдёт его автоматически.
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ditherspread;
                float _colorresolution;
            CBUFFER_END

            // Bayer 4×4 (канонический упорядоченный паттерн)
            static const float Bayer4x4[16] =
            {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 texelPos = input.texcoord * _ScreenSize.xy;

                // Point-load без фильтрации
                float4 color = LOAD_TEXTURE2D_X(_BlitTexture, (uint2)texelPos);

                // Bayer-индекс по пиксельным координатам
                uint2  px     = (uint2)texelPos % 4u;
                float  dither = Bayer4x4[px.y * 4u + px.x] - 0.5; // [-0.5, +0.5]

                // Квантизация с round() — не floor()
                float  steps  = max(_colorresolution, 2.0);
                float3 shifted = color.rgb + dither * _ditherspread / steps;
                float3 result  = round(shifted * steps) / steps;

                return float4(saturate(result), color.a);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // Pass 2 — Blit (SRP fallback)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "Blit"
            ZTest Always  ZWrite Off  Cull Off  Blend Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ditherspread;
                float _colorresolution;
            CBUFFER_END

            static const float Bayer4x4[16] =
            {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 texelPos = input.texcoord * _ScreenSize.xy;
                float4 color    = LOAD_TEXTURE2D_X(_BlitTexture, (uint2)texelPos);

                uint2  px     = (uint2)texelPos % 4u;
                float  dither = Bayer4x4[px.y * 4u + px.x] - 0.5;

                float  steps  = max(_colorresolution, 2.0);
                float3 shifted = color.rgb + dither * _ditherspread / steps;
                float3 result  = round(shifted * steps) / steps;

                return float4(saturate(result), color.a);
            }
            ENDHLSL
        }
    }

    CustomEditor "UnityEditor.Rendering.Fullscreen.ShaderGraph.FullscreenShaderGUI"
    Fallback "Hidden/Shader Graph/FallbackError"
}
