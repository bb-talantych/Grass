using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [Range(0.1f, 1f)]
    public float cullingBias = 0.5f;

    [Range(0f, 500f)]
    public float lodCutoff = 100f;

    [Header("Required Assets")]
    public Mesh grassMesh;
    public Material grassMaterial;
    public ComputeShader grassComputeShader;
    public Texture2D heightTex;

    private int kernelIndex, threadGroups;
    private ComputeBuffer grassDataBuffer, argsBuffer;

    void Start()
    {
        grassMaterial.SetVector("_ProtrusionDir", Vector3.back);

        GenerateGrass();
    }

    void Update()
    {
        grassComputeShader.SetFloat("_DisplacementStrength", displacementStrength);
        grassComputeShader.SetTexture(kernelIndex, "_HeightMap", heightTex);
        grassComputeShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

        grassMaterial.SetVector("_CamPos", Camera.main.transform.position);

        grassMaterial.SetColor("_BaseColor", baseColor);
        grassMaterial.SetColor("_TipColor", tipColor);
        grassMaterial.SetFloat("_LowGrassAnimationSpeed", lowGrassAnimationSpeed);
        grassMaterial.SetFloat("_HighGrassAnimationSpeed", highGrassAnimationSpeed);
        grassMaterial.SetVector("_WindDir", windDirection);
        grassMaterial.SetFloat("_DisplacementStrength", displacementStrength);
        grassMaterial.SetFloat("_CullingBias", cullingBias);
        grassMaterial.SetFloat("_LODCutoff", lodCutoff);

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
        kernelIndex = grassComputeShader.FindKernel("GetGrassData");
        threadGroups = Mathf.CeilToInt(grassFieldResolution / 8f);
        int totalSize = sizeof(float) * 3 + sizeof(float) * 2 + sizeof(float);

        grassDataBuffer = new ComputeBuffer(totalInstances, totalSize);

        grassComputeShader.SetBuffer(kernelIndex, "grassDataBuffer", grassDataBuffer);
        grassComputeShader.SetInt("grassFieldResolution", grassFieldResolution);
        grassComputeShader.SetInt("grassDensity", grassDensity);
        grassComputeShader.SetFloat("_DisplacementStrength", displacementStrength);
        grassComputeShader.SetTexture(kernelIndex, "_HeightMap", heightTex);
        grassComputeShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

        grassMaterial.enableInstancing = true;
        grassMaterial.SetBuffer("grassDataBuffer", grassDataBuffer);

        uint[] args = new uint[5]
        {
            grassMesh.GetIndexCount(0),
            (uint)totalInstances,
            0,
            0,
            0
        };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    void OnDestroy()
    {
        grassDataBuffer?.Release();
        argsBuffer?.Release();

        grassDataBuffer = null;
        argsBuffer = null;
    }
}