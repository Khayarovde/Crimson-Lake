/*
        ░██████╗███╗░░██╗░█████╗░░██╗░░░░░░░██╗  ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
        ██╔════╝████╗░██║██╔══██╗░██║░░██╗░░██║  ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
        ╚█████╗░██╔██╗██║██║░░██║░╚██╗████╗██╔╝  ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
        ░╚═══██╗██║╚████║██║░░██║░░████╔═████║░  ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
        ██████╔╝██║░╚███║╚█████╔╝░░╚██╔╝░╚██╔╝░  ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
        ╚═════╝░╚═╝░░╚══╝░╚════╝░░░░╚═╝░░░╚═╝░░  ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

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

Shader "Ultimate 10+ Shaders/Snow"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Normal ("Normal Map", 2D) = "bump" {}

        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _SnowColor ("Snow Color", Color) = (1,1,1,1)
        _SnowNormal ("Snow Normal Map", 2D) = "bump" {}

        _SnowGlossiness ("Snow Smoothness", Range(0,1)) = 0.5
        _SnowMetallic ("Snow Metallic", Range(0,1)) = 0.0

        _SnowDirection ("Snow Direction", Vector) = (0, 1, 0, 1)
        _SnowAmount ("Snow Amount", Range(0, 1)) = 0.75

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 150
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

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
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Glossiness;
                half _Metallic;
                half4 _SnowColor;
                half _SnowGlossiness;
                half _SnowMetallic;
                half3 _SnowDirection;
                half _SnowAmount;
                float4 _MainTex_ST;
                float4 _Normal_ST;
                float4 _SnowNormal_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Normal);
            SAMPLER(sampler_Normal);
            TEXTURE2D(_SnowNormal);
            SAMPLER(sampler_SnowNormal);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv0 : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uvMain : TEXCOORD3;
                float2 uvNormal : TEXCOORD4;
                float2 uvSnowNormal : TEXCOORD5;
                half snowMask : TEXCOORD6;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.tangentWS = float4(normalize(normalInputs.tangentWS), input.tangentOS.w);

                output.uvMain = TRANSFORM_TEX(input.uv0, _MainTex);
                output.uvNormal = TRANSFORM_TEX(input.uv0, _Normal);
                output.uvSnowNormal = TRANSFORM_TEX(input.uv0, _SnowNormal);

                half dotValue = saturate(dot(output.normalWS, normalize(_SnowDirection)));
                output.snowMask = (dotValue < (1.0h - _SnowAmount)) ? 0.0h : dotValue;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 baseAlbedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvMain).rgb * _Color.rgb;
                half3 albedo = lerp(baseAlbedo, _SnowColor.rgb, input.snowMask);

                half3 baseNormalTS = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, input.uvNormal));
                half3 snowNormalTS = UnpackNormal(SAMPLE_TEXTURE2D(_SnowNormal, sampler_SnowNormal, input.uvSnowNormal));
                half3 normalTS = normalize(lerp(baseNormalTS, snowNormalTS, input.snowMask));

                float tangentSign = input.tangentWS.w;
                float3 bitangentWS = normalize(cross(input.normalWS, input.tangentWS.xyz) * tangentSign);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                half3 normalWS = normalize(mul(normalTS, tangentToWorld));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = 0;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = 1.0h;
                surfaceData.metallic = lerp(_Metallic, _SnowMetallic, input.snowMask);
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.smoothness = lerp(_Glossiness, _SnowGlossiness, input.snowMask);
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1.0h;
                surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
