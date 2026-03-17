/*
██████╗░███████╗██████╗░████████╗██╗░░██╗  ███╗░░░███╗░█████╗░░██████╗██╗░░██╗  ███████╗██████╗░░██████╗░███████╗
██╔══██╗██╔════╝██╔══██╗╚══██╔══╝██║░░██║  ████╗░████║██╔══██╗██╔════╝██║░██╔╝  ██╔════╝██╔══██╗██╔════╝░██╔════╝
██║░░██║█████╗░░██████╔╝░░░██║░░░███████║  ██╔████╔██║███████║╚█████╗░█████═╝░  █████╗░░██║░░██║██║░░██╗░█████╗░░
██║░░██║██╔══╝░░██╔═══╝░░░░██║░░░██╔══██║  ██║╚██╔╝██║██╔══██║░╚═══██╗██╔═██╗░  ██╔══╝░░██║░░██║██║░░╚██╗██╔══╝░░
██████╔╝███████╗██║░░░░░░░░██║░░░██║░░██║  ██║░╚═╝░██║██║░░██║██████╔╝██║░╚██╗  ███████╗██████╔╝╚██████╔╝███████╗
╚═════╝░╚══════╝╚═╝░░░░░░░░╚═╝░░░╚═╝░░╚═╝  ╚═╝░░░░░╚═╝╚═╝░░╚═╝╚═════╝░╚═╝░░╚═╝  ╚══════╝╚═════╝░░╚═════╝░╚══════╝

                    ██████╗░███████╗████████╗███████╗░█████╗░████████╗██╗░█████╗░███╗░░██╗
                    ██╔══██╗██╔════╝╚══██╔══╝██╔════╝██╔══██╗╚══██╔══╝██║██╔══██╗████╗░██║
                    ██║░░██║█████╗░░░░░██║░░░█████╗░░██║░░╚═╝░░░██║░░░██║██║░░██║██╔██╗██║
                    ██║░░██║██╔══╝░░░░░██║░░░██╔══╝░░██║░░██╗░░░██║░░░██║██║░░██║██║╚████║
                    ██████╔╝███████╗░░░██║░░░███████╗╚█████╔╝░░░██║░░░██║╚█████╔╝██║░╚███║
                    ╚═════╝░╚══════╝░░░╚═╝░░░╚══════╝░╚════╝░░░░╚═╝░░░╚═╝░╚════╝░╚═╝░░╚══╝

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

Shader "Ultimate 10+ Shaders/Depth Mask Edge Detection"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Back
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                uv.y = (uv.y + 1) * .5;

                onePixelW = 1.0 / _ScreenParams.x;
                onePixelH = 1.0 / _ScreenParams.y;

                pixel = Linear01Depth(abs(
                        SampleSceneDepth(float2(uv.x - onePixelW, uv.y)) - 
                        SampleSceneDepth(float2(uv.x + onePixelW, uv.y)) + 
                        SampleSceneDepth(float2(uv.x, uv.y + onePixelH)) -
                        SampleSceneDepth(float2(uv.x, uv.y - onePixelH))
                    ), _ZBufferParams);

                return pixel * _Color;
            }    
            ENDHLSL
        }
    }
}
