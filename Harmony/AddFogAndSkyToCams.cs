using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public static class AddFogAndSkyToCams
{

    static FieldInfo fieldResources = AccessTools.Field(typeof(PostProcessLayer), "m_Resources");

    public static void PatchCamera(Camera cam)
    {
        var old = Camera.main.GetComponent<PostProcessLayer>();
        var layer = cam.gameObject.GetOrAddComponent<PostProcessLayer>();
        layer?.Init(fieldResources.GetValue(old) as PostProcessResources);
    }

    [HarmonyPatch(typeof(XUiC_CameraWindow), "CreateCamera")]
    public class XUiC_CameraWindow_CreateCamera_Patch
    {
        static void Postfix(Camera ___sensorCamera)
        {
            if (Camera.main == null) return;
            if (___sensorCamera == null) return;
            if (___sensorCamera.gameObject == null) return;
            ___sensorCamera.cullingMask |= 512; // Add back sky and clounds
            PatchCamera(___sensorCamera); // fog and other post processing
        }
    }

}
