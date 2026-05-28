using System.Runtime.InteropServices;
using UnityEngine;

public static class BrowserInfo
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern System.IntPtr GetUserAgent();
#endif
    public static string GetUserAgentString()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // 呼叫剛才定義的 JS 函式取得 userAgent
        return Marshal.PtrToStringAnsi(GetUserAgent());
#else
        return null;
#endif
    }

    public static void GetBrowserAndVersion(out string browser, out string browserVer)
    {
        browser = "Unknown";
        browserVer = "Unknown";

        string ua = GetUserAgentString();
        if (string.IsNullOrEmpty(ua))
            return;

        // 以下為簡單解析範例，可根據實際需要更細緻解析
        // 常見userAgent範例：
        // Chrome: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.5845.179 Safari/537.36"
        // Safari: "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1"
        // Firefox: "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:116.0) Gecko/20100101 Firefox/116.0"

        if (ua.Contains("Chrome") && ua.Contains("Safari") && !ua.Contains("Edge"))
        {
            browser = "Chrome";
            // 解析Chrome版本
            // Chrome/116.0.5845.179 Safari/537.36
            int chromeIndex = ua.IndexOf("Chrome/");
            if (chromeIndex != -1)
            {
                int start = chromeIndex + 7; // length of "Chrome/"
                int end = ua.IndexOf(' ', start);
                if (end < 0) end = ua.Length;
                browserVer = ua.Substring(start, end - start);
            }
        }
        else if (ua.Contains("Safari") && ua.Contains("Version") && !ua.Contains("Chrome"))
        {
            browser = "Safari";
            int verIndex = ua.IndexOf("Version/");
            if (verIndex != -1)
            {
                int start = verIndex + 8; // length of "Version/"
                int end = ua.IndexOf(' ', start);
                if (end < 0) end = ua.Length;
                browserVer = ua.Substring(start, end - start);
            }
        }
        else if (ua.Contains("Firefox"))
        {
            browser = "Firefox";
            int ffIndex = ua.IndexOf("Firefox/");
            if (ffIndex != -1)
            {
                int start = ffIndex + 8; // length of "Firefox/"
                int end = ua.IndexOf(' ', start);
                if (end < 0) end = ua.Length;
                browserVer = ua.Substring(start, end - start);
            }
        }
        else if (ua.Contains("Edg/"))
        {
            browser = "Edge";
            int edgeIndex = ua.IndexOf("Edg/");
            if (edgeIndex != -1)
            {
                int start = edgeIndex + 4;
                int end = ua.IndexOf(' ', start);
                if (end < 0) end = ua.Length;
                browserVer = ua.Substring(start, end - start);
            }
        }
        // 可依實際需要增加更多瀏覽器辨識邏輯
    }
}
