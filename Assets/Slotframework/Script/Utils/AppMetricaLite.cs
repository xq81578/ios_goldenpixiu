using System;
using System.Runtime.InteropServices;

public static class AppMetricaLite
{
    public static void Activate(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("AppMetrica API key is empty.", nameof(apiKey));

#if UNITY_IOS && !UNITY_EDITOR
        AppMetricaLiteActivate(apiKey);
#else
        LogUtils.Log("[AppMetricaLite] Activation skipped outside iOS player.");
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void AppMetricaLiteActivate(string apiKey);
#endif
}
