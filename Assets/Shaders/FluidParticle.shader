Shader "Fluid/ParticlesURP"
{
    Properties
    {
        _Radius ("Radius", Float) = 0.08
        _Color ("Color", Color) = (0.2, 0.4, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> Positions;
            StructuredBuffer<float4> Colors;

            float _Radius;
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float3 center = Positions[v.instanceID].xyz;

                float3 worldPos = center + v.vertex.xyz * _Radius;

                o.pos = TransformWorldToHClip(worldPos);
                o.color = Colors[v.instanceID] * _Color;

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return i.color;
            }

            ENDHLSL
        }
    }
}
