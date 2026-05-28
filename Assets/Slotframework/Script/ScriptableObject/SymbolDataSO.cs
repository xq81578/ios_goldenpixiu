using System;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[CreateAssetMenu(fileName = "SymbolData", menuName = "ScriptableObjects/SymbolData/BaseSymbolData", order = 0)]
public class MySymbolDataSO : SymbolDataSO<SymbolData>
{
    // Add your custom code here
}

public abstract class SymbolDataSOBase : ScriptableObject
{
    public abstract SymbolData GetSymbolData(int id);
    public abstract SymbolData GetSymbolData(string name);
}

public class SymbolDataSO<T> : SymbolDataSOBase where T : SymbolData
{
    [SerializeField]
    private List<T> _symbols;
    public List<T> Symbols => _symbols;
    protected Dictionary<string, T> _symbolDataMap;
    protected Dictionary<int, T> _symbolDataMapById;

    protected virtual void OnEnable()
    {
        _symbolDataMap = new Dictionary<string, T>();
        _symbolDataMapById = new Dictionary<int, T>();
        foreach (T data in _symbols)
        {
            _symbolDataMap[data.Name] = data;
            _symbolDataMapById[data.Id] = data;
            data.InitSymbolSpine();
        }
    }

    public override SymbolData GetSymbolData(int id)
    {
        return _symbolDataMapById.TryGetValue(id, out T data) ? data : null;
    }

    public override SymbolData GetSymbolData(string name)
    {
        return _symbolDataMap.TryGetValue(name, out T data) ? data : null;
    }

#if UNITY_EDITOR
    [Button]
    private void RemoveAllSymbolSpines()
    {
        foreach (T data in _symbols)
        {
            data.SymbolSpine = null;
        }
    }
#endif
}

[Serializable]
public class SymbolData
{
    public int Id;
    public string Name;
    [PreviewField]
    public Sprite NormalSprite;
    [PreviewField]
    public Sprite BackgroundSprite;
    public SkeletonDataAsset SymbolSpine;
    public AssetReference SymbolSpineAssetReference;

    public void InitSymbolSpine()
    {
        if (SymbolSpine == null && !string.IsNullOrEmpty(SymbolSpineAssetReference.AssetGUID))
        {
            var handle = SymbolSpineAssetReference.LoadAssetAsync<SkeletonDataAsset>();
            handle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    SymbolSpine = op.Result;
                }
            };
        }
    }
}
