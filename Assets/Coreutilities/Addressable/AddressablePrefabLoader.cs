using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;

public class AddressablePrefabLoader : MonoBehaviour
{
    [Tooltip("是否為動態加載")]
    public bool DynamicLoad = false;
    [Tooltip("Prefab的AssetReference")]
    public AssetReference PrefabReference;
    [Tooltip("生成節點")]
    public Transform Parent;
    [Tooltip("加載完成後的回調事件")]
    public UnityEvent<GameObject> OnPrefabLoaded;
    private GameObject _prefabInstance;
    private bool _isLoadPrefab = false;

    private void Start()
    {
        if (DynamicLoad) return;

        LoadPrefab();
    }

    private async void LoadPrefab()
    {
        if (_isLoadPrefab) return;
        _isLoadPrefab = true;

        var prefab = await SourceManager.Load<GameObject>(PrefabReference);

        if (prefab != null)
        {
            _prefabInstance = Instantiate(prefab, Parent.position, Parent.rotation, Parent);
            _prefabInstance.name = prefab.name;
            OnPrefabLoaded?.Invoke(_prefabInstance);
        }
        else
        {
            LogUtils.LogError($"[AddressablePrefabLoader] prefab 載入失敗: {PrefabReference}");
        }
    }

    public void DynamicLoadPrefab(Action<GameObject> callback)
    {
        OnPrefabLoaded.AddListener(callback.Invoke);
        LoadPrefab();
    }

    public void ReleaseAsset()
    {
        if (_prefabInstance != null)
        {
            PrefabReference.ReleaseAsset();
            Destroy(_prefabInstance);
            _prefabInstance = null;
        }
    }
}
