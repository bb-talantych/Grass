Shader "_BB/3D Grass Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #pragma target 4.6

            #include "UnityPBSLighting.cginc"

            struct GrassData 
            {
                float3 position;
                float2 uv;
                float displacement;
            };

            StructuredBuffer<GrassData> grassDataBuffer;

            float3 _BaseColor, _TipColor;
            float4 _Color;

            float  _LowGrassAnimationSpeed, _HighGrassAnimationSpeed;
            float3 _ProtrusionDir, _WindDir;
            float  _DisplacementStrength;
            float _CullingBias, _LODCutoff;

            float3 _CamPos;

            #define CAMERA_POSITION _CamPos
            #if !defined(CAMERA_POSITION)
                #define CAMERA_POSITION _WorldSpaceCameraPos
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;

                float normalizedDisplacement : TEXCOORD1;
            };

            float hash(uint n) 
            {
                // integer hash copied from Hugo Elias
	            n = (n << 13U) ^ n;
                n = n * (n * n * 15731U + 0x789221U) + 0x1376312589U;
                return float(n & uint(0x7fffffffU)) / float(0x7fffffff);
            }
            float3 RotateAroundY(float3 _vertex, float _deg)
            {
                float rad = radians(_deg);
                float cosine = cos(rad);
                float sine = sin(rad);

                float3 rotatedVertex = _vertex;
                rotatedVertex.x = _vertex.x * cosine - _vertex.z * sine;
                rotatedVertex.z = _vertex.x * sine + _vertex.z * cosine;

                return rotatedVertex;
            }

            float GetDistToCamera(float3 _CameraPosition, float3 _vertex)
            {
                return distance(_CameraPosition, _vertex);
            }

            bool VertexIsBelowClipPlane (float3 _vertex, int _planeIndex, float _bias) 
            {
                float4 plane = unity_CameraWorldClipPlanes[_planeIndex];
                return dot(float4(_vertex, 1), plane) < _bias;
            }
            bool VertexIsCulled(float _dist, float3 _vertex, float _bias)
            {
                return  _dist > _LODCutoff ||
                        VertexIsBelowClipPlane(_vertex, 0, _bias) ||
		                VertexIsBelowClipPlane(_vertex, 1, _bias) ||
		                VertexIsBelowClipPlane(_vertex, 2, _bias) ||
		                VertexIsBelowClipPlane(_vertex, 3, _bias);
            }

            v2f vert(appdata v, uint id : SV_InstanceID)
            {
                v2f o;

                // get values from command buffer
                float3 offset = grassDataBuffer[id].position;
                float displacement = grassDataBuffer[id].displacement;
                
                float normalizedDisplacement = 0;
                if(_DisplacementStrength != 0)
                {
                    normalizedDisplacement = displacement * (1 / _DisplacementStrength);
                }

                // adjust vertex position
                offset.y += (displacement * v.uv.y);

                // animation
                float animationSpeed = lerp(_LowGrassAnimationSpeed, _HighGrassAnimationSpeed, normalizedDisplacement);
                float normalizedAnimationTime = sin(_Time.y * animationSpeed) * 0.5 + 0.5;
                float animationTime = lerp(-0.47, 1, normalizedAnimationTime) * (0.5, 1, normalizedDisplacement);
                offset += normalize(_WindDir) * animationTime * v.uv.y;

                // calcuate vertex world position and distance to Camera
                int seed = 3235523;
                float rotAngle = hash(seed + id) * 360;
                float3 rotatedVertex = RotateAroundY(v.vertex.xyz, rotAngle);
                float4 worldPos = float4(rotatedVertex + offset, 1.0f);
                float distToCam = GetDistToCamera(CAMERA_POSITION, worldPos);

                // sending values to fragment + culling
                o.vertex = float4(0, 0, -1e8, 1);
                if(!VertexIsCulled(distToCam, worldPos, -_CullingBias * max(1.0f, _DisplacementStrength)))
                {
                    o.vertex = UnityObjectToClipPos(worldPos);
                }

                o.uv = v.uv;
                o.normalizedDisplacement = normalizedDisplacement;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float ndotl = DotClamped(lightDir, normalize(float3(0, 1, 0)));

                float3 finalColor = _BaseColor;

                float3 tipColor = lerp(_BaseColor, _TipColor, i.normalizedDisplacement);
                float tipUV = i.uv.y * (1.37f * i.normalizedDisplacement);
                finalColor = lerp(finalColor, tipColor, tipUV);

                float3 AO_Color = float3(0, 0, 0);
                float AO_UV = (1 - i.uv.y) * (1 - i.uv.y) * (1 - i.uv.y) * 1.47f;
                finalColor = lerp(finalColor, AO_Color, AO_UV);

                finalColor = finalColor * ndotl;

                finalColor.rgb = lerp(finalColor, _Color, _Color.a);
                return float4(finalColor, 1);
            }
            ENDCG
        }
    }
}

