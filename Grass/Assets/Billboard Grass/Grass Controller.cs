using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassController : MonoBehaviour
{
    [Header("Generation Properties")]
    [Range(1, 1000)]
    public int grassFieldSize = 300;
    [Range(1, 5)]
    public int grassDensity = 5;
    [Range(0, 5)]
    public float displacementStrength = 1;

    [Header("Shader Properties")]
    [Range(0, 360)]
    public float rotation = 45f;
    [Range(0, 0.2f)]
    public float protrusion = 0.1f;

    [Range(0, 4f)]
    public float animationSpeed = 1.0f;

    public Mesh grassMesh;
    public Material grassMaterial;
    public ComputeShader grassComputeShader;

    public Material grassMaterial2, grassMaterial3;

    public Texture2D heightTex;

    private int kernelIndex, threadGroups;
    private ComputeBuffer grassDataBuffer, argsBuffer;


    void Start()
    {
        grassMaterial.SetVector("_ProtrusionDir", Vector3.back);
        grassMaterial2.SetVector("_ProtrusionDir", Vector3.forward);
        grassMaterial3.SetVector("_ProtrusionDir", Vector3.forward);

        GenerateGrass();
    }

    void Update()
    {
        grassComputeShader.SetFloat("_DisplacementStrength", displacementStrength);
        grassComputeShader.SetTexture(kernelIndex, "_HeightMap", heightTex);
        grassComputeShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

        grassMaterial.SetFloat("_Protrusion", protrusion);
        grassMaterial.SetFloat("_AnimationSpeed", animationSpeed);

        grassMaterial2.SetFloat("_Rotation", rotation);
        grassMaterial2.SetFloat("_Protrusion", protrusion);
        grassMaterial2.SetFloat("_AnimationSpeed", animationSpeed);

        grassMaterial3.SetFloat("_Rotation", -rotation);
        grassMaterial3.SetFloat("_Protrusion", protrusion);
        grassMaterial3.SetFloat("_AnimationSpeed", animationSpeed);

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

        grassDataBuffer = new ComputeBuffer(totalInstances, sizeof(float) * 3);

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