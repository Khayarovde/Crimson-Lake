Shader "Custom/BloodPulse"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _PulseColor("Pulse Tint", Color) = (1, 0.2, 0.2, 1)
        _PulseSpeed("Pulse Speed", Float) = 2
        _PulseStrength("Pulse Strength", Range(0, 1)) = 0.4
        _PulseEmission("Pulse Emission", Range(0, 5)) = 1
        _PulsePhase("Pulse Phase", Float) = 0
        _FleshWarpStrength("Flesh Warp Strength", Range(0, 0.1)) = 0.02
        _FleshWarpSpeed("Flesh Warp Speed", Float) = 1.5
        _FleshWarpScale("Flesh Warp Scale", Float) = 6
        _FleshWarpStrength2("Flesh Warp Strength 2", Range(0, 0.1)) = 0.01
        _FleshWarpSpeed2("Flesh Warp Speed 2", Float) = 2.2
        _FleshWarpScale2("Flesh Warp Scale 2", Float) = 11
        _FleshWarpMaskStrength("Flesh Warp Mask Strength", Range(0, 1)) = 0.6
        _ObjectScale("Object Scale", Float) = 1
        _RandomUVOffsetStrength("Random UV Offset Strength", Range(0,0.5)) = 0.08
        _RandomUVRotationStrength("Random UV Rotation Strength", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _PulseColor;
            float _PulseSpeed;
            float _PulseStrength;
            float _PulseEmission;
            float _PulsePhase;
            float _FleshWarpStrength;
            float _FleshWarpSpeed;
            float _FleshWarpScale;
            float _FleshWarpStrength2;
            float _FleshWarpSpeed2;
            float _FleshWarpScale2;
            float _FleshWarpMaskStrength;
            float _ObjectScale;
            float _RandomUVOffsetStrength;
            float _RandomUVRotationStrength;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Scale geometry in object space around pivot
                float3 scaledPos = input.positionOS * _ObjectScale;
                output.positionCS = TransformObjectToHClip(scaledPos);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Per-object random UV offset & rotation based on object world origin
                float3 objOriginWS = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
                float seed = frac(sin(dot(objOriginWS, float3(12.9898,78.233,37.719))) * 43758.5453);

                float2 randOffset = float2(
                    frac(sin(seed * 12.9898) * 43758.5453) - 0.5,
                    frac(sin(seed * 78.233) * 43758.5453) - 0.5
                ) * _RandomUVOffsetStrength;
                float rotSeed = frac(sin(seed * 37.719) * 43758.5453);
                float rotAngle = (rotSeed - 0.5) * 3.14159 * _RandomUVRotationStrength; // [-pi/2,pi/2]*strength

                float2 warpUV = input.uv + randOffset;
                float2 uvCentered = warpUV - 0.5;
                float ca = cos(rotAngle);
                float sa = sin(rotAngle);
                warpUV = float2(uvCentered.x * ca - uvCentered.y * sa, uvCentered.x * sa + uvCentered.y * ca) + 0.5;

                float warpTime = _Time.y * _FleshWarpSpeed + _PulsePhase;
                float2 warpWave = float2(
                    sin(warpUV.y * _FleshWarpScale + warpTime),
                    cos(warpUV.x * _FleshWarpScale + warpTime)
                );

                float warpTime2 = _Time.y * _FleshWarpSpeed2 - _PulsePhase * 0.7;
                float2 warpWave2 = float2(
                    sin(warpUV.y * _FleshWarpScale2 + warpTime2),
                    cos(warpUV.x * _FleshWarpScale2 + warpTime2)
                );

                float4 preSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, warpUV);
                float mask = saturate(lerp(1.0, preSample.r, _FleshWarpMaskStrength));

                warpUV += (warpWave * _FleshWarpStrength + warpWave2 * _FleshWarpStrength2) * mask;

                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, warpUV);
                float4 albedo = texColor * _BaseColor;

                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + _PulsePhase);
                float strength = saturate(_PulseStrength);

                float3 pulsedColor = lerp(albedo.rgb, albedo.rgb * _PulseColor.rgb, pulse * strength);
                float3 emission = _EmissionColor.rgb * (_PulseEmission * pulse);

                return float4(pulsedColor + emission, albedo.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
