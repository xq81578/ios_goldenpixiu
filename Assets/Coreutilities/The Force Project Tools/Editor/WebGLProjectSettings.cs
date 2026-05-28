using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class WebGLProjectSettings
{
    [MenuItem("Tools/Project Setting/设置构建WebGL 参数")]
    public static void ApplySettings()
    {
        // 切換平台 (如果還不是 WebGL)
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }

        var buildTargetGroup = BuildTargetGroup.WebGL;
        var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);

        // --- Player → Other Settings ---
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
        PlayerSettings.SetApiCompatibilityLevel(namedBuildTarget, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.SetIl2CppCodeGeneration(namedBuildTarget, Il2CppCodeGeneration.OptimizeSpeed);
        PlayerSettings.SetIl2CppCompilerConfiguration(namedBuildTarget, Il2CppCompilerConfiguration.Release);

        // --- Optimization ---
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(namedBuildTarget, ManagedStrippingLevel.High);

        // --- Publishing Settings ---
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;

        // --- WebAssembly ---
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
#if UNITY_2023_2_OR_NEWER
        PlayerSettings.WebGL.wasm2023 = true;
#endif

        Debug.Log("✅ WebGL Project Settings 已套用完成");
    }
}
