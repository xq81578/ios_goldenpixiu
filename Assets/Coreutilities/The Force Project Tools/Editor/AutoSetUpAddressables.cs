#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public class AutoSetUpAddressables : EditorWindow
{
    private struct ProfileInfo
    {
        public string RemoteBuildPath;
        public string RemoteLoadPath;
    }

    private const string _projectName = "ProjectName";
    /// <summary>與 BuildScript、S3 <c>v-</c> 目錄段共用（值為 bundle 版本號）；AWS_PROD 的 Remote.LoadPath 模板中占位。</summary>
    public const string ReleaseVersionVariableName = "ReleaseVersion";
    private const string _remoteBuildPath = "Remote.BuildPath";
    private const string _remoteLoadPath = "Remote.LoadPath";

    private const string _localBuildPath_ID = "7b508f1bb8473fc4285705d853e97feb";
    private const string _localLoadPath_ID = "0f291b6c0aeedbf4f8f31da1d81d57b3";
    private const string _remoteBuildPath_ID = "e7af798b47683074886e6ab7122e762b";
    private const string _remoteLoadPath_ID = "06de24f6c6b92b34e97dd183e187c8eb";

    private static Dictionary<string, ProfileInfo> _profileSettings = new Dictionary<string, ProfileInfo>()
    {
        { "AWS_DEV"
            , new ProfileInfo { RemoteBuildPath = "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]"
            , RemoteLoadPath = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]" } },
        { "AWS_UAT"
            , new ProfileInfo { RemoteBuildPath = "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]"
            , RemoteLoadPath = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]" } },
        { "AWS_PROD"
            , new ProfileInfo { RemoteBuildPath = "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]"
            , RemoteLoadPath = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]" } }
    };


    [MenuItem("Tools/Combo Project Setting/自动设置 Addressables Profiles")]
    public static void SetUp()
    {
        // Get Addressable settings
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Addressables", "Addressables is not installed or not initialized in this project.", "OK");
            return;
        }

        AddressableAssetProfileSettings profileSettings = settings.profileSettings;
        if (profileSettings == null)
        {
            EditorUtility.DisplayDialog("Addressables", "Profile settings not available on AddressableAssetSettings.", "OK");
            return;
        }

        List<string> variableNames = profileSettings.GetVariableNames();
        if (!variableNames.Contains(_projectName))
        {
            Debug.Log("[AddressablesProfilesEasySetUp] ProjectName 變數不存在，請設定。");
            ProjectNameInputWindow.ShowWindow(settings);
        }
        else
        {
            AutoSetUp(settings);
        }
    }

    /// <summary>確保存在 <see cref="ReleaseVersionVariableName"/>（預設空）；RELEASE 構建前由 BuildScript 賦值。</summary>
    public static void EnsureReleaseVersionVariableExists(AddressableAssetProfileSettings profileSettings)
    {
        if (profileSettings == null)
            return;
        var variableNames = profileSettings.GetVariableNames();
        if (variableNames != null && variableNames.Contains(ReleaseVersionVariableName))
            return;
        profileSettings.CreateValue(ReleaseVersionVariableName, "");
        Debug.Log($"[AutoSetUpAddressables] 已建立 Addressables 變數 {ReleaseVersionVariableName}（預設空）。");
    }

    public static void AutoSetUp(AddressableAssetSettings settings)
    {
        AddressableAssetProfileSettings profileSettings = settings.profileSettings;
        EnsureReleaseVersionVariableExists(profileSettings);
        foreach (var valuePair in _profileSettings)
        {
            string profileName = valuePair.Key;
            ProfileInfo profileInfo = valuePair.Value;
            string profileId = profileSettings.GetProfileId(profileName);

            if (!string.IsNullOrEmpty(profileId))
            {
                profileSettings.SetValue(profileId, _remoteBuildPath, profileInfo.RemoteBuildPath);
                profileSettings.SetValue(profileId, _remoteLoadPath, profileInfo.RemoteLoadPath);
            }
            else
            {
                string sourceProfileId = profileSettings.GetProfileId("Default"); // Example: copy from "Default" profile
                profileSettings.AddProfile(profileName, sourceProfileId);
                profileId = profileSettings.GetProfileId(profileName);
                profileSettings.SetValue(profileId, _remoteBuildPath, profileInfo.RemoteBuildPath);
                profileSettings.SetValue(profileId, _remoteLoadPath, profileInfo.RemoteLoadPath);
            }
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("[AddressablesProfilesEasySetUp] Addressables Profiles 設定完成！");
    }

    [MenuItem("Tools/Combo Project Setting/统一 Addressables  ID")]
    private static void UnifyProfileVariableIdsMenu()
    {
        UnifyProfileVariableIds();
    }

    private static void UnifyProfileVariableIds()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressablesProfilesEasySetUp] 無法取得 Addressable 設定 (Settings 為 null)。");
            return;
        }

        string settingsPath = AssetDatabase.GetAssetPath(settings);

        if (string.IsNullOrEmpty(settingsPath) || !File.Exists(settingsPath))
        {
            Debug.LogError($"[AddressablesProfilesEasySetUp] 找不到 AddressableAssetSettings 檔案: {settingsPath}");
            return;
        }

        string originalText = File.ReadAllText(settingsPath);
        // 只先解析四個目前 ID，不執行替換
        string LocalLoadPath_Id = null;
        string LocalBuildPath_Id = null;
        string RemoteLoadPath_Id = null;
        string RemoteBuildPath_Id = null;

        // 在 m_ProfileEntryNames 區塊中條列: - m_Id: <id>\n      m_Name: <name>
        Regex entryRegex = new Regex(@"- m_Id:\s*([0-9a-f]{32})\s*\n\s*m_Name:\s*([A-Za-z0-9_.-]+)", RegexOptions.Compiled);
        foreach (Match m in entryRegex.Matches(originalText))
        {
            string id = m.Groups[1].Value;
            string name = m.Groups[2].Value;
            switch (name)
            {
                case "Local.LoadPath":
                    LocalLoadPath_Id = id; break;
                case "Local.BuildPath":
                    LocalBuildPath_Id = id; break;
                case "Remote.LoadPath":
                    RemoteLoadPath_Id = id; break;
                case "Remote.BuildPath":
                    RemoteBuildPath_Id = id; break;
            }
        }

        Debug.Log(
            "[AddressablesProfilesEasySetUp] 目前四個 ID:\n" +
            $" Local.LoadPath   : {LocalLoadPath_Id ?? "<未找到>"}\n" +
            $" Local.BuildPath  : {LocalBuildPath_Id ?? "<未找到>"}\n" +
            $" Remote.LoadPath  : {RemoteLoadPath_Id ?? "<未找到>"}\n" +
            $" Remote.BuildPath : {RemoteBuildPath_Id ?? "<未找到>"}\n"
        );

        var logs = new List<string>();

        void ReplaceSimple(string varName, string currentId, string targetId)
        {
            if (string.IsNullOrEmpty(currentId))
            {
                logs.Add(varName + ": <未找到> 跳過");
                return;
            }
            if (currentId == targetId)
            {
                logs.Add(varName + ": 已是目標 ID");
                return;
            }
            int count = 0; int idx = 0;
            while ((idx = originalText.IndexOf(currentId, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += currentId.Length;
            }
            originalText = originalText.Replace(currentId, targetId);
            logs.Add($"{varName}: {currentId} -> {targetId} (替換 {count} 處)");
        }

        ReplaceSimple("Local.LoadPath", LocalLoadPath_Id, _localLoadPath_ID);
        ReplaceSimple("Local.BuildPath", LocalBuildPath_Id, _localBuildPath_ID);
        ReplaceSimple("Remote.LoadPath", RemoteLoadPath_Id, _remoteLoadPath_ID);
        ReplaceSimple("Remote.BuildPath", RemoteBuildPath_Id, _remoteBuildPath_ID);

        // 取得此路徑底下所有的 .asset 檔案 "Assets/AddressableAssetsData/AssetGroups/Schemas/" ，也做以上相同的 ID 替換處理
        // Step 1: 僅取得 Schemas 目錄下所有 .asset 檔案並列出
        foreach (var group in settings.groups)
        {
            if (group == null) continue;

            // group.Schemas 直接就是所有掛在該 Group 的 Schema 實例 (ScriptableObject)
            foreach (var schema in group.Schemas)
            {
                string schemaSettingsPath = AssetDatabase.GetAssetPath(schema);
                string schemaOriginalText = File.ReadAllText(schemaSettingsPath);

                // Debug.LogError(schemaSettingsPath);
                // Debug.LogError(schemaOriginalText);

                schemaOriginalText = schemaOriginalText.Replace(LocalLoadPath_Id, _localLoadPath_ID);
                schemaOriginalText = schemaOriginalText.Replace(LocalBuildPath_Id, _localBuildPath_ID);
                schemaOriginalText = schemaOriginalText.Replace(RemoteLoadPath_Id, _remoteLoadPath_ID);
                schemaOriginalText = schemaOriginalText.Replace(RemoteBuildPath_Id, _remoteBuildPath_ID);

                File.WriteAllText(schemaSettingsPath, schemaOriginalText);
            }
        }

        File.WriteAllText(settingsPath, originalText);
        AssetDatabase.ImportAsset(settingsPath);

        AssetDatabase.Refresh();
    }
}

public class ProjectNameInputWindow : EditorWindow
{
    private string _projectNameValue = PlayerSettings.productName;
    private AddressableAssetSettings _settings;

    public static void ShowWindow(AddressableAssetSettings settings)
    {
        var window = GetWindow<ProjectNameInputWindow>("Enter Project Name");
        window._settings = settings;
        window.minSize = new Vector2(400, 100);
        window.maxSize = new Vector2(400, 100);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Please enter the value for the 'ProjectName' variable:", EditorStyles.wordWrappedLabel);
        _projectNameValue = EditorGUILayout.TextField("Project Name", _projectNameValue);

        if (GUILayout.Button("Confirm and Set Up"))
        {
            if (!string.IsNullOrEmpty(_projectNameValue))
            {
                _settings.profileSettings.CreateValue("ProjectName", _projectNameValue);
                Debug.Log($"[AddressablesProfilesEasySetUp] 已新增 ProjectName 變數，值為: {_projectNameValue}");
                AutoSetUpAddressables.AutoSetUp(_settings);
                Close();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Project Name cannot be empty.", "OK");
            }
        }
    }
}
#endif
