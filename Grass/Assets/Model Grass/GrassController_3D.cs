using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using static System.Runtime.InteropServices.Marshal;

public class GrassController_3D : MonoBehaviour
{
    [Header("Generation Properties")]
    [Range(1, 1000)]
    public int grassFieldSize = 300;
    [Range(1, 25)]
    public int grassDensity = 5;

    [Header("Wind Texture Properties")]
    public Vector2 windDir = new Vector2(1, 0.5f);
    public float windSpeed = 4.0f;
    public float frequency = 0.33f;
    public float windStrength = 0.5f;

    [Header("Shader Properties")]
    public Color baseColor = new Color(0.09569933f, 0.2641509f, 0.06852973f, 0);
    public Color tipColor = new Color(0.8584906f, 0.8019983f, 0.1012371f, 0);
    [Range(0.001f, 5)]
    public float displacementStrength = 1;

    [Header("Optimization Properties")]
    [Range(0f, 1f)]
    public float cullingBias = 0.25f;
    [Range(0f, 2f)]
    public float cullingBias_Down = 1.75f;
    [Range(0f, 500f)]
    public float lodCutoff = 100f;

    [Header("Required Assets")]
    public Mesh grassMesh;
    public Material grassMaterial;
    public ComputeShader grassDataCompute, cullGrassCompute, windNoiseCompute;
    public Texture2D heightTex;

    private ComputeBuffer grassDataBuffer, culledGrassBuffer, argsBuffer, argsCopyCountBuffer;
    private int culledGrassKernelIndex, windNoiseKernelIndex;
    private int culledGrassThreadGroups, windNoiseThreadGroups;
    private RenderTexture windNoiseTexture;

    private struct GrassData3D
    {
        public Vector3 position;
        public Vector2 uv;
        public float displacement;
    }

    void Start()
    {
        int windTexSize = 1024;
        InitializeData(windTexSize);

    }

    void Update()
    {
        cullGrassCompute.SetVector("_CamPos", Camera.main.transform.position);
        cullGrassCompute.SetFloat("_LODCutoff", lodCutoff);
        cullGrassCompute.SetFloat("_CullingBias", cullingBias);
        cullGrassCompute.SetFloat("_CullingBias_Down", cullingBias_Down);
        cullGrassCompute.SetVectorArray("_CameraClipPlanes", GetViewFrustumPlaneNormals(Camera.main));

        culledGrassBuffer.SetCounterValue(0);
        cullGrassCompute.Dispatch(culledGrassKernelIndex, culledGrassThreadGroups, 1, 1);

        uint[] argsData = new uint[5]
         {
            grassMesh.GetIndexCount(0),
            (uint)GetCulledGrassCount(),
            0,
            0,
            0
        };
        argsBuffer.SetData(argsData);

        GenerateWind();

        grassMaterial.SetVector("_CamPos", Camera.main.transform.position);

        grassMaterial.SetColor("_BaseColor", baseColor);
        grassMaterial.SetColor("_TipColor", tipColor);
        grassMaterial.SetVector("_WindDir", windDir.normalized);
        grassMaterial.SetFloat("_DisplacementStrength", displacementStrength);

        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial,
            new Bounds(Vector3.zero, new Vector3(grassFieldSize, 10f, grassFieldSize)),
            argsBuffer
        );       
    }

    void InitializeData(int _textureSize)
    {
        // Setting variables
        int grassFieldResolution = grassFieldSize * grassDensity;
        int totalInstances = grassFieldResolution * grassFieldResolution;
        int grassDataKernelIndex = grassDataCompute.FindKernel("GetGrassData3D");
        int grassDataThreadGroups = Mathf.CeilToInt(grassFieldResolution / 8f);

        // GrassData
        grassDataBuffer = new ComputeBuffer(totalInstances, SizeOf(typeof(GrassData3D)));

        grassDataCompute.SetBuffer(grassDataKernelIndex, "grassData3DBuffer", grassDataBuffer);
        grassDataCompute.SetInt("grassFieldResolution", grassFieldResolution);
        grassDataCompute.SetInt("grassDensity", grassDensity);
        grassDataCompute.SetTexture(grassDataKernelIndex, "_HeightMap", heightTex);
        grassDataCompute.Dispatch(grassDataKernelIndex, grassDataThreadGroups, grassDataThreadGroups, 1);

        // CullGrass
        culledGrassKernelIndex = cullGrassCompute.FindKernel("AppendCulledGrass");
        culledGrassThreadGroups = Mathf.CeilToInt(totalInstances / 64f);

        culledGrassBuffer = new ComputeBuffer(totalInstances, SizeOf(typeof(GrassData3D)), ComputeBufferType.Append);

        cullGrassCompute.SetFloat("_TotalInstances", totalInstances);
        cullGrassCompute.SetBuffer(culledGrassKernelIndex, "grassDataBuffer", grassDataBuffer);
        cullGrassCompute.SetBuffer(culledGrassKernelIndex, "culledGrassBuffer", culledGrassBuffer);

        // argsBuffer
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsCopyCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        // WindNoise
        InitializeWindData(_textureSize);

        // Grass Material
        grassMaterial.enableInstancing = true;
        grassMaterial.SetBuffer("grassDataBuffer", culledGrassBuffer);
        grassMaterial.SetTexture("_WindTex", windNoiseTexture);
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

    void GenerateWind()
    {
        windNoiseCompute.SetVector("_WindDir", windDir.normalized);
        windNoiseCompute.SetFloat("_Time", Time.time * windSpeed);
        windNoiseCompute.SetFloat("_Frequency", frequency);
        windNoiseCompute.SetFloat("_Amplitude", windStrength);

        windNoiseCompute.Dispatch(windNoiseKernelIndex, windNoiseThreadGroups, windNoiseThreadGroups, 1);
    }
    int GetCulledGrassCount()
    {
        ComputeBuffer.CopyCount(culledGrassBuffer, argsCopyCountBuffer, 0);
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

    void OnDestroy()
    {
        grassDataBuffer?.Release();
        culledGrassBuffer?.Release();
        argsBuffer?.Release();
        argsCopyCountBuffer?.Release();

        grassDataBuffer = null;
        culledGrassBuffer = null;
        argsBuffer = null;
        argsCopyCountBuffer = null;

        windNoiseTexture.Release();
        windNoiseTexture = null;
    }
}