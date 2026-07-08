Shader "Fluid/ParticlesURP"
{
    Properties
    {
        _Radius ("Radius", Float) = 0.08
        _Color ("Color", Color) = (0.2, 0.4, 1, 1)
        _HidePlaneParticles ("Hide Plane Particles", Float) = 0
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
            StructuredBuffer<int> ParticleStates;

            float _Radius;
            float4 _Color;
            float _HidePlaneParticles;

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float hide : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.hide = 0;

                if (_HidePlaneParticles > 0.5 && ParticleStates[v.instanceID] == 1)
                {
                    o.pos = float4(0, 0, 0, 0);
                    o.color = 0;
                    o.hide = 1;
                    return o;
                }

                float3 center = Positions[v.instanceID].xyz;

                float3 worldPos = center + v.vertex.xyz * _Radius;

                o.pos = TransformWorldToHClip(worldPos);
                o.color = Colors[v.instanceID] * _Color;

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                if (i.hide > 0.5)
                    discard;

                return i.color;
            }

            ENDHLSL
        }
    }
}
