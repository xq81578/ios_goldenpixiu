using UnityEditor;
using UnityEngine;

/// <summary>
/// 依 <see cref="BuildScript.BuildTypeEnum"/> 記錄「上次上傳成功」時使用的 bundle 版本號（與 BuildWindow / 構建時 PlayerSettings.bundleVersion 一致），
/// 供下次打開構建視窗時建議「版本號 +1」。
/// </summary>
public static class BuildBundleVersionHistory
{
    private const string EditorPrefsKeyPrefix = "TheForceTools.BuildBundle.LastUploadedVersion.";

    public static void SaveLastUploadedBundleVersion(BuildScript.BuildTypeEnum buildType, string bundleVersion)
    {
        if (string.IsNullOrWhiteSpace(bundleVersion))
            return;
        EditorPrefs.SetString(EditorPrefsKeyPrefix + buildType, bundleVersion.Trim());
    }

    /// <summary>
    /// 在 ThreadPool / 上传异步延续等非主线程上也可调用：<see cref="EditorPrefs"/> 仅允许主线程，故推迟到下一帧 <see cref="EditorApplication.delayCall"/> 再写入。
    /// </summary>
    public static void SaveLastUploadedBundleVersionFromAnyThread(BuildScript.BuildTypeEnum buildType, string bundleVersion)
    {
        if (string.IsNullOrWhiteSpace(bundleVersion))
            return;
        var trimmed = bundleVersion.Trim();
        var bt = buildType;
        EditorApplication.delayCall += () => SaveLastUploadedBundleVersion(bt, trimmed);
    }

    /// <summary>
    /// 若該構建類型尚無記錄則返回 <see cref="PlayerSettings.bundleVersion"/>；否則返回上次記錄版本號末段 +1（語義化版本常見規則）。
    /// </summary>
    public static string GetSuggestedNextBundleVersion(BuildScript.BuildTypeEnum buildType)
    {
        string last = EditorPrefs.GetString(EditorPrefsKeyPrefix + buildType, "");
        if (string.IsNullOrEmpty(last))
            return PlayerSettings.bundleVersion;
        return IncrementBundleVersionString(last);
    }

    /// <summary>從右往左找到最後一個純數字段並 +1；若無則在末尾追加 ".1"。</summary>
    public static string IncrementBundleVersionString(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "0.0.1";

        string[] parts = version.Split('.');
        if (parts.Length == 0)
            return version + ".1";

        for (int i = parts.Length - 1; i >= 0; i--)
        {
            string segment = parts[i].Trim();
            if (segment.Length > 0 && int.TryParse(segment, out int n))
            {
                parts[i] = (n + 1).ToString();
                return string.Join(".", parts);
            }
        }

        return version + ".1";
    }
}
