using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputeShadertest : MonoBehaviour
{
    public ComputeShader computeShader;
    public RenderTexture renderTexture;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(256, 256, 24);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
        }

        int kernelIndex = 0;
        computeShader.SetTexture(kernelIndex, "Result", renderTexture);
        computeShader.SetFloat("Resolution", renderTexture.width);
        computeShader.Dispatch(kernelIndex, renderTexture.width / 8, renderTexture.height / 8, 1);

        Graphics.Blit(renderTexture, destination);
    }
}
