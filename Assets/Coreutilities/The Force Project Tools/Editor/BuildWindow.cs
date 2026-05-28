using System;
using UnityEditor;
using UnityEngine;

namespace CoreUtilities.The_Force_Project_Tools.Editor
{
public class BuildWindow : EditorWindow
{
    private BuildTarget _selectedBuildTarget = BuildTarget.WebGL;
    private BuildScript.BuildTypeEnum _selectedBuildType = BuildScript.BuildTypeEnum.DEV_BUILD;
    private string _outputPath = "BuildOutput";
    private string _version = "";
    private bool _uploadAws = false;

    public static void ShowWindow()
    {
        var window = GetWindow<BuildWindow>("自定义构建");
        window.minSize = new Vector2(400, 300);
        window.ApplySuggestedVersionForSelectedBuildType();
    }

    private void OnEnable()
    {
        ApplySuggestedVersionForSelectedBuildType();
    }

    /// <summary>使用專案設定中的版本號（<see cref="PlayerSettings.bundleVersion"/>），不自動遞增。</summary>
    private void ApplySuggestedVersionForSelectedBuildType()
    {
        _version = PlayerSettings.bundleVersion;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("构建配置", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 构建目标
        _selectedBuildTarget = (BuildTarget)EditorGUILayout.EnumPopup("构建平台", _selectedBuildTarget);

        // 构建类型
        EditorGUI.BeginChangeCheck();
        _selectedBuildType = (BuildScript.BuildTypeEnum)EditorGUILayout.EnumPopup("构建类型", _selectedBuildType);
        if (EditorGUI.EndChangeCheck())
            ApplySuggestedVersionForSelectedBuildType();

        // 输出路径
        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField("输出路径", _outputPath);
        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string path = EditorUtility.SaveFolderPanel("选择输出路径", _outputPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                _outputPath = path;
            }
        }
        EditorGUILayout.EndHorizontal();

        // 版本号
        _version = EditorGUILayout.TextField("版本号", _version);
        if (string.IsNullOrEmpty(_version))
        {
            _version = PlayerSettings.bundleVersion;
        }

        // 构建成功后：Release WebGL + 勾选 → S3；其余勾选 → Linux（与菜单「上传到宝塔」一致）
        _uploadAws = EditorGUILayout.Toggle("构建成功后自动上传", _uploadAws);
        if (_uploadAws)
        {
            bool isReleaseWebGlS3 = _selectedBuildType == BuildScript.BuildTypeEnum.RELEASE_BUILD &&
                                    _selectedBuildTarget == BuildTarget.WebGL;
            string uploadHint = isReleaseWebGlS3
                ? "当前选择：Release WebGL，将上传 WebGL + BundleSource 至 S3。"
                : "当前选择：将上传至 Linux 服务器（SSH，Dev/Uat/或非 WebGL Release）。";

            if (isReleaseWebGlS3)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                Color prevContent = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.1f, 0.1f);
                EditorGUILayout.LabelField(uploadHint, EditorStyles.wordWrappedLabel);
                GUI.contentColor = prevContent;
                EditorGUILayout.EndVertical();
                EditorGUILayout.HelpBox(
                    "构建开始之前会先检查 S3 上是否已存在当前版本（v-）目录；若已存在将取消构建并提示，不会执行构建。",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(uploadHint, MessageType.None);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox($"将构建到: {_outputPath}\n" +
            $"文件名: {DateTime.Now:yyMMddHHmm}_{_version.Replace('.', '_')}{GetBuildTypeSuffix(_selectedBuildType)}", 
            MessageType.Info);

        EditorGUILayout.Space();

        // 构建按钮
        GUI.enabled = !string.IsNullOrEmpty(_outputPath) && !string.IsNullOrEmpty(_version);
        if (GUILayout.Button("开始构建", GUILayout.Height(30)))
        {
            BuildWithCustomSettings();
        }
        GUI.enabled = true;
    }

    private void BuildWithCustomSettings()
    {
        // 设置临时环境变量来模拟命令行参数
        Environment.SetEnvironmentVariable("BUILD_TARGET", _selectedBuildTarget.ToString());
        Environment.SetEnvironmentVariable("BUILD_TYPE", _selectedBuildType.ToString());
        Environment.SetEnvironmentVariable("OUTPUT_PATH", _outputPath);
        Environment.SetEnvironmentVariable("BUILD_VERSION", _version);
        Environment.SetEnvironmentVariable("UPLOAD_AWS", _uploadAws.ToString());

        // 保存版本号
        string originalVersion = PlayerSettings.bundleVersion;
        PlayerSettings.bundleVersion = _version;

        try
        {
            // 调用构建方法；取消或失败时不关窗口，避免与 EditorApplication.Exit 叠加导致异常
            bool ok = BuildScript.BuildWithPreset(_selectedBuildTarget, _selectedBuildType, _outputPath, _uploadAws);
            if (ok)
            {
                Close();
            }
        }
        finally
        {
            // 恢复版本号
            PlayerSettings.bundleVersion = originalVersion;
        }
    }

    private string GetBuildTypeSuffix(BuildScript.BuildTypeEnum buildType)
    {
        return buildType switch
        {
            BuildScript.BuildTypeEnum.DEV_BUILD => "_d",
            BuildScript.BuildTypeEnum.UAT_BUILD => "_u",
            _ => ""
        };
    }
}
}