using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

public class TextureManager : MonoBehaviour
{
    // 範例 建立一個Image 並且下載圖片
    // public Image image;
    // public string url = "https://i.imgur.com/PNy5HVm.png";

    // [Button]
    // public async void DownLoadTexture()
    // {
    //     await LoadUrlTexture2D(url, (Texture2D _Texture, string _Url) =>
    //     {
    //         if (_Texture != null)
    //         {
    //             image.sprite = Sprite.Create(_Texture, new Rect(0, 0, _Texture.width, _Texture.height), new Vector2(0.5f, 0.5f));
    //         }
    //     }, 10).ToUniTask();
    // }

    /// <summary>
    /// 讀取網路圖像
    /// </summary>
    /// <param name="Url">位址</param>
    /// <param name="Event"></param>
    /// <param name="IncludeFile">包含的格式類型 必須是Header樣式 EX: image/png 不填寫則不檢查</param>
    /// <returns></returns>
    public static IEnumerator LoadUrlTexture2D(string Url, Action<Texture2D, string> Event, int timeout = 0, params string[] IncludeFile)
    {
        if (Event == null || string.IsNullOrEmpty(Url))
            yield break;

        using (UnityWebRequest _WebRequest = UnityWebRequestTexture.GetTexture(Url))
        {
            _WebRequest.timeout = timeout; //error returns "Request timeout" when a timeout occurs. No timeout is applied when timeout is set to 0

            yield return _WebRequest.SendWebRequest();

            if (_WebRequest.result == UnityWebRequest.Result.Success)
            {
                if (IncludeFile != null && IncludeFile.Length > 0)
                {
                    Dictionary<string, string> _HeaderDic = _WebRequest.GetResponseHeaders();

                    string _Extension;
                    if (_HeaderDic.TryGetValue("CONTENT-TYPE", out _Extension))
                    {
                        if (!Array.Exists<string>(IncludeFile, _include => _include == _Extension))
                            yield break;
                    }
                }

                Texture2D _LoadedTex = DownloadHandlerTexture.GetContent(_WebRequest);
                if (_LoadedTex != null)
                {
                    _LoadedTex.name = Path.GetFileNameWithoutExtension(Url);
                    Debug.Log("LoadUrlTexture2D File Name : " + _LoadedTex.name);
                }

                if (Event != null)
                    Event.Invoke(_LoadedTex, Url);
            }
            else
            {
                Debug.LogError("LoadUrlTexture2D fail. WebRequestError: " + _WebRequest.error + "  URL: " + Url);
                if (Event != null)
                    Event.Invoke(null, Url);
            }
        }
    }

    public static Texture2D Base64ToTexture2D(string base64)
    {
        try
        {
            byte[] imageBytes = Convert.FromBase64String(base64);
            Texture2D texture = new Texture2D(2, 2); // 初始大小無所謂，會自動調整
            if (texture.LoadImage(imageBytes))
            {
                return texture;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Base64 轉換失敗: " + e.Message);
        }
        return null;
    }
}
