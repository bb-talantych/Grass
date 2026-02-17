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

    [Range(0, 5)]
    public float offsetY = 0.5f;

    public Mesh grassMesh;
    public Material grassMaterial;
    public ComputeShader grassComputeShader;

    private ComputeBuffer grassDataBuffer;
    private ComputeBuffer argsBuffer;

    private int lastFieldSize, lastDensity;

    void Start()
    {
        RegenerateGrass();
    }

    void Update()
    {
        if(lastDensity != grassDensity || lastFieldSize != grassFieldSize)
        {
            RegenerateGrass();
        }

        grassMaterial.SetFloat("_OffsetY", offsetY);

        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial,
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
        grassMaterial.SetBuffer("positionBuffer", grassDataBuffer);

        uint[] args = new uint[5]
        {
            grassMesh.GetIndexCount(0),
            (uint)totalInstances,
            0, 0, 0
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