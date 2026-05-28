using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed class UIPrefabLoader : UIPrefabLoaderBase
{
    [SerializeField, Searchable]
    private List<UIPrefabData> m_LoadList;

    void Start()
    {
        CorLoadUI();
    }

    private async void CorLoadUI()
    {
        for (int i = 0; i < m_LoadList.Count; i++)
        {
            await LoadUI(m_LoadList[i]);
        }

        // 沒有 加載的話直接呼叫完成
        TriggerWholeUILoadedEvent();
    }
}
