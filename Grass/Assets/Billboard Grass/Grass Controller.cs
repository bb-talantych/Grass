using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassController : MonoBehaviour
{
    struct GrassData
    {
        public Vector3 position;
    };

    [Range(1f, 1000f)]
    public int grassFieldSize = 300;

    public Transform grassPrefab;
    public ComputeShader grassPosShader;

    void Start()
    {
        int bufferSize = sizeof(float) * 3;
        int arraySize = grassFieldSize * grassFieldSize;

        ComputeBuffer grassBuffer = new ComputeBuffer(arraySize, bufferSize);

        GrassData[] grassDataArray = new GrassData[arraySize];
        grassBuffer.SetData(grassDataArray);

        int kernelIndex = grassPosShader.FindKernel("GetGrassPos");
        grassPosShader.SetBuffer(kernelIndex, "grassDataBuffer", grassBuffer);
        grassPosShader.SetInt("grassFieldSize", grassFieldSize);
        int threadGroups = Mathf.CeilToInt(grassFieldSize / 8.0f);
        grassPosShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

        grassBuffer.GetData(grassDataArray);

        for (int i = 0; i < arraySize; i++)
        {
            Vector3 grassPos = grassDataArray[i].position;

            Debug.Log("Grass number " + i + " is at " + grassPos);
        }


        grassBuffer.Dispose();
    }
}
