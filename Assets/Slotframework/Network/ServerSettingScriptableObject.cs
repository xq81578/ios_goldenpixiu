using System;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ServerSetting", menuName = "ScriptableObjects/ServerSetting", order = 2)]
public class ServerSettingScriptableObject : ScriptableObject
{
    [FoldoutGroup("GameServer")]
    public string DevWebSocketUri = "";
    [FoldoutGroup("GameServer")]
    public string UATWebSocketUri = "";



    [FoldoutGroup("GameServer")]
    public string ProdWebSocketUri = "";

    [InfoBox("重連設定(次數)"), FoldoutGroup("基礎設定")]
    public int ReconnectMaxCount = 5;
    [InfoBox("重連間隔時間(秒)"), FoldoutGroup("基礎設定")]
    public int ReconnectUnitTime = 2;

    [FoldoutGroup("GameServer")]
    [OnValueChanged(nameof(OnOtherServersChanged), IncludeChildren = true)]
    public OtherServer[] OtherServers;
    // 用來記住上一次的 IsEnable 狀態，用於判斷誰是「剛被打開」的那個
    private bool[] _oldStates;

    private void OnEnable()
    {
        // ScriptableObject 重新加載時，把 _oldStates 初始化
        if (OtherServers != null)
        {
            _oldStates = new bool[OtherServers.Length];
            for (int i = 0; i < OtherServers.Length; i++)
            {
                _oldStates[i] = OtherServers[i].Enabled;
            }
        }
    }

    private void OnOtherServersChanged()
    {
        // 如果陣列還沒初始化或大小改變，重新組態
        if (_oldStates == null || _oldStates.Length != OtherServers.Length)
        {
            _oldStates = new bool[OtherServers.Length];
            for (int i = 0; i < OtherServers.Length; i++)
            {
                _oldStates[i] = OtherServers[i].Enabled;
            }
            return;
        }

        // 尋找本次操作中「哪個索引剛從 false → true」
        int newlyEnabledIndex = -1;
        for (int i = 0; i < OtherServers.Length; i++)
        {
            // 如果跟舊狀態不一樣，代表它剛被切換
            if (OtherServers[i].Enabled != _oldStates[i])
            {
                // 如果是從 false -> true，記下這個索引
                if (_oldStates[i] == false && OtherServers[i].Enabled == true)
                {
                    newlyEnabledIndex = i;
                }
            }
        }

        // 如果本次有人「剛被打開」
        if (newlyEnabledIndex != -1)
        {
            // 把其他都設為 false，只留 newlyEnabledIndex 為 true
            for (int i = 0; i < OtherServers.Length; i++)
            {
                if (i != newlyEnabledIndex)
                {
                    OtherServers[i].Enabled = false;
                }
            }
        }

        // 最後，更新 _oldStates
        for (int i = 0; i < OtherServers.Length; i++)
        {
            _oldStates[i] = OtherServers[i].Enabled;
        }
    }
}

[Serializable]
public class OtherServer
{
    public bool Enabled = false;
    public string ServerName = "";
    public string WebSocketUri = "";
}