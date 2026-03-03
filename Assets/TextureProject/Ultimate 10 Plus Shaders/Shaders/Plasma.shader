/*
    ██████╗░██╗░░░░░░█████╗░░██████╗███╗░░░███╗░█████╗░  ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
    ██╔══██╗██║░░░░░██╔══██╗██╔════╝████╗░████║██╔══██╗  ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
    ██████╔╝██║░░░░░███████║╚█████╗░██╔████╔██║███████║  ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
    ██╔═══╝░██║░░░░░██╔══██║░╚═══██╗██║╚██╔╝██║██╔══██║  ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
    ██║░░░░░███████╗██║░░██║██████╔╝██║░╚═╝░██║██║░░██║  ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
    ╚═╝░░░░░╚══════╝╚═╝░░╚═╝╚═════╝░╚═╝░░░░░╚═╝╚═╝░░╚═╝  ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

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

Shader "Ultimate 10+ Shaders/Plasma"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Normal("Normal map", 2D) = "bump" {}

        _NoiseTex ("Noise", 2D) = "white" {}
        _MovementDirection ("Movement Direction", float) = (0, -1, 0, 1)
        
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags{ "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
        LOD 100

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half2 _MovementDirection;
                float4 _MainTex_ST;
                float4 _Normal_ST;
                float4 _NoiseTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Normal);
            SAMPLER(sampler_Normal);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uvMain : TEXCOORD3;
                float2 uvNormal : TEXCOORD4;
                float2 uvNoise : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half2 move = _MovementDirection * _Time.y;

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(nrm.normalWS);
                output.tangentWS = float4(normalize(nrm.tangentWS), input.tangentOS.w);
                output.uvMain = TRANSFORM_TEX(input.uv, _MainTex) + move;
                output.uvNormal = TRANSFORM_TEX(input.uv, _Normal) + (move * 0.5h);
                output.uvNoise = TRANSFORM_TEX(input.uv, _NoiseTex) + (move * 0.5h);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alphaNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uvNoise).r;
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvMain) * _Color;
                half3 albedo = mainTex.rgb * alphaNoise;

                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, input.uvNormal));

                float3 bitangentWS = normalize(cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w);
                float3x3 tbn = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                half3 normalWS = normalize(mul(normalTS, tbn));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(normalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = alphaNoise;
                surfaceData.metallic = 0.0h;
                surfaceData.specular = half3(0,0,0);
                surfaceData.smoothness = 0.6h;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1.0h;
                surfaceData.emission = half3(0,0,0);
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}
