Shader "Fluid/Depth"
{
    Properties
    {
        _ParticleRadius("Particle Radius", Float) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            StructuredBuffer<float3> Positions;

            float _ParticleRadius;

            struct appdata
            {
                float3 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 clipPos : SV_POSITION;

                float3 worldPos : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float3 center = Positions[v.instanceID];

                float3 world = center + v.vertex * _ParticleRadius;

                o.worldPos = world;
                o.localPos = v.vertex;

                o.clipPos = UnityWorldToClipPos(float4(world,1));

                o.screenPos = ComputeScreenPos(o.clipPos);

                return o;
            }

            float frag(v2f i, out float outDepth : SV_Depth) : SV_Target
            {
                // Reject pixels outside the sphere
                float r2 = dot(i.localPos, i.localPos);

                if (r2 > 1.0)
                    discard;

                // Linear eye-space depth
                float eyeDepth = -UnityWorldToViewPos(i.worldPos).z;

                // Write hardware depth
                float4 clip = UnityWorldToClipPos(float4(i.worldPos,1));

                outDepth = clip.z / clip.w;

                // Store linear depth for later passes
                return eyeDepth;
            }

            ENDHLSL
        }
    }
}