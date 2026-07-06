Shader "Fluid/Composite"
{
    Properties
    {
        _FluidTex ("Fluid Texture", 2D) = "black" {}
        _RawFluidTex ("Raw Fluid Texture", 2D) = "black" {}
        _FluidSceneDepthTex ("Fluid Scene Depth", 2D) = "black" {}
        _Normals ("Normals", 2D) = "bump" {}
        _Color ("Color", Color) = (0.2,0.5,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.8
        _RefractionStrength ("Refraction", Range(0,0.1)) = 0.02
        _Threshold ("Threshold", Float) = 0.025
        _Softness ("Softness", Float) = 0.04
        _Opacity ("Opacity", Range(0,1)) = 0.95
        _UseSceneDepthOcclusion("Use Scene Depth Occlusion", Float) = 1
        _DepthBias("Depth Bias", Float) = 0.0003
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            ZTest Always Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FluidTex);
            SAMPLER(sampler_FluidTex);

            TEXTURE2D(_RawFluidTex);
            SAMPLER(sampler_RawFluidTex);

            TEXTURE2D(_FluidSceneDepthTex);
            SAMPLER(sampler_FluidSceneDepthTex);

            TEXTURE2D(_Normals);
            SAMPLER(sampler_Normals);

            float4 _Color;
            float _Smoothness;
            float _RefractionStrength;
            float _Threshold;
            float _Softness;
            float _Opacity;
            float _UseSceneDepthOcclusion;
            float _DepthBias;

            struct appdata
            {
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 fluidUV : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.fluidUV = GetFullScreenTriangleTexCoord(v.vertexID);

                #if UNITY_UV_STARTS_AT_TOP
                o.fluidUV.y = 1.0 - o.fluidUV.y;
                #endif

                return o;
            }

            float3 DecodeNormal(float3 n)
            {
                return normalize(n * 2.0 - 1.0);
            }

            bool SceneHasOpaqueDepth(float sceneDepth)
            {
                #if UNITY_REVERSED_Z
                return sceneDepth > 0.00001;
                #else
                return sceneDepth < 0.99999;
                #endif
            }

            bool FluidIsBehindScene(float fluidDepth, float sceneDepth)
            {
                #if UNITY_REVERSED_Z
                return fluidDepth < sceneDepth - _DepthBias;
                #else
                return fluidDepth > sceneDepth + _DepthBias;
                #endif
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 fluidSample = SAMPLE_TEXTURE2D(_FluidTex, sampler_FluidTex, i.fluidUV).rg;
                float fluid = fluidSample.r;

                if (fluid <= 0.00001)
                    discard;

                float fluidDepth = fluidSample.g / max(fluid, 0.00001);
                float4 rawSample = SAMPLE_TEXTURE2D(_RawFluidTex, sampler_RawFluidTex, i.fluidUV);
                float compareDepth = fluidDepth;

                if (rawSample.r > 0.00001)
                    compareDepth = rawSample.b / rawSample.r;

                if (_UseSceneDepthOcclusion > 0.5)
                {
                    float sceneDepth = SAMPLE_TEXTURE2D(_FluidSceneDepthTex, sampler_FluidSceneDepthTex, i.fluidUV).r;

                    if (rawSample.r <= 0.00001 && SceneHasOpaqueDepth(sceneDepth))
                        discard;

                    if (FluidIsBehindScene(compareDepth, sceneDepth))
                        discard;
                }

                float3 normal = DecodeNormal(
                    SAMPLE_TEXTURE2D(_Normals, sampler_Normals, i.fluidUV).xyz
                );

                float mask = smoothstep(
                    _Threshold,
                    _Threshold + max(_Softness, 0.0001),
                    fluid
                );

                if (mask <= 0.001)
                    discard;

                // ----------------------------
                // 2. Lighting (simple but stable)
                // ----------------------------
                float3 lightDir = normalize(float3(0.4, 0.8, 0.3));
                float3 viewDir = float3(0, 0, 1);

                float ndotl = saturate(dot(normal, lightDir));
                float diffuse = ndotl;

                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(saturate(dot(normal, halfDir)), lerp(8, 64, _Smoothness));

                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 5.0);

                float3 lighting =
                    0.25 +
                    diffuse * 0.6 +
                    spec * 0.4 +
                    fresnel * 0.35;

                // ----------------------------
                // 4. Final color
                // ----------------------------
                float alpha = mask * _Opacity;

                float3 waterColor = lerp(_Color.rgb * 0.72, _Color.rgb * lighting, 0.45);

                return float4(waterColor, alpha);
            }

            ENDHLSL
        }
    }
}
