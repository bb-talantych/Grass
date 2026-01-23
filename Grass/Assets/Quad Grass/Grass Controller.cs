using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassController : MonoBehaviour
{
    [Range(1f, 1000f)]
    public int sizeX = 300;
    [Range(1f, 1000f)]
    public int sizeZ = 300;

    public GameObject grassPrefab;

    void Start()
    {

        for(int x = 0; x < sizeX; x++) 
        {
            for (int z = 0; z < sizeX; z++)
            {
                Vector3 grassPos = new Vector3(x - sizeX/2, 0, z - sizeZ / 2);
                GameObject grass = Instantiate(grassPrefab, grassPos, Quaternion.identity);
                grass.transform.parent = transform;
            }
        }
    }
}
