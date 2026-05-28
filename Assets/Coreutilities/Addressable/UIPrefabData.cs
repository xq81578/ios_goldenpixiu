using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

[Serializable]
public class UIPrefabData
{
    public AssetReference PrefabReference;
    public bool Dynamic = false;
    public GameObject Parent = null;

    [NonSerialized]
    public GameObject LoadedUI = null;
    public GameObjectUnityEvent LoadedEvent;
    private string _assetName;

    public void TriggerEvent(GameObject LoadedObj)
    {
        LoadedEvent?.Invoke(LoadedObj);
    }

    public bool IsLoaded()
    {
        return LoadedUI != null;
    }

    public async UniTask LoadUI(Action<AssetReference, GameObject> onLoaded, Transform defaultParent)
    {
        GameObject prefab = await SourceManager.Load<GameObject>(PrefabReference);

        if (prefab == null)
        {
            LogUtils.LogError("Can't LoadUI Prefab:" + PrefabReference);
            return;
        }

        _assetName = prefab.name;
        LogUtils.Log("LoadUI Prefab:" + _assetName);

        Transform parent = Parent != null ? Parent.transform : defaultParent;
        GameObject _UI = UnityEngine.Object.Instantiate(prefab, parent);
        _UI.name = _assetName;
        LoadedUI = _UI;
        onLoaded?.Invoke(PrefabReference, LoadedUI);
        TriggerEvent(LoadedUI);
    }
}
