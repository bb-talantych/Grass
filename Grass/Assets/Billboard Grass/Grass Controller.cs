using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassController : MonoBehaviour
{
    [Range(1, 1000)]
    public int grassFieldSize = 300;
    [Range(1, 5)]
    public int grassDensity = 5;

    [Range(0, 360)]
    public float rotation = 45f;
    [Range(0, 5)]
    public float protrusion = 0.1f;

    public Mesh grassMesh;
    public Material grassMaterial;
    public ComputeShader grassComputeShader;

    public Material grassMaterial2, grassMaterial3;

    private ComputeBuffer grassDataBuffer;
    private ComputeBuffer argsBuffer;

    private int lastFieldSize, lastDensity;

    void Start()
    {
        grassMaterial.SetVector("_ProtrusionDir", Vector3.back);
        grassMaterial2.SetVector("_ProtrusionDir", Vector3.forward);
        grassMaterial3.SetVector("_ProtrusionDir", Vector3.forward);

        RegenerateGrass();
    }

    void Update()
    {
        if(lastDensity != grassDensity || lastFieldSize != grassFieldSize)
        {
            RegenerateGrass();
        }

        grassMaterial.SetFloat("_Protrusion", protrusion);

        grassMaterial2.SetFloat("_Rotation", rotation);
        grassMaterial2.SetFloat("_Protrusion", protrusion);

        grassMaterial3.SetFloat("_Rotation", -rotation);
        grassMaterial3.SetFloat("_Protrusion", protrusion);

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

    void RegenerateGrass()
    {
        lastFieldSize = grassFieldSize;
        lastDensity = grassDensity;

        grassDataBuffer?.Release();
        argsBuffer?.Release();

        int grassFieldResolution = grassFieldSize * grassDensity;
        int totalInstances = grassFieldResolution * grassFieldResolution;

        grassDataBuffer = new ComputeBuffer(totalInstances, sizeof(float) * 3);

        int kernelIndex = grassComputeShader.FindKernel("GetGrassData");

        grassComputeShader.SetBuffer(kernelIndex, "grassDataBuffer", grassDataBuffer);
        grassComputeShader.SetInt("grassFieldResolution", grassFieldResolution);
        grassComputeShader.SetInt("grassDensity", grassDensity);
        int threadGroups = Mathf.CeilToInt(grassFieldResolution / 8f);
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