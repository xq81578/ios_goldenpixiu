using System.Linq;
using UnityEditor;

[System.Obsolete]
public class DefineChecker
{
    public static bool HasDefine(string define)
    {
        // 獲取當前目標平台的 BuildTargetGroup
        BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

        // 使用新版 API 取得 define symbols
        string defines = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup));

        // 檢查是否包含指定的 Define
        return defines.Split(';').Contains(define);
    }
}