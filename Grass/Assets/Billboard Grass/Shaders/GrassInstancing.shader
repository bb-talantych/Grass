Shader "_BB/Billboad Grass Shader"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            float _Rotation, _Protrusion;
            float  _LowGrassAnimationSpeed, _HighGrassAnimationSpeed;
            float3 _ProtrusionDir, _WindDir;
            float  _DisplacementStrength;
            int _QuadID;
            float _CullingBias, _LODGroup0Percent, _LODGroup1Percent, _LODCutoff;

            float3 _CamPos;

            #define OFFSET_Y 0.5f
            #if !defined (OFFSET_Y)
               #define OFFSET_Y 0.0f;
            #endif

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
            
            bool QuadInLODGroup2Dist(float _dist)
            {
                float LODGroup1Dist = _LODCutoff * _LODGroup1Percent;
                return _dist > LODGroup1Dist;
            }

            bool VertexIsBelowClipPlane (float3 _vertex, int _planeIndex, float _bias) 
            {
                float4 plane = unity_CameraWorldClipPlanes[_planeIndex];
                return dot(float4(_vertex, 1), plane) < _bias;
            }
            bool QuandIsLODCulled(float _dist, int _quadID)
            {
                float LODGroup0Dist = _LODCutoff * _LODGroup0Percent;
                // fixes little zone between LOD1 and LOD2 with no grass
                float LOD1DistAdjust = 1;

                return  (_quadID == 1 && QuadInLODGroup2Dist(_dist)) ||
                        (_quadID == 0 && _dist > LODGroup0Dist && !QuadInLODGroup2Dist(_dist + LOD1DistAdjust));
                        
            }
            bool VertexIsCulled(float _dist, float3 _vertex, float _bias)
            {
                return  _dist > _LODCutoff ||
                        QuandIsLODCulled(_dist, _QuadID) ||
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
                offset.y += OFFSET_Y;
                offset.y -= (displacement * (1 - v.uv.y));
                if(_Protrusion != 0)
                {
                    float3 rotatedProtrusionDir = RotateAroundY(_ProtrusionDir, _Rotation);
                    offset += rotatedProtrusionDir * _Protrusion;
                }

                // animation
                float animationSpeed = lerp(_LowGrassAnimationSpeed, _HighGrassAnimationSpeed, normalizedDisplacement);
                float normalizedAnimationTime = sin(_Time.y * animationSpeed) * 0.5 + 0.5;
                float animationTime = lerp(-0.47, 1, normalizedAnimationTime) * (0.5, 1, normalizedDisplacement);
                offset += normalize(_WindDir) * animationTime * v.uv.y;

                // calcuate vertex world position and distance to Camera
                float3 rotatedVertex = RotateAroundY(v.vertex.xyz, _Rotation);
                float4 worldPos = float4(rotatedVertex + offset, 1.0f);
                float distToCam = GetDistToCamera(CAMERA_POSITION, worldPos);
                if(_QuadID == 0 && QuadInLODGroup2Dist(distToCam))
                {
                    float3 lookDir = normalize(_CamPos - worldPos);
                    float rotationAngle = degrees(atan2(lookDir.z, lookDir.x)) + 90.0f;

                    rotatedVertex = RotateAroundY(rotatedVertex, rotationAngle);
                    worldPos = float4(rotatedVertex + offset, 1.0f);
                    distToCam = GetDistToCamera(CAMERA_POSITION, worldPos);
                }

                // sending values to fragment + culling
                o.vertex = float4(0, 0, -1e8, 1);
                if(!VertexIsCulled(distToCam, worldPos, -_CullingBias * max(1.0f, _DisplacementStrength)))
                {
                    o.vertex = UnityObjectToClipPos(worldPos);
                }

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalizedDisplacement = normalizedDisplacement;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 mainTex = tex2D(_MainTex, i.uv);

                if(mainTex.a < 0.1)
                    discard;

                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float ndotl = DotClamped(lightDir, normalize(float3(0, 1, 0)));

                float4 finalColor = lerp(0, mainTex, i.uv.y);
                float4 topColor = lerp(mainTex, float4(1, 0.8, 0, 1), i.normalizedDisplacement);
                finalColor = lerp(finalColor, topColor, i.uv.y);
                finalColor = finalColor * ndotl;

                finalColor.rgb = lerp(finalColor, _Color, _Color.a);
                return finalColor;
            }
            ENDCG
        }
    }
}

