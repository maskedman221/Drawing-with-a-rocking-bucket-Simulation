Shader "Fluid/Composite"
{
    Properties
    {
        _Color ("Color", Color) = (0.2,0.5,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.8
        _RefractionStrength ("Refraction", Range(0,0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            ZTest Always Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FluidTex);
            SAMPLER(sampler_FluidTex);

            TEXTURE2D(_Normals);
            SAMPLER(sampler_Normals);

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float4 _Color;
            float _Smoothness;
            float _RefractionStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            float3 DecodeNormal(float3 n)
            {
                return normalize(n * 2.0 - 1.0);
            }

            float4 frag(v2f i) : SV_Target
            {
                float fluid = SAMPLE_TEXTURE2D(_FluidTex, sampler_FluidTex, i.uv).r;

                float3 normal = DecodeNormal(
                    SAMPLE_TEXTURE2D(_Normals, sampler_Normals, i.uv).xyz
                );

                float sceneDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;

                // ----------------------------
                // 1. Fluid mask
                // ----------------------------
                float thicknessMask = smoothstep(0.02, 0.15, fluid);

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
                // 3. SCREEN SPACE REFRACTION (NEW)
                // ----------------------------

                float2 uv = i.uv;

                // distort UV using normal
                float2 refractionOffset = normal.xy * _RefractionStrength;

                float2 refractedUV = uv + refractionOffset;

                float3 background =
                    SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, refractedUV).rgb;

                // fallback if opaque texture missing
                float3 fallbackBG =
                    SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv).rgb;

                float3 sceneColor = background;

                // ----------------------------
                // 4. Final color
                // ----------------------------
                float alpha = thicknessMask;

                float3 waterColor = _Color.rgb * lighting;

                // blend water with background
                float3 finalColor =
                    lerp(sceneColor, waterColor, alpha);

                return float4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}