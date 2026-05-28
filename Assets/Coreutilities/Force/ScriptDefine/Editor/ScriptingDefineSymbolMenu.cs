using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class ScriptingDefineSymbolMenu
{
    public static readonly string[] DefineSymbols = { "DEV_BUILD", "UAT_BUILD", "RELEASE_BUILD" };
    public static readonly string[] AddressableProfiles = { "AWS_DEV", "AWS_UAT", "AWS_PROD" };

    #region Menu Items
    [MenuItem("SwitchDefine/DEV_BUILD", false, 20)]
    public static void ToggleDevBuild()
    {
        ToggleDefineSymbol(DefineSymbols[0]);
    }

    [MenuItem("SwitchDefine/DEV_BUILD", true)]
    public static bool DevBuildChecked()
    {
        Menu.SetChecked("SwitchDefine/DEV_BUILD"
            , GetDefineSymbols().Contains(DefineSymbols[0]));
        return true;
    }

    [MenuItem("SwitchDefine/UAT_BUILD", false, 21)]
    public static void ToggleUatBuild()
    {
        ToggleDefineSymbol(DefineSymbols[1]);
    }

    [MenuItem("SwitchDefine/UAT_BUILD", true)]
    public static bool UatBuildChecked()
    {
        Menu.SetChecked("SwitchDefine/UAT_BUILD"
            , GetDefineSymbols().Contains(DefineSymbols[1]));
        return true;
    }

    [MenuItem("SwitchDefine/RELEASE_BUILD", false, 41)]
    public static void ToggleReleaseBuild()
    {
        ToggleDefineSymbol(DefineSymbols[2]);
    }

    [MenuItem("SwitchDefine/RELEASE_BUILD", true)]
    public static bool ReleaseBuildChecked()
    {
        Menu.SetChecked("SwitchDefine/RELEASE_BUILD"
            , GetDefineSymbols().Contains(DefineSymbols[2]));
        return true;
    }
    #endregion

    public static void SetDevBuild()
    {
        Debug.Log("[ScriptingDefineSymbolMenu] SetDevBuild 啟動");
        if (GetDefineSymbols().Contains(DefineSymbols[0]))
        {
            Debug.Log($"[ScriptingDefineSymbolMenu] 已經是 {DefineSymbols[0]}，切換 Addressables Profile");
            SetAddressablesProfile();
        }
        else
        {
            Debug.Log($"[ScriptingDefineSymbolMenu] 切換到 {DefineSymbols[0]}");
            ToggleDevBuild();
        }
        Debug.Log($"[ScriptingDefineSymbolMenu] 開始建置專案 ({DefineSymbols[0]})");
    }

    public static void SetUatBuild()
    {
        Debug.Log("[ScriptingDefineSymbolMenu] SetUatBuild 啟動");
        if (GetDefineSymbols().Contains(DefineSymbols[1]))
        {
            Debug.Log($"[ScriptingDefineSymbolMenu] 已經是 {DefineSymbols[1]}，切換 Addressables Profile");
            SetAddressablesProfile();
        }
        else
        {
            Debug.Log($"[ScriptingDefineSymbolMenu] 切換到 {DefineSymbols[1]}");
            ToggleUatBuild();
        }
        Debug.Log($"[ScriptingDefineSymbolMenu] 開始建置專案 ({DefineSymbols[1]})");
    }

    public static void SetReleaseBuild()
    {
        Debug.Log("[ScriptingDefineSymbolMenu] SetReleaseBuild 啟動");
        if (GetDefineSymbols().Contains(DefineSymbols[2]))
        {
            Debug.Log($"[ScriptingDefineSymbolMenu] 已經是 {DefineSymbols[2]}，切換 Addressables Profile");
            SetAddressablesProfile();
        }
        else
        {
            Debug.Log($"[ScriptingDefineSymbolMenu] 切換到 {DefineSymbols[2]}");
            ToggleReleaseBuild();
        }
        // 連續打 Release 時會走「已經是 RELEASE_BUILD」分支，不會經過 ToggleDefineSymbol → SetSRDebugger；此處保證正式包必關 SRDebugger。
        EnsureSRDebuggerDisabledForRelease();
        Debug.Log($"[ScriptingDefineSymbolMenu] 開始建置專案 ({DefineSymbols[2]})");
    }

    /// <summary>
    /// Release 正式包必關 SRDebugger（寫入 DISABLE_SRDEBUGGER 等）。與選單 Toggle 無關，供 SetReleaseBuild 與建置流程最後一道保險。
    /// </summary>
    private static void EnsureSRDebuggerDisabledForRelease()
    {
        if (!SRDebugger.Editor.SRDebugEditor.IsEnabled)
            return;
        Debug.Log("[ScriptingDefineSymbolMenu] Release 構建：強制停用 SRDebugger（避免已為 RELEASE_BUILD 時短路未關閉）");
        SRDebugger.Editor.SRDebugEditor.SetEnabled(false);
    }

    // 切換符號定義
    private static void ToggleDefineSymbol(string symbol)
    {
        Debug.Log($"[ScriptingDefineSymbolMenu] ToggleDefineSymbol 啟動，目標 symbol: {symbol}");
        List<string> currentSymbols = GetDefineSymbols();
        Debug.Log($"[ScriptingDefineSymbolMenu] 當前 define symbols: {string.Join(", ", currentSymbols)}");

        bool isRelease = symbol == DefineSymbols[2];

        // 如果已經有此 symbol，則移除（取消選取）
        SetSRDebugger(currentSymbols.Contains(symbol) ? !isRelease : isRelease);
        // SRDebugger 狀態有變動，重新取得 define symbols
        currentSymbols = GetDefineSymbols();

        // 如果已經有此 symbol，則移除（取消選取）
        if (currentSymbols.Contains(symbol))
        {
            Debug.Log($"[ScriptingDefineSymbolMenu] 已有 symbol {symbol}，將移除");
            currentSymbols.Remove(symbol);
        }
        else
        {
            Debug.Log($"[ScriptingDefineSymbolMenu] 切換 symbol，移除所有環境 define 並加入 {symbol}");
            // 只允許一個環境 define 存在，先移除所有已存在的環境 define，再加入選取的 symbol
            currentSymbols.RemoveAll(s => DefineSymbols.Contains(s));
            currentSymbols.Add(symbol);
        }

        Debug.Log($"[ScriptingDefineSymbolMenu] 設定 define symbols: {string.Join(", ", currentSymbols)}");
        SetDefineSymbols(currentSymbols);
        SetAddressablesProfile();
    }

    private static void SetSRDebugger(bool isRelease)
    {
        bool srEnable = SRDebugger.Editor.SRDebugEditor.IsEnabled;
        if (srEnable && isRelease)
        {
            Debug.Log("[ScriptingDefineSymbolMenu] SRDebugger 啟用且切換為 Release，將停用 SRDebugger");
            SRDebugger.Editor.SRDebugEditor.SetEnabled(false);
        }
        else if (!srEnable && !isRelease)
        {
            Debug.Log("[ScriptingDefineSymbolMenu] SRDebugger 停用且切換為 Dev/UAT，將啟用 SRDebugger");
            SRDebugger.Editor.SRDebugEditor.SetEnabled(true);
        }
    }

    private static List<string> GetDefineSymbols()
    {
        string defineSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)));
        return defineSymbols.Split(';').ToList();
    }

    private static void SetDefineSymbols(List<string> symbols)
    {
        if (symbols == null || symbols.Count == 0)
            return;

        string defineSymbols = symbols[0];
        if (symbols.Count > 1)
        {
            defineSymbols = string.Join(";", symbols);
        }

        Debug.Log($"[ScriptingDefineSymbolMenu] Updated Scripting Define Symbols: {defineSymbols}");
        var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
        PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defineSymbols);
        AssetDatabase.SaveAssets();
    }

    public static void SetAddressablesProfile()
    {
        // 根據切換的Define來切換Addressables Profile
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null || settings.profileSettings == null)
        {
            Debug.LogWarning("[ScriptingDefineSymbolMenu] 找不到 AddressableAssetSettings 或 profileSettings");
            return;
        }

        List<string> defineSymbols = GetDefineSymbols();

        string profileName = "Default";
        for (int i = 0; i < DefineSymbols.Length; i++)
        {
            if (defineSymbols.Contains(DefineSymbols[i]))
            {
                profileName = AddressableProfiles[i];
                break;
            }
        }

        string profileId = settings.profileSettings.GetProfileId(profileName);
        if (string.IsNullOrEmpty(profileId))
        {
            Debug.LogWarning($"[ScriptingDefineSymbolMenu] 找不到對應的 Addressables Profile: {profileName}, 將嘗試自動設置。");
            AutoSetUpAddressables.AutoSetUp(settings);
            profileId = settings.profileSettings.GetProfileId(profileName);
        }

        settings.activeProfileId = profileId;
        Debug.Log($"[ScriptingDefineSymbolMenu] Addressables Profile 已切換為: {profileName}");
    }
}
