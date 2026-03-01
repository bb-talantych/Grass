using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassController : MonoBehaviour
{
    [Header("Generation Properties")]
    [Range(1, 1000)]
    public int grassFieldSize = 300;
    [Range(1, 25)]
    public int grassDensity = 2;
    [Range(0.001f, 5)]
    public float displacementStrength = 2;

    [Header("Shader Properties")]
    [Range(0, 360)]
    public float rotation = 45f;
    [Range(0, 0.2f)]
    public float protrusion = 0f;   
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
    [Range(0f, 1f)]
    public float lodGroup1 = 0.15f;
    [Range(0f, 1f)]
    public float lodGroup0 = 0.1f;

    [Header("Required Assets")]
    public Mesh grassMesh;
    public Material grassMaterial, grassMaterial2, grassMaterial3;
    public ComputeShader grassComputeShader;
    public Texture2D heightTex;

    private int kernelIndex, threadGroups;
    private ComputeBuffer grassDataBuffer, argsBuffer;

    void Start()
    {
        grassMaterial.SetVector("_ProtrusionDir", Vector3.back);
        grassMaterial2.SetVector("_ProtrusionDir", Vector3.forward);
        grassMaterial3.SetVector("_ProtrusionDir", Vector3.forward);

        grassMaterial.SetInt("_QuadID", 0);
        grassMaterial2.SetInt("_QuadID", 1);
        grassMaterial3.SetInt("_QuadID", 1);

        GenerateGrass();
    }

    void Update()
    {
        grassComputeShader.SetFloat("_DisplacementStrength", displacementStrength);
        grassComputeShader.SetTexture(kernelIndex, "_HeightMap", heightTex);
        grassComputeShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

        grassMaterial.SetVector("_CamPos", Camera.main.transform.position);
        grassMaterial2.SetVector("_CamPos", Camera.main.transform.position);
        grassMaterial3.SetVector("_CamPos", Camera.main.transform.position);

        grassMaterial.SetFloat("_Protrusion", protrusion);
        grassMaterial.SetFloat("_LowGrassAnimationSpeed", lowGrassAnimationSpeed);
        grassMaterial.SetFloat("_HighGrassAnimationSpeed", highGrassAnimationSpeed);
        grassMaterial.SetVector("_WindDir", windDirection);
        grassMaterial.SetFloat("_DisplacementStrength", displacementStrength);
        grassMaterial.SetFloat("_CullingBias", cullingBias);
        grassMaterial.SetFloat("_LODGroup0Percent", lodGroup0);
        grassMaterial.SetFloat("_LODGroup1Percent", lodGroup1);
        grassMaterial.SetFloat("_LODCutoff", lodCutoff);

        grassMaterial2.SetFloat("_Rotation", rotation);
        grassMaterial2.SetFloat("_Protrusion", protrusion);
        grassMaterial2.SetFloat("_LowGrassAnimationSpeed", lowGrassAnimationSpeed);
        grassMaterial2.SetFloat("_HighGrassAnimationSpeed", highGrassAnimationSpeed);
        grassMaterial2.SetVector("_WindDir", windDirection);
        grassMaterial2.SetFloat("_DisplacementStrength", displacementStrength);
        grassMaterial2.SetFloat("_CullingBias", cullingBias);
        grassMaterial2.SetFloat("_LODGroup0Percent", lodGroup0);
        grassMaterial2.SetFloat("_LODGroup1Percent", lodGroup1);
        grassMaterial2.SetFloat("_LODCutoff", lodCutoff);

        grassMaterial3.SetFloat("_Rotation", -rotation);
        grassMaterial3.SetFloat("_Protrusion", protrusion);
        grassMaterial3.SetFloat("_LowGrassAnimationSpeed", lowGrassAnimationSpeed);
        grassMaterial3.SetFloat("_HighGrassAnimationSpeed", highGrassAnimationSpeed);
        grassMaterial3.SetVector("_WindDir", windDirection);
        grassMaterial3.SetFloat("_DisplacementStrength", displacementStrength);
        grassMaterial3.SetFloat("_CullingBias", cullingBias);
        grassMaterial3.SetFloat("_LODGroup0Percent", lodGroup0);
        grassMaterial3.SetFloat("_LODGroup1Percent", lodGroup1);
        grassMaterial3.SetFloat("_LODCutoff", lodCutoff);

        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial,
            new Bounds(Vector3.zero, new Vector3(grassFieldSize, 10f, grassFieldSize)),
            argsBuffer
        );
        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial2,
            new Bounds(Vector3.zero, new Vector3(grassFieldSize, 10f, grassFieldSize)),
            argsBuffer
        );
        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial3,
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

        grassMaterial2.enableInstancing = true;
        grassMaterial2.SetBuffer("grassDataBuffer", grassDataBuffer);

        grassMaterial3.enableInstancing = true;
        grassMaterial3.SetBuffer("grassDataBuffer", grassDataBuffer);

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