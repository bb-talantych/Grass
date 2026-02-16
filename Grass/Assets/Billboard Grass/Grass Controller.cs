using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassController : MonoBehaviour
{
    [Range(1f, 1000f)]
    public int grassSize = 300;

    public GameObject grassPrefab;

    void Start()
    {
        for(int i = 0; i < grassSize; i++) 
        {
            GameObject grass = Instantiate(grassPrefab);
            grass.transform.parent = transform;
        }
    }
}
