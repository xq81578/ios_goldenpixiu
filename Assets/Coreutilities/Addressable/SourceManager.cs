using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class SourceManager
{
    private static SourceManager _instance;
    
    public static SourceManager Instance
    {
        get
        {
            _instance ??= new SourceManager();
            return _instance;
        }
    }

    private Dictionary<string, AsyncOperationHandle> handles = new();
    private AsyncOperationHandle<SceneInstance> _loadHandle;
    private AssetReference _sceneReference;
    private bool _isLoadingScene = false; // 是否正在載入場景
    private int _maxConcurrent = 20;

    private SourceManager()
    {
        WebRequestQueue.SetMaxConcurrentRequests(_maxConcurrent);  // 設定最大同時下載數量
    }

    public async UniTask PreLoadScene(AssetReference sceneReference, Action<float> onProgress = null)
    {
        _sceneReference = sceneReference;
#if UNITY_EDITOR
        // EDITOR 不作用，因為會直接切場景
        await UniTask.Yield();
        return;
#else
        // 先估總量（0 代表全在快取或伺服器沒提供 Content-Length）
        var sizeHandle = Addressables.GetDownloadSizeAsync(sceneReference);
        await sizeHandle.Task;
        long expectedTotal = sizeHandle.Result;

        LogUtils.Log($"PreLoadScene: {sceneReference} 估總量: {expectedTotal}");

        Addressables.Release(sizeHandle);

        if (_isLoadingScene)
            return;
        _isLoadingScene = true;

        _loadHandle = Addressables.LoadSceneAsync(sceneReference, LoadSceneMode.Single, activateOnLoad: false);

        //下載 / 解包階段（映射到 0 % ~90 %）
        while (!_loadHandle.IsDone)
        {
            var st = _loadHandle.GetDownloadStatus(); // 彙整子作業（bundle）
            long total = st.TotalBytes > 0 ? (long)st.TotalBytes : expectedTotal;
            long downloaded = (long)st.DownloadedBytes;

            float downloadingPct;
            if (total > 0)
                downloadingPct = UnityEngine.Mathf.Clamp01((float)downloaded / total);
            else
                downloadingPct = _loadHandle.PercentComplete; // 後備：無法取得 bytes 時

            onProgress?.Invoke(downloadingPct);

            await UniTask.Yield();
        }

        _isLoadingScene = false;

        await _loadHandle;
#endif
    }

    public async UniTask<bool> ActivateScene(Action<float> onProgress = null)
    {
#if UNITY_EDITOR
        // EDITOR 切場景
        await ChangeScene(_sceneReference, onProgress);
        return true;
#else
        if (_loadHandle.IsValid())
        {
            var activateOp = _loadHandle.Result.ActivateAsync(); // 激活 → 真正切換場景
            while (!activateOp.isDone)
            {
                float p = activateOp.progress;
                onProgress?.Invoke(p);
                await UniTask.Yield();
            }
            onProgress?.Invoke(1f);
            return true;
        }

        return false;
#endif
    }


    public async UniTask<bool> ChangeScene(AssetReference sceneReference, Action<float> onProgress = null)
    {
        bool isSuccess = false;
        if (_isLoadingScene)
            return isSuccess;

        _isLoadingScene = true;

        var handle = Addressables.LoadSceneAsync(sceneReference, LoadSceneMode.Single);

        // 監控場景加載進度
        while (!handle.IsDone)
        {
            onProgress?.Invoke(handle.PercentComplete);
            await UniTask.Yield();
        }

        // 確保進度到達100%
        onProgress?.Invoke(1f);
        _isLoadingScene = false;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            LogUtils.Log($"Scene:{sceneReference} 載入成功！");
            isSuccess = true;
        }
        else
        {
            LogUtils.LogError($"Scene:{sceneReference} 載入失敗！");
        }

        return isSuccess;
    }

    public static async UniTask<T> Load<T>(string key) where T : UnityEngine.Object
    {
        if (Instance.handles.TryGetValue(key, out var handle))
        {
            return handle.Result as T;
        }

        var newHandle = Addressables.LoadAssetAsync<T>(key);
        await newHandle.Task;

        if (newHandle.Status == AsyncOperationStatus.Succeeded)
        {
            if (newHandle.Result is T result)
            {
                Instance.handles[key] = newHandle;
                return result;
            }
            else
            {
                LogUtils.LogError($"[SourceManager] Type mismatch for key: {key}. Got: {newHandle.Result?.GetType()} Expected: {typeof(T)}");
            }
        }

        LogUtils.LogError($"[SourceManager] Failed to load asset with key: {key}");
        return null;
    }

    public static async UniTask<T> Load<T>(AssetReference asset) where T : UnityEngine.Object
    {
        var newHandle = asset.LoadAssetAsync<T>();

        await newHandle.Task;

        if (newHandle.Status == AsyncOperationStatus.Succeeded)
        {
            if (newHandle.Result is T result)
            {
                return result;
            }
            else
            {
                LogUtils.LogError($"[SourceManager] Type mismatch for key: {asset}. Got: {newHandle.Result?.GetType()} Expected: {typeof(T)}");
            }
        }

        LogUtils.LogError($"[SourceManager] Failed to load asset with key: {asset}");
        return null;
    }

    public static void Unload(string key)
    {
        if (Instance.handles.TryGetValue(key, out var handle))
        {
            Addressables.Release(handle);
            Instance.handles.Remove(key);
        }
    }

    public static void UnloadAll()
    {
        foreach (var handle in Instance.handles.Values)
            Addressables.Release(handle);

        Instance.handles.Clear();
    }

    public static async UniTask DownloadAssetBundle(string keyOrLabel, Action<float> onProgress = null)
    {
        var downloadHandle = Addressables.DownloadDependenciesAsync(keyOrLabel);
        while (!downloadHandle.IsDone)
        {
            onProgress?.Invoke(downloadHandle.PercentComplete);
            await UniTask.Yield();
        }

        if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            LogUtils.Log($"[SourceManager] AssetBundle for {keyOrLabel} downloaded.");
            downloadHandle.Release(); // 釋放下載的資源
        }
        else
        {
            LogUtils.LogError($"[SourceManager] Failed to download bundle: {keyOrLabel}");
        }
    }
}
