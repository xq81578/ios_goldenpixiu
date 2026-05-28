using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;

public abstract class UIPrefabLoaderBase : MonoBehaviour
{
    protected List<AssetReference> m_LoadingList = new List<AssetReference>();

    [Space(10)]
    [Header("--------UI LOADED CALL BACK--------")]
    public GameObjectAssetReferenceUnityEvent UILoadedEvent;

    [Space(10)]
    [Header("----WHOLE UI LOADED CALL BACK----")]
    public UnityEvent WholeUILoadedEvent;

    /// <summary>
    /// 是否整體UI已經加載完成
    /// </summary>
    public bool IsWholeUILoaded { get; private set; } = false;

    protected virtual async UniTask LoadUI(UIPrefabData data)
    {
        if (data == null)
            return;

        // 標示為動態加載
        if (data.Dynamic)
            return;

        // 已經加載或加載中
        if (data.IsLoaded())// || IsLoading(data.AssetName))
            return;

        m_LoadingList.Add(data.PrefabReference);

        await data.LoadUI(OnUILoaded, transform);
    }

    protected virtual void OnUILoaded(AssetReference asset, GameObject go)
    {
        m_LoadingList.Remove(asset);
        TriggerLoadedEvent(asset, go);
    }

    public void TriggerLoadedEvent(AssetReference asset, GameObject go)
    {
        UILoadedEvent?.Invoke(asset, go);
    }

    protected void TriggerWholeUILoadedEvent()
    {
        if (m_LoadingList.Count == 0)
        {
            LogUtils.Log("TriggerWholeUILoadedEvent End");
            IsWholeUILoaded = true; // 設置為整體UI已加載完成

            WholeUILoadedEvent?.Invoke();
        }
    }

    public bool IsLoading(AssetReference assetName)
    {
        return m_LoadingList.Contains(assetName);
    }
}
