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
    public int grassDensity = 2;
    [Range(0.001f, 5)]
    public float displacementStrength = 2;

    [Header("Shader Properties")]
    public Color baseColor;
    public Color tipColor;
    public Vector3 windDirection = new Vector3(1, 0.5f, 0);
    [Range(0, 5f)]
    public float lowGrassAnimationSpeed = 1.2f;
    [Range(0, 5f)]
    public float highGrassAnimationSpeed = 0.47f;

    [Header("Optimization Properties")]
    [Range(0f, 1f)]
    public float cullingBias = 0.5f;
    [Range(0f, 500f)]
    public float lodCutoff = 100f;

    [Header("Required Assets")]
    public Mesh grassMesh;
    public Material grassMaterial;
    public ComputeShader grassDataCompute, cullGrassCompute;
    public Texture2D heightTex;

    private ComputeBuffer grassDataBuffer, culledGrassBuffer, argsBuffer, argsCopyCountBuffer;
    private int culledGrassKernelIndex;
    private int culledGrassThreadGroups;

    private struct GrassData3D
    {
        public Vector3 position;
        public Vector2 uv;
        public float displacement;
    }

    void Start()
    {
        GenerateGrass();
    }

    void Update()
    {
        cullGrassCompute.SetVector("_CamPos", Camera.main.transform.position);
        cullGrassCompute.SetFloat("_LODCutoff", lodCutoff);
        cullGrassCompute.SetFloat("_CullingBias", cullingBias);
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

        grassMaterial.SetVector("_CamPos", Camera.main.transform.position);

        grassMaterial.SetColor("_BaseColor", baseColor);
        grassMaterial.SetColor("_TipColor", tipColor);
        grassMaterial.SetFloat("_LowGrassAnimationSpeed", lowGrassAnimationSpeed);
        grassMaterial.SetFloat("_HighGrassAnimationSpeed", highGrassAnimationSpeed);
        grassMaterial.SetVector("_WindDir", windDirection);
        grassMaterial.SetFloat("_DisplacementStrength", displacementStrength);

        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial,
            new Bounds(Vector3.zero, new Vector3(grassFieldSize, 10f, grassFieldSize)),
            argsBuffer
        );       
    }

    void GenerateGrass()
    {
        int grassFieldResolution = grassFieldSize * grassDensity;
        int totalInstances = grassFieldResolution * grassFieldResolution;
        int grassDataKernelIndex = grassDataCompute.FindKernel("GetGrassData3D");
        int grassDataThreadGroups = Mathf.CeilToInt(grassFieldResolution / 8f);

        grassDataBuffer = new ComputeBuffer(totalInstances, SizeOf(typeof(GrassData3D)));

        grassDataCompute.SetBuffer(grassDataKernelIndex, "grassData3DBuffer", grassDataBuffer);
        grassDataCompute.SetInt("grassFieldResolution", grassFieldResolution);
        grassDataCompute.SetInt("grassDensity", grassDensity);
        grassDataCompute.SetTexture(grassDataKernelIndex, "_HeightMap", heightTex);
        grassDataCompute.Dispatch(grassDataKernelIndex, grassDataThreadGroups, grassDataThreadGroups, 1);

        culledGrassKernelIndex = cullGrassCompute.FindKernel("AppendCulledGrass");
        culledGrassThreadGroups = Mathf.CeilToInt(totalInstances / 64f);

        culledGrassBuffer = new ComputeBuffer(totalInstances, SizeOf(typeof(GrassData3D)), ComputeBufferType.Append);

        cullGrassCompute.SetFloat("_TotalInstances", totalInstances);
        cullGrassCompute.SetBuffer(culledGrassKernelIndex, "grassDataBuffer", grassDataBuffer);
        cullGrassCompute.SetBuffer(culledGrassKernelIndex, "culledGrassBuffer", culledGrassBuffer);

        grassMaterial.enableInstancing = true;
        grassMaterial.SetBuffer("grassDataBuffer", culledGrassBuffer);

        // argsBuffer
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsCopyCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
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
        Vector4[] planeNormals = new Vector4[4];
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);

        for (int i = 0; i < 4; i++)
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
    }
}