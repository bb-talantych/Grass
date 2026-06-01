using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

using static System.Runtime.InteropServices.Marshal;

public class GrassController_3D_V2 : MonoBehaviour
{
    [Header("Generation Properties")]
    [Range(1, 1000)]
    public int grassFieldSize = 300;
    [Range(1, 25)]
    public int grassDensity = 5;
    [Range(1, 25)]
    public int numChunks = 5;

    [Header("Wind Texture Properties")]
    public Vector2 windDir = new Vector2(1, 0.5f);
    public float windSpeed = 4.0f;
    public float frequency = 0.33f;
    public float windStrength = 2.0f;

    [Header("Shader Properties")]
    public Color baseColor = new Color(0.09569933f, 0.2641509f, 0.06852973f, 0);
    public Color tipColor = new Color(0.8584906f, 0.8019983f, 0.1012371f, 0);
    [Range(0.001f, 5)]
    public float displacementStrength = 1;

    [Header("Optimization Properties")]
    [Range(0f, 1f)]
    public float cullingBias = 0.1f;
    [Range(0f, 2f)]
    public float cullingBias_Down = 0.6f;
    [Range(0f, 1000f)]
    public float lodCutoff = 160f;
    [Range(0f, 1f)]
    public float lodGroup0 = 0.35f;

    [Header("Required Assets")]
    public Mesh grassMesh_LOD0;
    public Mesh grassMesh_LOD1;
    public Material grassMaterial_LOD0, grassMaterial_LOD1;
    public ComputeShader chunkDataCompute, cullGrassCompute, windNoiseCompute;
    public Texture2D heightTex;

    private struct GrassData3D
    {
        public Vector3 position;
        public Vector2 uv;
        public float displacement;
    }
    private struct ChunkData
    {
        public int ChunkID;

        public ComputeBuffer grassDataBuffer;
        public ComputeBuffer culledGrassBuffer;
        public ComputeBuffer argsBuffer;

        public Bounds chunkBounds;

        public Material grassMaterial_LOD0;
        public Material grassMaterial_LOD1;
    }

    private Camera cam;

    private RenderTexture depthRT;
    private RenderTexture windNoiseTexture;

    private ChunkData[] chunkArray;
    private int totalChunkGrass, chunkDimension;
    private int chunkDataKernelIndex, cullChunksKernelIndex, cullGrassKernelIndex, windNoiseKernelIndex;
    private int chunkDataThreadGroups, cullChunksThreadGroups, cullGrassThreadGroups, windNoiseThreadGroups;
    private Vector3 chunkSize;

    ComputeBuffer argsCopyCountBuffer, chunksCenterBuffer, culledChunksBuffer;
    int[] visibleChunksArr;

    void Start()
    {
        cam = Camera.main;

        int multiplier = 2;
        multiplier = Mathf.Max(multiplier, 1);
        int depthTexWidth = 480 * multiplier;
        int depthTexHeight = 270 * multiplier;
        CreateDepthRenderTexture(depthTexWidth, depthTexHeight);

        int windTexSize = 1024;
        InitializeWindData(windTexSize);

        InitializeData();
    }
    private void Update()
    {
        UpdateDepthRenderTexture();

        GenerateWind();

        UpdateCullGrassCommon();

        RetrieveVisibleChunks();

        foreach (var chunk in chunkArray)
        {
            if (IsChunkCulled(chunk)) continue;

            CullGrass(chunk);

            UpdateChunkVariables(chunk);

            float dist = Vector3.Distance(cam.transform.position, chunk.chunkBounds.center);
            if (dist < lodGroup0 * lodCutoff)
            {
                uint[] argsData = new uint[5]
                {
                    grassMesh_LOD0.GetIndexCount(0),
                    (uint)GetCulledCount(chunk.culledGrassBuffer),
                    0,
                    0,
                    0
                };
                chunk.argsBuffer.SetData(argsData);

                Graphics.DrawMeshInstancedIndirect(
                    grassMesh_LOD0,
                    0,
                    chunk.grassMaterial_LOD0,
                    new Bounds(Vector3.zero, new Vector3(grassFieldSize, displacementStrength * 2, grassFieldSize)),
                    chunk.argsBuffer
                );
            }
            else
            {
                uint[] argsData = new uint[5]
                {
                    grassMesh_LOD1.GetIndexCount(0),
                    (uint)GetCulledCount(chunk.culledGrassBuffer),
                    0,
                    0,
                    0
                };
                chunk.argsBuffer.SetData(argsData);

                Graphics.DrawMeshInstancedIndirect(
                    grassMesh_LOD1,
                    0,
                    chunk.grassMaterial_LOD1,
                    new Bounds(Vector3.zero, new Vector3(grassFieldSize, displacementStrength * 2, grassFieldSize)),
                    chunk.argsBuffer
                );
            }
        }
    }

    #region Start Functions
    void CreateDepthRenderTexture(int _width, int _height)
    {
        depthRT = new RenderTexture(_width, _height, 24, RenderTextureFormat.Depth);
        cam.targetTexture = depthRT;
        cam.Render();
        cam.targetTexture = null;
    }
    void InitializeWindData(int _textureSize)
    {
        // Creating RenderTexture
        if (windNoiseTexture != null)
        {
            windNoiseTexture.Release();
            windNoiseTexture = null;
        }

        windNoiseTexture = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RFloat);

        windNoiseTexture.enableRandomWrite = true;
        windNoiseTexture.wrapMode = TextureWrapMode.Repeat;

        windNoiseTexture.Create();

        // For WindNoise
        windNoiseKernelIndex = windNoiseCompute.FindKernel("WindNoise");
        windNoiseThreadGroups = Mathf.CeilToInt(_textureSize / 8.0f);
        windNoiseCompute.SetTexture(windNoiseKernelIndex, "_WindMap", windNoiseTexture);
    }
    void InitializeData()
    {
        // Setting variables
        int grassFieldResolution = grassFieldSize * grassDensity;
        int totalGrass = grassFieldResolution * grassFieldResolution;

        // Chunk variables
        int totalChunks = numChunks * numChunks;
        totalChunkGrass = Mathf.CeilToInt(totalGrass / (float)totalChunks);
        int chunkResolution = Mathf.CeilToInt(grassFieldResolution / (float)numChunks);

        chunkDataThreadGroups = Mathf.CeilToInt(chunkResolution / 8.0f);
        chunkDimension = Mathf.CeilToInt(grassFieldSize / (float)numChunks);

        // Buffers
        argsCopyCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        chunksCenterBuffer = new ComputeBuffer(totalGrass, 3 * sizeof(float));
        culledChunksBuffer = new ComputeBuffer(totalChunks, sizeof(int));

        // ChunkData
        chunkArray = new ChunkData[totalChunks];
        visibleChunksArr = new int[totalChunks];

        chunkDataKernelIndex = chunkDataCompute.FindKernel("GetChunkData");

        chunkDataCompute.SetInt("chunkResolution", chunkResolution);
        chunkDataCompute.SetInt("grassDensity", grassDensity); 
        chunkDataCompute.SetInt("numChunks", numChunks); 
        chunkDataCompute.SetTexture(chunkDataKernelIndex, "_HeightMap", heightTex);

        // Culling Variables
        cullChunksKernelIndex = cullGrassCompute.FindKernel("CullChunk");
        cullChunksThreadGroups = Mathf.CeilToInt(totalChunks / 64f);
        cullGrassKernelIndex = cullGrassCompute.FindKernel("CullGrass");
        cullGrassThreadGroups = Mathf.CeilToInt(totalChunkGrass / 64f);
        chunkSize = new Vector3(chunkDimension, 10, chunkDimension);

        // Setup Chunks
        Vector3[] chunkCenters = new Vector3[totalChunks];
        for (int z = 0; z < numChunks; z++) 
        {
            for (int x = 0; x < numChunks; x++)
            {
                int id = x + z * numChunks;
                chunkArray[id] = CreateChunk(x, z, id);
                chunkCenters[id] = chunkArray[id].chunkBounds.center;
            }
        }
        chunksCenterBuffer.SetData(chunkCenters);

        // Culling Common
        cullGrassCompute.SetInt("_TotalChunks", totalChunks);
        cullGrassCompute.SetInt("_TotalChunkGrass", totalChunkGrass);
        cullGrassCompute.SetVector("_ChunkSize", chunkSize);
        cullGrassCompute.SetBuffer(cullChunksKernelIndex, "chunksCenterBuffer", chunksCenterBuffer);
        cullGrassCompute.SetBuffer(cullChunksKernelIndex, "culledChunksBuffer", culledChunksBuffer);
        cullGrassCompute.SetBuffer(cullGrassKernelIndex, "culledChunksBuffer", culledChunksBuffer);
    }
    ChunkData CreateChunk(int _xOffset, int _zOffset, int _id)
    {
        ChunkData chunk = new ChunkData();

        // ID
        chunk.ChunkID = _id;

        // argsBuffer
        chunk.argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

        // GrassData
        chunk.grassDataBuffer = new ComputeBuffer(totalChunkGrass, SizeOf(typeof(GrassData3D)));

        chunkDataCompute.SetBuffer(chunkDataKernelIndex, "chunkDataBuffer", chunk.grassDataBuffer);
        chunkDataCompute.SetInt("xOffset", _xOffset);
        chunkDataCompute.SetInt("zOffset", _zOffset);
        chunkDataCompute.Dispatch(chunkDataKernelIndex, chunkDataThreadGroups, chunkDataThreadGroups, 1);

        // CullGrass
        chunk.culledGrassBuffer = new ComputeBuffer(totalChunkGrass, SizeOf(typeof(GrassData3D)), ComputeBufferType.Append);

        // ChunkBounds
        Vector3 chunkCenter = Vector3.zero;
        chunkCenter.x -=(chunkDimension * 0.5f * numChunks);
        chunkCenter.z -=(chunkDimension * 0.5f * numChunks);
        chunkCenter.x += chunkDimension * _xOffset;
        chunkCenter.z += chunkDimension * _zOffset;
        chunkCenter.x += chunkDimension * 0.5f;
        chunkCenter.z += chunkDimension * 0.5f;

        chunk.chunkBounds = 
            new Bounds(chunkCenter, chunkSize);
        
        // Grass Material
        Material chunkGrassMaterial_LOD0 = new Material(grassMaterial_LOD0);
        chunkGrassMaterial_LOD0.enableInstancing = true;
        chunkGrassMaterial_LOD0.SetBuffer("grassDataBuffer", chunk.culledGrassBuffer);
        chunkGrassMaterial_LOD0.SetTexture("_WindTex", windNoiseTexture);
        chunk.grassMaterial_LOD0 = chunkGrassMaterial_LOD0;

        Material chunkGrassMaterial_LOD1 = new Material(grassMaterial_LOD1);
        chunkGrassMaterial_LOD1.enableInstancing = true;
        chunkGrassMaterial_LOD1.SetBuffer("grassDataBuffer", chunk.culledGrassBuffer);
        chunkGrassMaterial_LOD1.SetTexture("_WindTex", windNoiseTexture);
        chunk.grassMaterial_LOD1 = chunkGrassMaterial_LOD1;

        return chunk;
    }
    #endregion

    #region Update Functions
    void UpdateDepthRenderTexture()
    {
        cam.targetTexture = depthRT;
        cam.Render();
        cam.targetTexture = null;
    }
    void GenerateWind()
    {
        windNoiseCompute.SetVector("_WindDir", windDir.normalized);
        windNoiseCompute.SetFloat("_Time", Time.time * windSpeed);
        windNoiseCompute.SetFloat("_Frequency", frequency);
        windNoiseCompute.SetFloat("_Amplitude", windStrength);

        windNoiseCompute.Dispatch(windNoiseKernelIndex, windNoiseThreadGroups, windNoiseThreadGroups, 1);
    }
    void UpdateCullGrassCommon()
    {
        cullGrassCompute.SetTexture(cullGrassKernelIndex, "_CameraDepthTexture", depthRT);

        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;
        cullGrassCompute.SetMatrix("_ViewProjectionMatrix", viewProjMatrix);
        cullGrassCompute.SetMatrix("_CameraProjectionToWorld", Matrix4x4.Inverse(viewProjMatrix));

        cullGrassCompute.SetVector("_CamPos", cam.transform.position);
        cullGrassCompute.SetFloat("_DisplacementStrength", displacementStrength);
        cullGrassCompute.SetFloat("_LODCutoff", lodCutoff);
        cullGrassCompute.SetFloat("_CullingBias", cullingBias);
        cullGrassCompute.SetFloat("_CullingBias_Down", cullingBias_Down);
        cullGrassCompute.SetVectorArray("_CameraClipPlanes", GetViewFrustumPlaneNormals(cam));
    }
    void RetrieveVisibleChunks()
    {
        cullGrassCompute.Dispatch(cullChunksKernelIndex, cullChunksThreadGroups, 1, 1);

        culledChunksBuffer.GetData(visibleChunksArr);
    }
    bool IsChunkCulled(ChunkData _chunk)
    {
        return visibleChunksArr[_chunk.ChunkID] == 0;
    }
    void CullGrass(ChunkData _chunk)
    {
        cullGrassCompute.SetBuffer(cullGrassKernelIndex, "grassDataBuffer", _chunk.grassDataBuffer);
        cullGrassCompute.SetBuffer(cullGrassKernelIndex, "culledGrassBuffer", _chunk.culledGrassBuffer);

        _chunk.culledGrassBuffer.SetCounterValue(0);
        cullGrassCompute.Dispatch(cullGrassKernelIndex, cullGrassThreadGroups, 1, 1);
    }
    void UpdateChunkVariables(ChunkData _chunk)
    {
        _chunk.grassMaterial_LOD0.SetColor("_BaseColor", baseColor);
        _chunk.grassMaterial_LOD0.SetColor("_TipColor", tipColor);
        _chunk.grassMaterial_LOD0.SetVector("_WindDir", windDir.normalized);
        _chunk.grassMaterial_LOD0.SetFloat("_DisplacementStrength", displacementStrength);

        _chunk.grassMaterial_LOD1.SetColor("_BaseColor", baseColor);
        _chunk.grassMaterial_LOD1.SetColor("_TipColor", tipColor);
        _chunk.grassMaterial_LOD1.SetVector("_WindDir", windDir.normalized);
        _chunk.grassMaterial_LOD1.SetFloat("_DisplacementStrength", displacementStrength);
    }
    int GetCulledCount(ComputeBuffer _buffer)
    {
        ComputeBuffer.CopyCount(_buffer, argsCopyCountBuffer, 0);
        int[] appendBufferCount = new int[1];
        argsCopyCountBuffer.GetData(appendBufferCount);
        return appendBufferCount[0];
    }
    private Vector4[] GetViewFrustumPlaneNormals(Camera _cam)
    {
        const int numPlanes = 4;
        Vector4[] planeNormals = new Vector4[numPlanes];
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);

        for (int i = 0; i < numPlanes; i++)
        {
            planeNormals[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
        }
        return planeNormals;
    }
    #endregion

    void OnDestroy()
    {
        for (int i = 0; i < chunkArray.Length; i++) 
        {
            chunkArray[i].grassDataBuffer?.Release();
            chunkArray[i].grassDataBuffer = null;

            chunkArray[i].culledGrassBuffer?.Release();
            chunkArray[i].culledGrassBuffer = null;

            chunkArray[i].argsBuffer?.Release();
            chunkArray[i].argsBuffer = null;
        }

        windNoiseTexture.Release();
        windNoiseTexture = null;

        argsCopyCountBuffer?.Release();
        argsCopyCountBuffer = null;

        chunksCenterBuffer?.Release();
        chunksCenterBuffer = null;

        culledChunksBuffer?.Release();
        culledChunksBuffer = null;
    }

    private void OnDrawGizmos()
    {
        if (chunkArray == null) 
            return;

        Gizmos.color = Color.red;
        foreach (var chunk in chunkArray) 
        {
            Gizmos.DrawWireCube(chunk.chunkBounds.center, chunk.chunkBounds.size);
        }
    }
}