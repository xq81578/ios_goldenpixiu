using System;
using UnityEngine;

public class DeviceInfo
{
    public string manufacturer;
    public string brand;
    public string resolution;
    public string network;
    public string sysType;
    public string sysVer;
    public string browser;
    public string browserVer;
    public string ver;
    public string deviceId;

    public override string ToString()
    {
        return $"Manufacturer: {manufacturer}\n" +  // 裝置製造商
               $"Brand: {brand}\n" +                // 裝置型號
               $"Resolution: {resolution}\n" +      // 裝置解析度
               $"Network: {network}\n" +
               $"System Type: {sysType}\n" +
               $"System Version: {sysVer}\n" +
               $"Browser: {browser}\n" +
               $"Browser Version: {browserVer}\n" +
               $"Version: {ver}" +
               $"Device ID: {deviceId}";
    }
}

[Serializable]
public class GeoData
{
    public string country;
    public string region;
    public string postal;
}

public static class DeviceInfoRetriever
{
    public static DeviceInfo GetDeviceInfo()
    {
        var deviceInfo = new DeviceInfo
        {
            manufacturer = GetManufacturer(),
            brand = SystemInfo.deviceModel,
            resolution = $"{Screen.width}x{Screen.height}",
            network = GetNetworkType(),
            sysType = ParseSystemType(SystemInfo.operatingSystem),
            sysVer = SystemInfo.operatingSystem,
            ver = Application.version,
            deviceId = GetDeviceId()
        };
#if UNITY_WEBGL && !UNITY_EDITOR
        BrowserInfo.GetBrowserAndVersion(out string browser, out string browserVer);
        deviceInfo.browser = browser;
        deviceInfo.browserVer = browserVer;
#else
        deviceInfo.browser = "Null";
        deviceInfo.browserVer = "Null";
#endif
        return deviceInfo;
    }

    private static string GetManufacturer()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var buildClass = new AndroidJavaClass("android.os.Build"))
            {
                return buildClass.GetStatic<string>("MANUFACTURER");
            }
        }
        catch (System.Exception e)
        {
            return "Unknown";
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS 不公開製造商資訊，基本上若為 iOS 裝置通常可判定為 "Apple"
        return "Apple";
#else
        // 非行動裝置或在 Editor 中
        return "Unknown";
#endif
    }

    public static string ParseSystemType(string osString)
    {
        // 優先判斷已知平台
        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer:
                return "iOS";
            case RuntimePlatform.Android:
                return "Android";
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor:
                return "Windows";
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.OSXEditor:
                return "macOS";
            case RuntimePlatform.LinuxPlayer:
                return "Linux";
        }

        // 若 Application.platform 無法準確辨識，再用 osString 做輔助判斷
        osString = osString.ToLowerInvariant();
        if (osString.Contains("windows"))
        {
            return "Windows";
        }
        else if (osString.Contains("mac os") || osString.Contains("osx") || osString.Contains("macos"))
        {
            return "macOS";
        }
        else if (osString.Contains("linux"))
        {
            return "Linux";
        }
        else if (osString.Contains("android"))
        {
            return "Android";
        }
        else if (osString.Contains("ios") || osString.Contains("iphone") || osString.Contains("ipad"))
        {
            return "iOS";
        }

        return "Unknown";
    }

    private static string GetNetworkType()
    {
        return Application.internetReachability switch
        {
            NetworkReachability.NotReachable => "No Network",
            NetworkReachability.ReachableViaCarrierDataNetwork => "Mobile Data",
            NetworkReachability.ReachableViaLocalAreaNetwork => "WIFI",
            _ => "Unknown",
        };
    }

    private static string GetDeviceId()
    {
#if UNITY_EDITOR
        return SystemInfo.deviceUniqueIdentifier + "_EDITOR";

#elif UNITY_ANDROID
        // ANDROID 不建議用 deviceUniqueIdentifier，改用 ANDROID_ID（需特殊權限）
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var contentResolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (var secure = new AndroidJavaClass("android.provider.Settings$Secure"))
            {
                string androidId = secure.CallStatic<string>("getString", contentResolver, "android_id");
                return androidId;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Android device ID fallback: " + e.Message);
            return SystemInfo.deviceUniqueIdentifier;
        }

#elif UNITY_IOS
        // iOS 上會回傳 IDFV（app 同廠商共用），IDFA 需使用 AppTrackingTransparency
        return UnityEngine.iOS.Device.vendorIdentifier;

#elif UNITY_WEBGL
        // WebGL 沒有原生裝置 ID，建議用 PlayerPrefs 製作持久化識別碼
        const string key = "webgl_device_id";
        if (PlayerPrefs.HasKey(key))
        {
            return PlayerPrefs.GetString(key);
        }
        else
        {
            string newId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(key, newId);
            PlayerPrefs.Save();
            return newId;
        }

#else
        // 其他平台（如 Windows/macOS）使用內建識別碼
        return SystemInfo.deviceUniqueIdentifier;
#endif
    }
}
