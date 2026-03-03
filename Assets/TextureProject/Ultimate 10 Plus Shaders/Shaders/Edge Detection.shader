/*

███████╗██████╗░░██████╗░███████╗  ██████╗░███████╗████████╗███████╗░█████╗░████████╗██╗░█████╗░███╗░░██╗
██╔════╝██╔══██╗██╔════╝░██╔════╝  ██╔══██╗██╔════╝╚══██╔══╝██╔════╝██╔══██╗╚══██╔══╝██║██╔══██╗████╗░██║
█████╗░░██║░░██║██║░░██╗░█████╗░░  ██║░░██║█████╗░░░░░██║░░░█████╗░░██║░░╚═╝░░░██║░░░██║██║░░██║██╔██╗██║
██╔══╝░░██║░░██║██║░░╚██╗██╔══╝░░  ██║░░██║██╔══╝░░░░░██║░░░██╔══╝░░██║░░██╗░░░██║░░░██║██║░░██║██║╚████║
███████╗██████╔╝╚██████╔╝███████╗  ██████╔╝███████╗░░░██║░░░███████╗╚█████╔╝░░░██║░░░██║╚█████╔╝██║░╚███║
╚══════╝╚═════╝░░╚═════╝░╚══════╝  ╚═════╝░╚══════╝░░░╚═╝░░░╚══════╝░╚════╝░░░░╚═╝░░░╚═╝░╚════╝░╚═╝░░╚══╝

                            ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
                            ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
                            ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
                            ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
                            ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
                            ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝
            
                █▀▀▄ █──█ 　 ▀▀█▀▀ █──█ █▀▀ 　 ░█▀▀▄ █▀▀ ▀█─█▀ █▀▀ █── █▀▀█ █▀▀█ █▀▀ █▀▀█ 
                █▀▀▄ █▄▄█ 　 ─░█── █▀▀█ █▀▀ 　 ░█─░█ █▀▀ ─█▄█─ █▀▀ █── █──█ █──█ █▀▀ █▄▄▀ 
                ▀▀▀─ ▄▄▄█ 　 ─░█── ▀──▀ ▀▀▀ 　 ░█▄▄▀ ▀▀▀ ──▀── ▀▀▀ ▀▀▀ ▀▀▀▀ █▀▀▀ ▀▀▀ ▀─▀▀
____________________________________________________________________________________________________________________________________________

        ▄▀█ █▀ █▀ █▀▀ ▀█▀ ▀   █░█ █░░ ▀█▀ █ █▀▄▀█ ▄▀█ ▀█▀ █▀▀   ▄█ █▀█ ▄█▄   █▀ █░█ ▄▀█ █▀▄ █▀▀ █▀█ █▀
        █▀█ ▄█ ▄█ ██▄ ░█░ ▄   █▄█ █▄▄ ░█░ █ █░▀░█ █▀█ ░█░ ██▄   ░█ █▄█ ░▀░   ▄█ █▀█ █▀█ █▄▀ ██▄ █▀▄ ▄█
____________________________________________________________________________________________________________________________________________
License:
    The license is ATTRIBUTION 3.0

    More license info here:
        https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
This shader has NOT been tested on any other PC configuration except the following:
    CPU: Intel Core i5-6400
    GPU: NVidia GTX 750Ti
    RAM: 16GB
    Windows: 10 x64
    DirectX: 11
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Edge Detection" /* The edge detection algorithm that is implemented in this shader is named "Sobel Edge Detection" */
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };    

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            half4 _Color;
            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            v2f vert(appdata input)
            {
                v2f output;

                output.position = TransformObjectToHClip(input.vertex.xyz);
                output.screenPos = output.position;

                return output;
            }

            half4 pixel;
            float2 uv;
            float onePixelW, onePixelH;
            half4 frag(v2f input) : SV_Target
            {    
                uv = input.screenPos.xy / input.screenPos.w;
                uv.x = (uv.x + 1) * .5;
                uv.y = 1.0 - (uv.y + 1) * .5;

                onePixelW = 1.0 / _ScreenParams.x;
                onePixelH = 1.0 / _ScreenParams.y;

                pixel = 0;
                pixel = abs(
                    SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - onePixelW, uv.y)) - 
                    SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + onePixelW, uv.y)) + 
                    SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x, uv.y + onePixelH)) -
                    SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x, uv.y - onePixelH))
                    );

                return pixel * _Color;
            }    
            ENDHLSL
        }
    }
}
