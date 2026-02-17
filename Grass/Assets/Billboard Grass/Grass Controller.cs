using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassController : MonoBehaviour
{
    [Range(1, 1000)]
    public int grassFieldSize = 300;
    public Mesh grassMesh;
    public Material grassMaterial;
    public ComputeShader grassComputeShader;

    private ComputeBuffer grassBuffer;
    private ComputeBuffer argsBuffer;
    private int totalInstances;

    void Start()
    {
        totalInstances = grassFieldSize * grassFieldSize;
        grassBuffer = new ComputeBuffer(totalInstances, sizeof(float) * 3);
        Vector3[] dummy = new Vector3[totalInstances];
        grassBuffer.SetData(dummy);

        int kernelIndex = grassComputeShader.FindKernel("GetGrassData");
        grassComputeShader.SetBuffer(kernelIndex, "grassDataBuffer", grassBuffer);
        grassComputeShader.SetInt("grassFieldSize", grassFieldSize);

        int threadGroups = Mathf.CeilToInt(grassFieldSize / 8f);
        grassComputeShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

        grassMaterial.enableInstancing = true;
        grassMaterial.SetBuffer("positionBuffer", grassBuffer);

        uint[] args = new uint[5]
        {
            grassMesh.GetIndexCount(0),
            (uint)totalInstances,
            0, 0, 0
        };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    void Update()
    {
        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial,
            new Bounds(Vector3.zero, new Vector3(grassFieldSize, 10f, grassFieldSize)),
            argsBuffer
        );
    }

    void OnDestroy()
    {
        grassBuffer?.Dispose();
        argsBuffer?.Dispose();
    }
}