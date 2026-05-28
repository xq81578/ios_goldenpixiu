using Sirenix.OdinInspector;
using UnityEngine;

public class GlobalSettingManager : Singleton<GlobalSettingManager>
{
    [SerializeField]
    private GlobalSettings _globalSettings;
    public GlobalSettings GlobalSettings => _globalSettings;

    private void Start()
    {
        LoadSettings();
        ApplySettings();
    }

    [Button]
    public void ApplySettings()
    {
        LogUtils.Log("ApplySettings");
        // Example
        // Screen.SetResolution(_globalSettings.ScreenWidth, _globalSettings.ScreenHeight, _globalSettings.FullScreen, _globalSettings.FPS);
        // AudioListener.volume = _globalSettings.MasterVolume;
        // 可以根據需求在這裡設置更多
        QualitySettings.vSyncCount = 0;   // 把垂直同步關掉
        if (SystemInfo.deviceType == DeviceType.Handheld)
        {
            Application.targetFrameRate = GlobalSettings.MobileFPS;
        }
        else if (SystemInfo.deviceType == DeviceType.Desktop)
        {
            Application.targetFrameRate = GlobalSettings.DesktopFPS;
        }
    }

    // 保存設置到 PlayerPrefs 或其他持久化存儲
    [Button]
    public void SaveSettings()
    {
        LogUtils.Log("SaveSettings");
        // PlayerPrefs.SetInt("FullScreen", _globalSettings.FullScreen ? 1 : 0);
        // PlayerPrefs.SetFloat("MasterVolume", _globalSettings.MasterVolume);
        // PlayerPrefs.Save();
    }

    [Button]
    // 從持久化存儲中加載設置
    public void LoadSettings()
    {
        LogUtils.Log("LoadSettings");
        // if (PlayerPrefs.HasKey("FullScreen"))
        // {
        //     _globalSettings.FullScreen = PlayerPrefs.GetInt("FullScreen") == 1;
        // }
        // if (PlayerPrefs.HasKey("MasterVolume"))
        // {
        //     _globalSettings.MasterVolume = PlayerPrefs.GetFloat("MasterVolume");
        // }
    }
}
