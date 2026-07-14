using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlanarReflectionManager : MonoBehaviour
{
    Camera reflectionCam;
    Camera mainCamera;

    public GameObject reflectionPlane;

    RenderTexture renderTarget;

    public Material floorMaterial;

    [Range(0.0f,1.0f)]
    public float reflectionFactor = 0.5f;

    private void Start()
    {
        GameObject reflectionCameraGO = new GameObject("ReflectionCamera");
        reflectionCam = reflectionCameraGO.AddComponent<Camera>();
        reflectionCam.enabled = false;

        mainCamera = Camera.main;

        renderTarget = new RenderTexture(Screen.width, Screen.height, 24);
    }

    private void Update()
    {
        Shader.SetGlobalFloat("_reflectionFactor", reflectionFactor);
    }

    private void OnPostRender()
    {
        RenderReflection();
    }

    void RenderReflection()
    {
        reflectionCam.CopyFrom(mainCamera);

        Vector3 cameraDirectionWorldSpace = mainCamera.transform.forward;
        Vector3 cameraUpWorldSpace = mainCamera.transform.up;
        Vector3 cameraPositionWorldSpace = mainCamera.transform.position;

        //Transform the vectors to the floor's space
        Vector3 cameraDirectionPlaneSpace = reflectionPlane.transform.InverseTransformDirection(cameraDirectionWorldSpace);
        Vector3 cameraUpPlaneSpace = reflectionPlane.transform.InverseTransformDirection(cameraUpWorldSpace);
        Vector3 cameraPositionPlaneSpace = reflectionPlane.transform.InverseTransformPoint(cameraPositionWorldSpace);

        //Mirror the vectors
        cameraDirectionPlaneSpace.y *= -1.0f;
        cameraUpPlaneSpace.y *= -1.0f;
        cameraPositionPlaneSpace.y *= -1.0f;

        //Transform the vectors back to world space
        cameraDirectionWorldSpace = reflectionPlane.transform.TransformDirection(cameraDirectionPlaneSpace);
        cameraUpWorldSpace = reflectionPlane.transform.TransformDirection(cameraUpPlaneSpace);
        cameraPositionWorldSpace = reflectionPlane.transform.TransformPoint(cameraPositionPlaneSpace);

        //Set camera position and rotation
        reflectionCam.transform.position = cameraPositionWorldSpace;
        reflectionCam.transform.LookAt(cameraPositionWorldSpace + cameraDirectionWorldSpace, cameraUpWorldSpace);

        //Set render target for reflection cam
        reflectionCam.targetTexture = renderTarget;

        //Render the reflection camera
        reflectionCam.Render();

        //Draw full screen quad
        DrawQuad();
    }

    void DrawQuad()
    {
        GL.PushMatrix();

        //Use ground Material to draw the quad
        floorMaterial.SetPass(0);
        floorMaterial.SetTexture("_ReflectionTex", renderTarget);

        GL.LoadOrtho();

        GL.Begin(GL.QUADS);
        GL.TexCoord2(1.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 0.0f);
        GL.TexCoord2(1.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f);
        GL.TexCoord2(0.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 0.0f);
        GL.TexCoord2(0.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 1.0f);
        GL.End();

        GL.PopMatrix();
    }
}
