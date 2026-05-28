#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BootstrapWebSceneCreator
{
    private const string ScenePath = "Assets/Slot001_GoldenPixiu/Scene/Slot001_BootstrapWeb.unity";
    private const string WebViewPrefabPath = "Assets/Coreutilities/Utility/Prefabs/WebViewController.prefab";
    private const string LoadingScenePath = "Assets/Slot001_GoldenPixiu/Scene/Slot001_LoadingScene.unity";

    [MenuItem("Tools/GoldenPixiu/Setup Bootstrap Web Scene (Scheme A)")]
    public static void SetupBootstrapWebScene()
    {
        CreateOrUpdateBootstrapScene();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Bootstrap Web",
            "Bootstrap scene created/updated and set as the first scene in Build Settings.\n\n" +
            "First scene: Slot001_BootstrapWeb\n" +
            "Then: Slot001_LoadingScene",
            "OK");
    }

    private static void CreateOrUpdateBootstrapScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();

        var cameraGo = new GameObject("BootstrapCamera");
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Depth;
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        camera.transform.position = new Vector3(0, 0, -10);
        cameraGo.AddComponent<AudioListener>();

        var canvasGo = new GameObject("BootstrapCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100;
        canvas.sortingOrder = 1000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var webViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WebViewPrefabPath);
        if (webViewPrefab == null)
        {
            Debug.LogError($"[BootstrapWeb] WebView prefab not found: {WebViewPrefabPath}");
            return;
        }

        PrefabUtility.InstantiatePrefab(webViewPrefab, canvasGo.transform);

        var gateGo = new GameObject("StartupWebGate");
        gateGo.AddComponent<StartupWebGate>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[BootstrapWeb] Scene saved: {ScenePath}");
    }

    private static void UpdateBuildSettings()
    {
        var bootstrap = new EditorBuildSettingsScene(ScenePath, true);
        var loading = new EditorBuildSettingsScene(LoadingScenePath, true);

        var existing = EditorBuildSettings.scenes.ToList();
        existing.RemoveAll(s =>
            s.path == ScenePath ||
            s.path == LoadingScenePath);

        var ordered = new List<EditorBuildSettingsScene> { bootstrap, loading };
        foreach (var scene in existing)
        {
            if (scene.enabled)
                ordered.Add(scene);
        }

        EditorBuildSettings.scenes = ordered.ToArray();
        Debug.Log("[BootstrapWeb] Build Settings updated. Bootstrap scene is index 0.");
    }
}
#endif
