using System.Runtime.InteropServices;
using UnityEngine;

public static class WebGLPageReloader
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReloadPage();

    [DllImport("__Internal")]
    private static extern void GoBackPage();
#endif

    public static void RefreshPage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ReloadPage();
#else
        Debug.LogWarning("RefreshPage only works in WebGL build, not in Editor or other platforms.");
#endif
    }

    public static void BackToPreviousPage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GoBackPage();
#else
        Debug.LogWarning("BackToPreviousPage only works in WebGL build, not in Editor or other platforms.");
#endif
    }
}
