Shader "Fluid/Depth"
{
    Properties
    {
        _ParticleRadius("Particle Radius", Float) = 0.08
        _UseSceneDepthOcclusion("Use Scene Depth Occlusion", Float) = 1
        _DepthBias("Depth Bias", Float) = 0.0003
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask RGB
            Blend One One

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            StructuredBuffer<float3> Positions;

            float _ParticleRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 localPos : TEXCOORD0;
                float3 centerVS : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float3 center = Positions[v.instanceID];
                float2 disk = v.vertex.xy * 2.0;
                float3 centerVS = TransformWorldToView(center);
                float3 quadVS = centerVS + float3(disk * _ParticleRadius, 0.0);

                o.localPos = disk;
                o.centerVS = centerVS;
                o.positionCS = TransformWViewToHClip(quadVS);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.localPos, i.localPos);

                if (r2 > 1.0)
                    discard;

                float sphereZ = sqrt(saturate(1.0 - r2)) * _ParticleRadius;
                float3 surfaceVS = i.centerVS + float3(i.localPos * _ParticleRadius, sphereZ);
                float4 surfaceCS = TransformWViewToHClip(surfaceVS);
                float4 centerCS = TransformWViewToHClip(i.centerVS);
                float fluidDepth = surfaceCS.z / surfaceCS.w;
                float centerDepth = centerCS.z / centerCS.w;

                float core = sqrt(saturate(1.0 - r2));
                float thickness = core * core * _ParticleRadius;
                return float4(thickness, thickness * fluidDepth, thickness * centerDepth, 0.0);
            }

            ENDHLSL
        }
    }
}
