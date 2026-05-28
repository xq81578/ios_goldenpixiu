using UnityEngine;
using System.Collections.Generic;
using System.Web;

public static class URLParameter
{
    // 取得單一 URL 參數值
    public static string GetURLParameter(string key)
    {
        Dictionary<string, string> parameters = GetAllURLParameters();
        return parameters.ContainsKey(key) ? parameters[key] : null;
    }

    // 取得所有 URL 參數，並回傳 Dictionary
    public static Dictionary<string, string> GetAllURLParameters()
    {
        string url = Application.absoluteURL;
        Dictionary<string, string> paramDict = new();

        if (string.IsNullOrEmpty(url) || !url.Contains("?"))
            return paramDict;

        string queryString = url.Split('?')[1]; // 取得 `?` 之後的部分
        string[] parameters = queryString.Split('&'); // 拆分不同參數

        foreach (string param in parameters)
        {
            string[] keyValue = param.Split('=');
            if (keyValue.Length == 2)
            {
                paramDict[keyValue[0]] = HttpUtility.UrlDecode(keyValue[1]); // 解碼 URL 參數值
            }
        }

        return paramDict;
    }

    // 檢查是否包含某個參數
    public static bool HasParameter(string key)
    {
        return GetAllURLParameters().ContainsKey(key);
    }
}
