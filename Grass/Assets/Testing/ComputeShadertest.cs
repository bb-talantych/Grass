using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public struct Cube
{
    public Vector3 position;
    public Color color;
}

public class ComputeShadertest : MonoBehaviour
{
    public ComputeShader computeShader;
    public RenderTexture renderTexture;

    public Mesh mesh;
    public Material material;
    public int count = 50;
    [Range(1, 50000)]
    public int repetitions = 1;

    private List<GameObject> objects;
    private Cube[] data;

    public void CreateCubes()
    {
        objects = new List<GameObject>();

        data = new Cube[count * count];

        for(int i = 0; i < count; i++)
        {
            for(int j = 0; j < count; j++)
            {
                CreateCube(i, j);
            }
        }
    }
    void CreateCube(int x, int y)
    {
        GameObject cube = new GameObject("Cube" + x * count + y, typeof(MeshFilter), typeof(MeshRenderer));
        cube.GetComponent<MeshFilter>().mesh = mesh;
        cube.GetComponent<MeshRenderer>().material = new Material(material);
        float xPos = x - count * 0.5f;
        float yPos = y - count * 0.5f;
        cube.transform.position = new Vector3(xPos, yPos, Random.Range(-0.1f, 0.1f));

        Color color = Random.ColorHSV();
        cube.GetComponent<MeshRenderer>().material.SetColor("_Color", color);

        objects.Add(cube);

        Cube cubeData = new Cube();
        cubeData.position = cube.transform.position;
        cubeData.color = color;

        data[x * count + y] = cubeData;

    }

    private void OnRandomizeGPU()
    {
        int colorSize = sizeof(float) * 4;
        int vecotr3Size = sizeof(int) * 3;
        int totalSize = colorSize + vecotr3Size;    

        ComputeBuffer cubesBuffer = new ComputeBuffer(data.Length, totalSize);
        cubesBuffer.SetData(data);

        int kernel = computeShader.FindKernel("CSMain");
        computeShader.SetBuffer(kernel, "cubes", cubesBuffer);
        computeShader.SetFloat("resolution", data.Length);
        computeShader.SetInt("repetitions", repetitions);
        computeShader.Dispatch(kernel, data.Length / 10, 1, 1);

        cubesBuffer.GetData(data);

        for (int i = 0; i < objects.Count; i++) 
        {
            GameObject obj = objects[i];
            Cube cube = data[i];
            obj.transform.position = cube.position;
            obj.GetComponent<MeshRenderer>().material.SetColor("_Color", cube.color);
        }

        cubesBuffer.Dispose();
    }

    private void OnGUI()
    {
        if(objects == null)
        {
            if(GUI.Button(new Rect(0, 0, 100, 50), "Create"))
            {
                CreateCubes();
            }
        }
        else
        {
            if(GUI.Button(new Rect(100, 0, 100, 50), "Random GPU"))
            {
                OnRandomizeGPU();
            }
        }
    }
}
