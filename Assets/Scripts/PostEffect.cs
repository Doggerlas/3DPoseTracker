/*实现应用后期处理效果
 * 该文件主要功能是在Unity中应用后期处理效果。
 * 它包括一个Shader变量和一个Material变量，用于定义后期处理效果。
 * 在Start()函数中，创建了一个新的Material实例。如果不存在 Material，Update()函数将创建一个新的Material实例。
 * 最后，在OnRenderImage()函数中，使用Graphics.Blit()函数来应用定义的后期处理效果。
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PostEffect : MonoBehaviour
{
    public Shader shader;
    public Material mat;

    void Start()
    {
        this.mat = new Material(this.shader);
    }

    private void Update()
    {
        if(mat == null)
        {
            this.mat = new Material(this.shader);
        }
    }
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, dest, this.mat);
    }
}
