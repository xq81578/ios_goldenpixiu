using UnityEngine;
using UnityEditor;
using System.IO;

public class AutoSetTextureFormatTool
{
    [MenuItem("Assets/Auto Set TextureFormat(DXT)", false, 1000)]
    public static void SetTextureFormat2DXT()
    {
        SetTextureFormat(TextureImporterFormat.DXT5Crunched, TextureImporterFormat.DXT1Crunched);
    }

    [MenuItem("Assets/Auto Set TextureFormat(ASTC)", false, 1000)]
    public static void SetTextureFormat2ASTC()
    {
        SetTextureFormat(TextureImporterFormat.ASTC_6x6, TextureImporterFormat.DXT1Crunched);
    }

    public static void SetTextureFormat(TextureImporterFormat alphaTextureFormat = TextureImporterFormat.DXT5Crunched, TextureImporterFormat opaqueTextureFormat = TextureImporterFormat.DXT1Crunched)
    {
        if (!SetTextureFormatValidate())
        {
            Debug.LogWarning("請選擇有效的資料夾或圖片檔案。");
            return;
        }

        // 取得選定的資源路徑
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path))
            {
                ProcessFolder(path, alphaTextureFormat, opaqueTextureFormat);
            }
            else
            {
                ProcessFile(path, alphaTextureFormat, opaqueTextureFormat);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("自動設定貼圖格式完成。");
    }

    public static bool SetTextureFormatValidate()
    {
        // 檢查是否至少選中了資料夾
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path))
            {
                return true;
            }
            else if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static void ProcessFolder(string folderPath, TextureImporterFormat alphaTextureFormat = TextureImporterFormat.DXT5Crunched, TextureImporterFormat opaqueTextureFormat = TextureImporterFormat.DXT1Crunched)
    {
        // 取得資料夾下所有的 .png 和 .jpg 檔案
        string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            ProcessFile(file, alphaTextureFormat, opaqueTextureFormat);
        }
    }

    private static void ProcessFile(string file, TextureImporterFormat alphaTextureFormat = TextureImporterFormat.DXT5Crunched, TextureImporterFormat opaqueTextureFormat = TextureImporterFormat.DXT1Crunched)
    {
        if (file.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith(".spriteatlasv2", System.StringComparison.OrdinalIgnoreCase))
        {
            string assetPath = file.Replace("\\", "/");
            int assetsIndex = assetPath.IndexOf("Assets/");
            if (assetsIndex >= 0)
            {
                assetPath = assetPath.Substring(assetsIndex);
                Debug.Log(AssetImporter.GetAtPath(assetPath));
                TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (textureImporter != null)
                {
                    // 設定貼圖壓縮為高品質壓縮
                    textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                    textureImporter.crunchedCompression = true; // 啟用 Crunched 壓縮

                    TextureImporterFormat textureFormat = TextureImporterFormat.Automatic;

                    // 根據檔案類型設定貼圖格式
                    if (file.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase))
                    {
                        textureFormat = opaqueTextureFormat;
                    }
                    else if (file.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    {
                        textureFormat = alphaTextureFormat;
                    }

                    // 若為 DXT 系列格式，檢查長寬是否為 4 的倍數 (DXT 壓縮區塊尺寸 4x4)
                    if (IsDXTFormat(textureFormat))
                    {
                        // 先強制讀取原始貼圖，以防還沒被匯入時資訊不正確
                        Texture2D sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                        if (sourceTex != null)
                        {
                            int w = sourceTex.width;
                            int h = sourceTex.height;
                            if ((w % 4) != 0 || (h % 4) != 0)
                            {
                                EditorUtility.DisplayDialog("DXT 尺寸錯誤", $"貼圖 {assetPath} 尺寸為 {w}x{h}，DXT 壓縮要求寬與高皆為 4 的倍數。請調整後再試。", "確定");
                                return; // 直接跳過，不進行後續設定
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"無法載入貼圖以檢查尺寸：{assetPath}");
                        }
                    }

                    // 設定 WebGL 平台的貼圖導入設定
                    TextureImporterPlatformSettings webGLSettings = new()
                    {
                        overridden = true,
                        maxTextureSize = textureImporter.maxTextureSize,
                        format = textureFormat,
                        compressionQuality = 100,
                        name = "WebGL"
                    };
                    textureImporter.SetPlatformTextureSettings(webGLSettings);

                    // 應用更改
                    textureImporter.SaveAndReimport();
                }
                else
                {
                    Debug.LogWarning($"無法取得資源的 TextureImporter：{assetPath}");
                }
            }
            else
            {
                Debug.LogWarning($"無法處理檔案：{file}");
            }
        }
    }

    private static bool IsDXTFormat(TextureImporterFormat format)
    {
        return format == TextureImporterFormat.DXT1 ||
               format == TextureImporterFormat.DXT1Crunched ||
               format == TextureImporterFormat.DXT5 ||
               format == TextureImporterFormat.DXT5Crunched;
    }
}
