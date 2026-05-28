using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CoreUtilities.The_Force_Project_Tools.Editor;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public class BuildScript
{
    // Profile 名稱常數
    private const string PROFILE_DEV = "AWS_DEV";
    private const string PROFILE_UAT = "AWS_UAT";
    private const string PROFILE_PROD = "AWS_PROD";
    private const string PROFILE_DEFAULT = "Default";
    private const int MAX_CONCURRENT_UPLOADS = 10;

    public enum BuildTypeEnum
    {
        UNKNOWN,
        DEV_BUILD,
        UAT_BUILD,
        RELEASE_BUILD
    }

    public static string Building_Path { get; set; } = null;
    public static BuildTypeEnum Build_Type_Enum { get; set; } = BuildTypeEnum.UNKNOWN;

    /// <summary>最近一次成功 BuildPlayer 的目標平台（供 uploadAws / S3 分支判斷）。</summary>
    public static BuildTarget LastBuildTarget { get; private set; } = BuildTarget.NoTarget;

    /// <summary>
    /// CLI RELEASE 上傳 S3 時使用的 bundle 版本號（與構建時 <c>PlayerSettings.bundleVersion</c> / 自定義構建「版本號」一致）。
    /// PerformBuild 會在還原 bundleVersion 之前寫入，供 CLI 上傳 S3 使用。
    /// </summary>
    public static string LastReleaseBundleVersionForS3 { get; private set; }

    private static int _editorMainThreadId;

    [InitializeOnLoadMethod]
    private static void CaptureEditorMainThreadIdForUiDispatch()
    {
        _editorMainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    /// <summary>
    /// 上传流程在 ConfigureAwait(false) / 线程池 hop 后可能在非主线程；DisplayProgressBar、ClearProgressBar、DisplayDialog 须在此调度。
    /// </summary>
    private static void DispatchEditorUi(Action action)
    {
        if (action == null)
            return;

        if (Thread.CurrentThread.ManagedThreadId == _editorMainThreadId)
        {
            action();
            return;
        }

        EditorApplication.delayCall += () =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        };
    }

    // Jenkins/Unity CLI 呼叫的主入口
    public static void PerformBuild()
    {
        Building_Path = null;
        Build_Type_Enum = BuildTypeEnum.UNKNOWN;
        LastReleaseBundleVersionForS3 = null;

        Debug.Log("[BuildScript] 开始执行自动构建...");
        // 1. 取得參數
        var (outputPath, buildTargetStr, buildTypeStr) = GetBuildArgs();
        BuildTypeEnum buildType = ParseBuildType(buildTypeStr);
        BuildTarget buildTarget = ParseBuildTarget(buildTargetStr);
        Build_Type_Enum = buildType;

        // 2. 記錄原本的 bundleVersion
        string originalVersion = PlayerSettings.bundleVersion;
        bool versionChanged = false;

        // 3. 切換 ScriptingDefine
        SwitchScriptingDefine(buildType, buildTypeStr);

        // 4. 設定版本號（改為解析 -buildVersion CLI 參數）
        string version = GetArg("buildVersion", null);
        if (string.IsNullOrEmpty(version) || version == "0")
        {
            version = PlayerSettings.bundleVersion;
            Debug.Log($"[BuildScript] 未從 CLI 取得有效 buildVersion，改用 Unity 專案設定 version: {version}");
        }
        else
        {
            PlayerSettings.bundleVersion = version;
            Debug.Log($"[BuildScript] 使用從 CLI 傳入的 buildVersion: {version}");
        }

        if (version != originalVersion)
            versionChanged = true;

        // 5. 產生輸出檔名
        string outputName = GenerateOutputName(buildType, version);

        bool doUpload = ParseCliUploadFlag();

        Debug.Log($"[BuildScript] Build Info =>\n" +
                  $"  outputPath: {outputPath}\n" +
                  $"  outputName: {outputName}\n" +
                  $"  buildTarget: {buildTarget}\n" +
                  $"  buildType: {buildType}\n" +
                  $"  version: {version}\n" +
                  $"  upload (-upload / 兼容 -uploadAws): {doUpload}");

        if (buildType == BuildTypeEnum.RELEASE_BUILD)
        {
            SetAddressablesProfile(buildType);
            ApplyAddressablesReleaseVersionVariableForProd(version);
            LastReleaseBundleVersionForS3 = version;
        }

        // RELEASE + WebGL + 上傳 S3：構建前先檢查 S3 版本目錄是否已佔用
        if (doUpload && buildType == BuildTypeEnum.RELEASE_BUILD && buildTarget == BuildTarget.WebGL)
        {
            if (!TryPrecheckS3ReleaseVersionBeforeBuild(version))
            {
                LastReleaseBundleVersionForS3 = null;
                if (versionChanged)
                    PlayerSettings.bundleVersion = originalVersion;
                return;
            }
        }

        // 6. 取得場景
        string[] scenes = GetEnabledScenes();
        Debug.Log($"[BuildScript] scenes: {string.Join(", ", scenes)}");

        // 7. 產生輸出路徑
        string locationPathName = GetLocationPathName(buildTarget, outputPath, outputName);
        Debug.Log($"[BuildScript] locationPathName: {locationPathName}");

        // 8. 執行 Build（取消或失败时不在编辑器内 Exit，避免闪退）
        if (!DoBuild(scenes, locationPathName, buildTarget))
        {
            Debug.LogWarning("[BuildScript] 构建未成功，已中止后续步骤。");
            LastReleaseBundleVersionForS3 = null;
            return;
        }

        // // 9. 若有更動版本號，建置完後切回原本的版本號
        // if (versionChanged)
        // {
        //     PlayerSettings.bundleVersion = originalVersion;
        //     Debug.Log($"[BuildScript] 已將 bundleVersion 還原為: {originalVersion}");
        // }

        // 10. 上傳：RELEASE+WebGL → S3；其餘（DEV/UAT 或 Release 非 WebGL）→ 寶塔/SSH
        if (doUpload)
        {
            bool uploadOk = TryCliPostUploadSync(buildType, buildTarget, version);
            if (!uploadOk && Application.isBatchMode)
                EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("[BuildScript] 未傳入 -upload true，跳過構建後上傳。");
        }
    }

    /// <summary>
    /// <c>-upload true</c>（大小寫不敏感）；未傳 <c>-upload</c> 時兼容舊參數 <c>-uploadAws</c>。
    /// </summary>
    private static bool ParseCliUploadFlag()
    {
        string v = GetArg("upload", null);
        if (!string.IsNullOrEmpty(v) && bool.TryParse(v, out bool b))
            return b;

        string legacy = GetArg("uploadAws", "false");
        return bool.TryParse(legacy, out bool l) && l;
    }

    /// <summary>
    /// 命令行 <c>-upload true</c> 後同步完成上傳（batchmode 下可正確判斷進程退出碼）。
    /// </summary>
    private static bool TryCliPostUploadSync(BuildTypeEnum buildType, BuildTarget buildTarget,
        string bundleVersionLabelForHistory)
    {
        if (string.IsNullOrEmpty(Building_Path))
        {
            Debug.LogError("[BuildScript] -upload 已啟用但 Building_Path 為空，無法上傳。");
            return false;
        }

        string versionFolderName = Path.GetFileName(
            Building_Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        // Application.isBatchMode 仅主线程可访问；Task.Run 内必须传入主线程快照
        bool batchMode = Application.isBatchMode;
        try
        {
            // S3（主线程 GetResult）与 SSH（线程池 GetResult）共用：先快照 isBatchMode/CI 与项目名，避免延续线程调用 Application / 未预热时误判。
            SSHUploader.PrewarmEditorStateForCliWorkerUpload();

            if (buildType == BuildTypeEnum.RELEASE_BUILD && buildTarget == BuildTarget.WebGL)
            {
                string verForS3 = !string.IsNullOrEmpty(LastReleaseBundleVersionForS3)
                    ? LastReleaseBundleVersionForS3
                    : bundleVersionLabelForHistory;
                bool s3Ok = UploadBuildVersionToS3TaskAsync(
                    Building_Path, versionFolderName, buildType, verForS3, batchMode).GetAwaiter().GetResult();
                if (s3Ok && !string.IsNullOrEmpty(verForS3))
                    BuildBundleVersionHistory.SaveLastUploadedBundleVersion(buildType, verForS3);
                return s3Ok;
            }

            // 主线程 GetResult 会阻塞 Unity 主循环；若在主线程等待整段上传 async，延续可能无法调度 → 表现为卡在 SSH 诊断后。
            // Unity 下 Task.Run 仍可能被调度到主线程；改用 ThreadPool.QueueUserWorkItem 强制在线程池线程上阻塞等待 async。
            bool uploadResult = false;
            Exception uploadCaught = null;
            using (var done = new ManualResetEventSlim(false))
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Debug.Log(
                            $"[BuildScript] Linux CLI 上传工作线程 ManagedThreadId={Thread.CurrentThread.ManagedThreadId}（应与主线程不同）");
                        uploadResult = UploadBuildVersionToLinuxTaskAsync(
                                Building_Path, versionFolderName, buildType, bundleVersionLabelForHistory, batchMode)
                            .ConfigureAwait(false)
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (Exception ex)
                    {
                        uploadCaught = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                });
                done.Wait();
            }

            if (uploadCaught != null)
            {
                throw uploadCaught;
            }

            // 工作线程上不能调用 EditorPrefs；回到主线程后立即写入，避免 batchmode 在 delayCall 前退出导致未保存。
            if (uploadResult && !string.IsNullOrEmpty(bundleVersionLabelForHistory))
                BuildBundleVersionHistory.SaveLastUploadedBundleVersion(buildType, bundleVersionLabelForHistory);

            return uploadResult;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BuildScript] CLI 上傳過程異常: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    // 取得建構參數
    private static (string outputPath, string buildTargetStr, string buildTypeStr) GetBuildArgs()
    {
        string outputPath = GetArg("outputPath", "BuildOutput");
        string buildTargetStr = GetArg("buildTarget", "StandaloneWindows64");
        string buildTypeStr = GetArg("buildType", "DEV_BUILD");
        return (outputPath, buildTargetStr, buildTypeStr);
    }

    // 切換 ScriptingDefine
    public static void SwitchScriptingDefine(BuildTypeEnum buildType, string buildTypeStr)
    {
        Debug.Log($"[BuildScript] 開始切換 ScriptingDefine，buildType: {buildType}, buildTypeStr: {buildTypeStr}");
        try
        {
            var type = Type.GetType("ScriptingDefineSymbolMenu, Assembly-CSharp-Editor");
            if (type != null)
            {
                string methodName = null;
                switch (buildType)
                {
                    case BuildTypeEnum.DEV_BUILD:
                        methodName = "SetDevBuild";
                        break;
                    case BuildTypeEnum.UAT_BUILD:
                        methodName = "SetUatBuild";
                        break;
                    case BuildTypeEnum.RELEASE_BUILD:
                        methodName = "SetReleaseBuild";
                        break;
                }

                if (!string.IsNullOrEmpty(methodName))
                {
                    var method = type.GetMethod(methodName,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (method != null)
                    {
                        method.Invoke(null, null);
                        Debug.Log($"[BuildScript] 已反射呼叫 {methodName}()");
                    }
                    else
                    {
                        Debug.LogWarning($"[BuildScript] 找不到 ScriptingDefineSymbolMenu 的方法: {methodName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[BuildScript] buildType {buildTypeStr} 無對應 ScriptingDefine 切換函式");
                }
            }
            else
            {
                Debug.LogWarning("[BuildScript] 找不到 ScriptingDefineSymbolMenu 型別，請確認編譯設定與路徑");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BuildScript] 反射呼叫 ScriptingDefineSymbolMenu 發生例外: {e.Message}");
        }
    }

    // 產生輸出檔名
    private static string GenerateOutputName(BuildTypeEnum buildType, string version)
    {
        string buildTypeSuffix =
            buildType == BuildTypeEnum.DEV_BUILD
                ? "_d"
                : (buildType == BuildTypeEnum.UAT_BUILD ? "_u" : "");
        string timeStamp = DateTime.Now.ToString("yyMMddHHmm");
        return $"{timeStamp}_{version.Replace('.', '_')}{buildTypeSuffix}";
    }

    // 產生輸出路徑
    private static string GetLocationPathName(BuildTarget buildTarget, string outputPath, string outputName)
    {
        if (buildTarget == BuildTarget.WebGL)
        {
            return outputPath + "/" + outputName;
        }
        else
        {
            string extension = GetBuildExtension(buildTarget);
            return outputPath + "/" + outputName + extension;
        }
    }

    // 執行 Build；返回 true 表示成功。编辑器内取消/失败不调用 Exit，避免整编辑器退出（闪退）。
    private static bool DoBuild(string[] scenes, string locationPathName, BuildTarget buildTarget)
    {
        if (buildTarget == BuildTarget.WebGL)
        {
            WebGLProjectSettings.ApplySettings();
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = locationPathName,
            target = buildTarget,
            options = BuildOptions.None,
        };

        Debug.Log("[BuildScript] 開始 BuildPipeline.BuildPlayer...");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        Debug.Log($"[BuildScript] Build result: {summary.result}");
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
            Debug.Log($"[JENKINS_OUTPUT_PATH]={locationPathName}");
            Building_Path = locationPathName;
            LastBuildTarget = buildTarget;
            return true;
        }

        Debug.LogError($"[BuildScript] Build 未成功: {summary.result}");

        // 仅命令行/batch 构建失败时退出进程，供 CI 判断失败码
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }

        return false;
    }
    private static bool _argsLogged = false;
    // 取得命令列參數（開關名不區分大小寫，例如 -upload、-Upload 皆可）
    private static string GetArg(string name, string defaultValue)
    {
        string[] args = Environment.GetCommandLineArgs();
        if (!_argsLogged)
        {
            Debug.Log("[BuildScript] CLI Args: " + string.Join(" ", args));
            _argsLogged = true;
        }
        string prefix = "-" + name;
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], prefix, StringComparison.OrdinalIgnoreCase) && args.Length > i + 1)
                return args[i + 1];
        }

        return defaultValue;
    }

    // 取得 Build Settings 裡啟用的場景
    private static string[] GetEnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        System.Collections.Generic.List<string> enabledScenes = new System.Collections.Generic.List<string>();
        foreach (var scene in scenes)
        {
            if (scene.enabled)
                enabledScenes.Add(scene.path);
        }

        return enabledScenes.ToArray();
    }

    // 解析 BuildTarget
    private static BuildTarget ParseBuildTarget(string target)
    {
        try
        {
            return (BuildTarget)Enum.Parse(typeof(BuildTarget), target);
        }
        catch
        {
            return BuildTarget.StandaloneWindows64;
        }
    }

    // 根據平台取得副檔名
    private static string GetBuildExtension(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return ".exe";
            case BuildTarget.StandaloneOSX:
                return ".app";
            case BuildTarget.Android:
                return ".apk";
            default:
                return "";
        }
    }

    private static BuildTypeEnum ParseBuildType(string type)
    {
        switch (type)
        {
            case "DEV_BUILD": return BuildTypeEnum.DEV_BUILD;
            case "UAT_BUILD": return BuildTypeEnum.UAT_BUILD;
            case "RELEASE_BUILD": return BuildTypeEnum.RELEASE_BUILD;
            default: return BuildTypeEnum.UNKNOWN;
        }
    }

    // 設定 Addressables Profile
    private static string SetAddressablesProfile(BuildTypeEnum buildType)
    {
        string profileName = null;
#if UNITY_EDITOR
        // 直接使用 UnityEditor.AddressableAssets.Settings.AddressableAssetSettings API
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[BuildScript] 找不到 AddressableAssetSettings 。請確認專案已建立 Addressables 設定。");
            return profileName;
        }

        settings.MaxConcurrentWebRequests = 20;
        settings.CatalogRequestsTimeout = 10;
        settings.BundleTimeout = 10;
        settings.BundleRetryCount = 3;

        Debug.Log(
            $"[BuildScript] Addressables Settings: MaxConcurrentWebRequests={settings.MaxConcurrentWebRequests}, " +
            $"CatalogRequestsTimeout={settings.CatalogRequestsTimeout}, BundleTimeout={settings.BundleTimeout}, BundleRetryCount={settings.BundleRetryCount}");

        var profileSettings = settings.profileSettings;
        if (profileSettings == null)
        {
            Debug.LogWarning("[BuildScript] 找不到 profileSettings。");
            return profileName;
        }

        profileName = GetProfileName(buildType);
        string profileId = profileSettings.GetProfileId(profileName);
        if (string.IsNullOrEmpty(profileId))
        {
            Debug.LogWarning($"[BuildScript] 找不到 Addressables Profile: {profileName}，將使用 Default。");
            profileId = profileSettings.GetProfileId("Default");
            profileName = "Default";
        }

        if (!string.IsNullOrEmpty(profileId))
        {
            settings.activeProfileId = profileId;
            Debug.Log($"[BuildScript] Addressables Profile In Use 已設定為: {profileName}");
        }
        else
        {
            Debug.LogWarning("[BuildScript] 設定 activeProfileId 失敗。");
        }

        return profileName;
#endif
    }

    /// <summary>
    /// 正式 RELEASE 構建前：將 AWS_PROD Profile 的 ReleaseVersion 設為 <paramref name="bundleVersionLabel"/>（<c>PlayerSettings.bundleVersion</c>，不含時間戳）。
    /// 需與 S3 <c>v-…</c> 目錄段及 <see cref="AutoSetUpAddressables"/> 中 Remote.LoadPath 的 <c>[ReleaseVersion]</c> 一致。
    /// </summary>
    private static void ApplyAddressablesReleaseVersionVariableForProd(string bundleVersionLabel)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[BuildScript] 無法寫入 ReleaseVersion：AddressableAssetSettings 為 null。");
            return;
        }

        var profileSettings = settings.profileSettings;
        if (profileSettings == null)
            return;

        AutoSetUpAddressables.EnsureReleaseVersionVariableExists(profileSettings);

        string profileId = profileSettings.GetProfileId(PROFILE_PROD);
        if (string.IsNullOrEmpty(profileId))
        {
            Debug.LogWarning("[BuildScript] 找不到 AWS_PROD Profile，跳過 ReleaseVersion 寫入。");
            return;
        }

        string seg = SanitizeReleaseUploadVersionSegment(bundleVersionLabel);
        profileSettings.SetValue(profileId, AutoSetUpAddressables.ReleaseVersionVariableName, seg);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BuildScript] Addressables {AutoSetUpAddressables.ReleaseVersionVariableName}（{PROFILE_PROD}）= \"{seg}\"（與 S3 v- 目錄段一致）");
    }

    private static string SanitizeReleaseUploadVersionSegment(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return "";
        return folderName.Trim().Trim('/', '\\').Replace('\\', '_').Replace('/', '_');
    }

    private static string KeyPrefix()
    {
        // 取 Application.productName，遇到底線就只取前半段
        string productName = Application.productName;
        int idx = productName.IndexOf('_');
        if (idx > 0)
            return productName.Substring(0, idx);
        return productName;
    }

    // 取得 Addressables Profile 名稱
    private static string GetProfileName(BuildTypeEnum buildType)
    {
        switch (buildType)
        {
            case BuildTypeEnum.DEV_BUILD:
                return PROFILE_DEV;
            case BuildTypeEnum.UAT_BUILD:
                return PROFILE_UAT;
            case BuildTypeEnum.RELEASE_BUILD:
                return PROFILE_PROD;
            default:
                return PROFILE_DEFAULT;
        }
    }

    // public static AWSS3UploaderSettings.S3Profile CreateProductionProfile(string buildingPath)
    // {
    //     var s3UploaderSettings = AWSS3UploaderSettings.LoadEditorSettings(false);
    //     if (s3UploaderSettings == null)
    //     {
    //         Debug.LogWarning("[BuildScript] 找不到 AWSS3UploaderSettings 資源，無法上傳至 AWS。");
    //         return null;
    //     }
    //
    //     var settings = AddressableAssetSettingsDefaultObject.Settings;
    //     if (settings == null || settings.profileSettings == null)
    //     {
    //         Debug.LogWarning("[BuildScript] 找不到 AddressableAssetSettings 或 profileSettings，無法取得 RemoteBuildPath。");
    //         return null;
    //     }
    //
    //     string activeProfileId = settings.activeProfileId;
    //     string activeProfileName = settings.profileSettings.GetProfileName(activeProfileId);
    //     Debug.Log($"[BuildScript] 取得 Addressables Profile：activeProfileId={activeProfileId}, activeProfileName={activeProfileName}");
    //     var s3Profile = s3UploaderSettings.Profiles.Find(p => p.ProfileName == activeProfileName);
    //
    //     if (s3Profile == null)
    //     {
    //         Debug.LogWarning($"[BuildScript] 找不到對應的 AWS Profile: {activeProfileName}");
    //         return null;
    //     }
    //
    //     s3Profile = s3Profile.Clone();
    //     s3Profile.SyncWithAddressableProfile = true;
    //     s3Profile.MaxConcurrentUploads = MAX_CONCURRENT_UPLOADS;
    //     s3Profile.UploadAssetBundle = true;
    //     s3Profile.LocalDirectoryPath = buildingPath;
    //
    //     string remoteBuildPath = settings.profileSettings.GetValueByName(activeProfileId, "Remote.BuildPath");
    //     Debug.Log($"[BuildScript] ActiveProfile RemoteBuildPath: {remoteBuildPath}");
    //
    //     string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
    //     string bundleDir = remoteBuildPath.Replace("/[BuildTarget]", "");
    //     string assetBundleDirectoryPath = projectRoot + "/" + bundleDir;
    //     s3Profile.SetAssetBundleDirectoryPath(assetBundleDirectoryPath);
    //     s3Profile.GetAssetBundleDirectoryPath();
    //     Debug.Log($"[BuildScript] Profile AssetBundleDirectoryPath: {assetBundleDirectoryPath}");
    //     var projectName = settings.profileSettings.GetValueByName(activeProfileId, "ProjectName");
    //     var keyPrefix = !string.IsNullOrEmpty(projectName) ? projectName : KeyPrefix();
    //     s3Profile.SetS3KeyPrefix(keyPrefix);
    //     Debug.Log($"[BuildScript] Profile ProjectName: {projectName}, S3KeyPrefix: {keyPrefix}");
    //
    //     // 列印 s3Profile 詳細資訊（僅列出已知存在的屬性）
    //     Debug.Log($"[BuildScript] S3Profile 詳細資訊：\n" +
    //         $"  ProfileName: {s3Profile.ProfileName}\n" +
    //         $"  MaxConcurrentUploads: {s3Profile.MaxConcurrentUploads}\n" +
    //         $"  UploadAssetBundle: {s3Profile.UploadAssetBundle}\n" +
    //         $"  ExcludeBundleSourceFromClear: {s3Profile.ExcludeBundleSourceFromClear}\n" +
    //         $"  SkipDuplicateBundleUploads: {s3Profile.SkipDuplicateBundleUploads}\n" +
    //         $"  LocalDirectoryPath: {s3Profile.LocalDirectoryPath}\n" +
    //         $"  AssetBundleDirectoryPath: {assetBundleDirectoryPath}\n" +
    //         $"  S3KeyPrefix: {keyPrefix}");
    //
    //     return s3Profile;
    // }


    // ========== 菜单项：一键构建 ==========

    [MenuItem("Tools/Combo Project Setting/Build/WebGL - DEV", false, 1)]
    public static void BuildWebGLDev()
    {
        BuildWithPreset(BuildTarget.WebGL, BuildTypeEnum.DEV_BUILD, "BuildOutput");
    }

    [MenuItem("Tools/Combo Project Setting/Build/WebGL - UAT", false, 2)]
    public static void BuildWebGLUat()
    {
        BuildWithPreset(BuildTarget.WebGL, BuildTypeEnum.UAT_BUILD, "BuildOutput");
    }

    [MenuItem("Tools/Combo Project Setting/Build/WebGL - RELEASE", false, 3)]
    public static void BuildWebGLRelease()
    {
        BuildWithPreset(BuildTarget.WebGL, BuildTypeEnum.RELEASE_BUILD, "BuildOutput");
    }

    [MenuItem("Tools/Combo Project Setting/Build/Windows64 - DEV", false, 11)]
    public static void BuildWindowsDev()
    {
        BuildWithPreset(BuildTarget.StandaloneWindows64, BuildTypeEnum.DEV_BUILD, "BuildOutput");
    }

    [MenuItem("Tools/Combo Project Setting/Build/Windows64 - UAT", false, 12)]
    public static void BuildWindowsUat()
    {
        BuildWithPreset(BuildTarget.StandaloneWindows64, BuildTypeEnum.UAT_BUILD, "BuildOutput");
    }

    [MenuItem("Tools/Combo Project Setting/Build/Android - DEV", false, 21)]
    public static void BuildAndroidDev()
    {
        BuildWithPreset(BuildTarget.Android, BuildTypeEnum.DEV_BUILD, "BuildOutput");
    }

    [MenuItem("Tools/自定义构建版本", false, 100)]
    public static void BuildCustom()
    {
        BuildWindow.ShowWindow();
    }

    [MenuItem("Tools/上传到宝塔", false, 110)]
    public static void UploadBuildToBaoTaMenu()
    {
        string buildPath = EditorUtility.OpenFolderPanel("选择要上传的构建目录", "BuildOutput", "");
        if (string.IsNullOrEmpty(buildPath))
        {
            return;
        }

        string versionFolderName = Path.GetFileName(buildPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(versionFolderName))
        {
            EditorUtility.DisplayDialog("上传失败", "无法识别构建目录名称。", "确定");
            return;
        }

        BuildTypeEnum buildType = InferBuildTypeFromFolderName(versionFolderName);
        if (buildType == BuildTypeEnum.UNKNOWN)
        {
            EditorUtility.DisplayDialog(
                "上传失败",
                $"无法从目录名识别构建类型: {versionFolderName}\n\n目录名需以 _d / _u 结尾，或无后缀表示正式版。",
                "确定");
            return;
        }

        Debug.Log($"[BuildScript] 手动上传到宝塔，目录: {buildPath}, 版本: {versionFolderName}, 类型: {buildType}");
        UploadBuildVersionToLinuxAsync(buildPath, versionFolderName, buildType);
    }

    /// <summary>
    /// 使用预设参数进行构建；返回 true 表示构建成功（编辑器内取消/失败返回 false，且不会 Exit 编辑器）。
    /// </summary>
    public static bool BuildWithPreset(BuildTarget buildTarget, BuildTypeEnum buildType, string outputPath,
        bool autoUpload = false)
    {
        Building_Path = null;
        Build_Type_Enum = BuildTypeEnum.UNKNOWN;
        LastReleaseBundleVersionForS3 = null;

        Debug.Log($"[BuildScript] 开始一键构建: {buildTarget} - {buildType}");
        // ========== 步骤1: 清理旧文件 ==========
        Debug.Log("[BuildScript] 步骤1: 清理旧的构建文件...");
        CleanBuildOutput(outputPath);
        CleanBundleSource(buildType);
        Debug.Log("[BuildScript] 清理完成，准备开始构建");
        // 记录原本的 bundleVersion
        string originalVersion = PlayerSettings.bundleVersion;
        bool versionChanged = false;

        // 切换 ScriptingDefine
        string buildTypeStr = buildType.ToString();
        SwitchScriptingDefine(buildType, buildTypeStr);

        // 使用当前项目版本号
        string version = PlayerSettings.bundleVersion;

        // 生成输出文件名
        string outputName = GenerateOutputName(buildType, version);

        Debug.Log($"[BuildScript] Build Info =>\n" +
                  $"  outputPath: {outputPath}\n" +
                  $"  outputName: {outputName}\n" +
                  $"  buildTarget: {buildTarget}\n" +
                  $"  buildType: {buildType}\n" +
                  $"  version: {version}");

        if (buildType == BuildTypeEnum.RELEASE_BUILD)
        {
            SetAddressablesProfile(buildType);
            ApplyAddressablesReleaseVersionVariableForProd(version);
            LastReleaseBundleVersionForS3 = version;
        }

        // 获取场景
        string[] scenes = GetEnabledScenes();
        Debug.Log($"[BuildScript] scenes: {string.Join(", ", scenes)}");

        // 生成输出路径
        string locationPathName = GetLocationPathName(buildTarget, outputPath, outputName);
        Debug.Log($"[BuildScript] locationPathName: {locationPathName}");

        // RELEASE + WebGL + 自動上傳 S3：構建前先檢查 S3 是否已存在當前 v- 版本（避免構建完才發現無法上傳）
        if (autoUpload && buildType == BuildTypeEnum.RELEASE_BUILD && buildTarget == BuildTarget.WebGL)
        {
            if (!TryPrecheckS3ReleaseVersionBeforeBuild(version))
            {
                LastReleaseBundleVersionForS3 = null;
                return false;
            }
        }

        // 执行 Build（取消或失败时不再弹成功框、不上传）
        if (!DoBuild(scenes, locationPathName, buildTarget))
        {
            LastReleaseBundleVersionForS3 = null;
            if (versionChanged)
            {
                PlayerSettings.bundleVersion = originalVersion;
            }

            EditorUtility.DisplayDialog(
                "构建已取消或失败",
                $"构建未成功。\n结果: 未生成输出\n路径: {locationPathName}",
                "确定");
            return false;
        }

        // 恢复版本号（如果被修改）
        if (versionChanged)
        {
            PlayerSettings.bundleVersion = originalVersion;
            Debug.Log($"[BuildScript] 已将 bundleVersion 还原为: {originalVersion}");
        }

        Debug.Log($"[BuildScript] ✅ 构建完成！输出路径: {locationPathName}");
        Debug.Log($"[BuildScript] ✅ 构建完成！autoUpload: {autoUpload}");
        Debug.Log($"[BuildScript] ✅ 构建完成！Building_Path: {Building_Path}");
        // 如果启用了自动上传（延迟执行，避免构建后主线程/文件锁未释放导致卡住）
        if (autoUpload && !string.IsNullOrEmpty(Building_Path))
        {
            // 快照变量，避免闭包捕获到后续变化的值
            string delayBuildPath = Building_Path;
            string delayOutputName = outputName;
            BuildTypeEnum delayBuildType = buildType;
            BuildTarget delayBuildTarget = buildTarget;
            string delayVersion = version;
            float delaySeconds = 3f;
            float startTime = (float)EditorApplication.timeSinceStartup;
            EditorApplication.CallbackFunction delayCallback = null;
            delayCallback = () =>
            {
                if ((float)EditorApplication.timeSinceStartup - startTime < delaySeconds)
                    return;
                EditorApplication.update -= delayCallback;
                Debug.Log($"[BuildScript] 延迟{delaySeconds}秒后开始自动上传...");
                if (delayBuildType == BuildTypeEnum.RELEASE_BUILD && delayBuildTarget == BuildTarget.WebGL)
                {
                    UploadBuildVersionToS3Async(delayBuildPath, delayOutputName, delayBuildType, delayVersion);
                }
                else
                {
                    UploadBuildVersionToLinuxAsync(delayBuildPath, delayOutputName, delayBuildType, delayVersion);
                }
            };
            EditorApplication.update += delayCallback;
        }
        else
        {
            // 可选：构建完成后打开文件夹
            if (EditorUtility.DisplayDialog("构建完成",
                    $"构建成功！\n输出路径: {locationPathName}\n\n是否打开输出文件夹？",
                    "打开", "关闭"))
            {
                EditorUtility.RevealInFinder(locationPathName);
            }
        }

        return true;
    }

    /// <summary>
    /// 上传构建版本到Linux服务器（可 await；CLI / Runner 請用此方法以取得是否成功）。
    /// </summary>
    /// <param name="bundleVersionForHistory">構建時使用的 bundle 版本號（與 PlayerSettings.bundleVersion 一致），上傳成功後寫入編輯器記錄供 BuildWindow 下次 +1。</param>
    public static Task<bool> UploadBuildVersionToLinuxTaskAsync(string buildPath, string versionFolderName,
        BuildTypeEnum buildType, string bundleVersionForHistory = null, bool? isBatchMode = null)
    {
        return UploadBuildVersionToLinuxCoreAsync(buildPath, versionFolderName, buildType, bundleVersionForHistory,
            isBatchMode);
    }

    private static async Task<bool> UploadBuildVersionToLinuxCoreAsync(string buildPath, string versionFolderName,
        BuildTypeEnum buildType, string bundleVersionForHistory, bool? isBatchMode = null)
    {
        if (string.IsNullOrEmpty(buildPath))
        {
            Debug.LogWarning("[BuildScript] 构建路径为空，无法上传");
            return false;
        }

        bool batch = isBatchMode ?? Application.isBatchMode;

        Debug.Log("[BuildScript] 开始上传构建版本到Linux服务器...");

        if (!batch)
            EditorUtility.DisplayProgressBar("上传中", "准备上传...", 0.1f);

        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string bundleSourceFolderName = GetBundleSourceFolderNameForUpload(buildType);
            string bundleSourcePath = Path.Combine(projectRoot, bundleSourceFolderName);

            if (!Directory.Exists(bundleSourcePath))
            {
                if (!batch)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("上传失败",
                        $"BundleSource文件夹不存在: {bundleSourcePath}\n\n请先构建Addressables。",
                        "确定");
                }
                else
                {
                    Debug.LogError(
                        $"[BuildScript] BundleSource 不存在: {bundleSourcePath}，请先构建 Addressables。");
                }

                return false;
            }

            // batchmode 下 TryCliPostUploadSync 在主线程 GetResult() 阻塞；UploadBuildVersionAsync 内已在首次 await SSH 前 Task.Yield+ConfigureAwait(false)，避免与主线程死锁。
            bool success = await SSHUploader.UploadBuildVersionAsync(
                buildPath,
                bundleSourcePath,
                versionFolderName,
                buildType,
                (progress) =>
                {
                    if (!batch)
                        DispatchEditorUi(() => EditorUtility.DisplayProgressBar("上传中", progress, 0.8f));
                }
            );

            if (!batch)
                DispatchEditorUi(EditorUtility.ClearProgressBar);

            if (success)
            {
                // 不在此写 EditorPrefs：CLI 上传在 ThreadPool 上完成时延续可能在子线程，须由主线程调用方保存（见 TryCliPostUploadSync / UploadBuildVersionToLinuxAsync）。
                Debug.Log("[BuildScript] ✅ 上传和软链接更新完成！");
                return true;
            }

            Debug.LogError("[BuildScript] Linux 上传失败，请查看 Console。");
            return false;
        }
        catch (Exception e)
        {
            if (!batch)
                DispatchEditorUi(EditorUtility.ClearProgressBar);
            Debug.LogError($"[BuildScript] 上传异常: {e.Message}\n{e.StackTrace}");
            if (!batch)
                DispatchEditorUi(() =>
                    EditorUtility.DisplayDialog("上传异常", $"上传时发生错误: {e.Message}", "确定"));
            return false;
        }
    }

    /// <summary>
    /// 上传构建版本到Linux服务器（编辑器内异步，带弹窗）。
    /// </summary>
    public static async void UploadBuildVersionToLinuxAsync(string buildPath, string versionFolderName,
        BuildTypeEnum buildType, string bundleVersionForHistory = null)
    {
        bool batch = Application.isBatchMode;
        bool success = await UploadBuildVersionToLinuxCoreAsync(buildPath, versionFolderName, buildType,
            bundleVersionForHistory, batch);
        if (success && !string.IsNullOrEmpty(bundleVersionForHistory))
            BuildBundleVersionHistory.SaveLastUploadedBundleVersionFromAnyThread(buildType, bundleVersionForHistory);
        if (batch)
            return;

        if (success)
        {
            string projectName = SSHUploader.GetProjectName();
            DispatchEditorUi(() =>
                EditorUtility.DisplayDialog("上传成功",
                    $"构建版本已上传到服务器并更新软链接！\n\n" +
                    $"项目: {projectName}\n" +
                    $"版本: {versionFolderName}\n" +
                    $"软链接: /www/wwwroot/{projectName}/Current",
                    "确定"));
        }
        else
        {
            DispatchEditorUi(() =>
                EditorUtility.DisplayDialog("上传失败",
                    "上传过程中出现错误，请查看Console日志。",
                    "确定"));
        }
    }

    /// <summary>
    /// 將 WebGL 構建目錄與 BundleSource 上傳至 S3（與 SSH 流程同源路徑）；可 await，供 CLI 判斷成功與否。
    /// </summary>
    private static async Task<bool> UploadBuildVersionToS3TaskAsync(string buildPath, string versionFolderName,
        BuildTypeEnum buildType, string releaseBundleVersionLabel = null, bool? isBatchMode = null)
    {
        if (string.IsNullOrEmpty(buildPath))
        {
            Debug.LogWarning("[BuildScript] 构建路径为空，无法上传 S3");
            return false;
        }

        bool batch = isBatchMode ?? Application.isBatchMode;

        Debug.Log($"[BuildScript] 开始上传构建版本到 S3... 本地输出目录: {versionFolderName}" +
                  (buildType == BuildTypeEnum.RELEASE_BUILD && !string.IsNullOrEmpty(releaseBundleVersionLabel)
                      ? $", S3 v- 段: {releaseBundleVersionLabel}"
                      : ""));

        if (!batch)
            EditorUtility.DisplayProgressBar("上传到 S3", "准备上传...", 0.05f);

        AWSS3UploaderSettings.S3Profile s3Profile = null;
        CancellationTokenSource s3UploadTimeoutCts = null;
        try
        {
            SetAddressablesProfile(buildType);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string bundleSourceFolderName = GetBundleSourceFolderNameForUpload(buildType);
            string bundleSourcePath = Path.Combine(projectRoot, bundleSourceFolderName);

            if (!Directory.Exists(bundleSourcePath))
            {
                if (!batch)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("上传失败",
                        $"BundleSource 文件夹不存在: {bundleSourcePath}\n\n请先构建 Addressables。",
                        "确定");
                }
                else
                {
                    Debug.LogError(
                        $"[BuildScript] BundleSource 不存在: {bundleSourcePath}，请先构建 Addressables。");
                }

                return false;
            }

            s3Profile = CreateConfiguredS3ProfileForUpload(buildPath, bundleSourcePath, buildType,
                releaseBundleVersionLabel);
            if (s3Profile == null)
            {
                if (!batch)
                    EditorUtility.ClearProgressBar();
                return false;
            }

            if (!AWSS3UploaderAPI.IsGameUploadSettingsValid(s3Profile))
            {
                if (!batch)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("上传失败",
                        "S3 主游戏上传配置无效：请检查 AWSS3Uploader 中对应 Profile 的桶、密钥，以及 WebGL 输出路径是否存在。",
                        "确定");
                }
                else
                    Debug.LogError("[BuildScript] S3 主游戏上传配置无效。");

                return false;
            }

            if (s3Profile.UploadAssetBundle && !AWSS3UploaderAPI.IsAssetBundleSettingsValid(s3Profile))
            {
                if (!batch)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("上传失败",
                        "S3 AssetBundle 配置无效：请检查 Bundle 目录与 AWSS3Uploader 设置。",
                        "确定");
                }
                else
                    Debug.LogError("[BuildScript] S3 AssetBundle 配置无效。");

                return false;
            }

            CancellationToken s3UploadToken = CancellationToken.None;
            if (batch)
            {
                string timeoutMinutesArg = GetArg("s3UploadTimeoutMinutes", "120");
                int timeoutMinutes;
                if (!int.TryParse(timeoutMinutesArg, out timeoutMinutes))
                {
                    timeoutMinutes = 120;
                }

                timeoutMinutes = Mathf.Clamp(timeoutMinutes, 5, 720);
                s3UploadTimeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
                s3UploadToken = s3UploadTimeoutCts.Token;
                Debug.Log($"[BuildScript] S3 上传超时保护已启用：{timeoutMinutes} 分钟（可用 -s3UploadTimeoutMinutes 覆盖）");
            }

            // 与 SSH 上传相同：Jenkins 等可能未 -batchmode；须用 Prewarm 快照，避免在 ConfigureAwait(false) 延续上调用 Application.isBatchMode。
            if (SSHUploader.GetShouldUseThreadPoolHopPreferPrewarmed())
            {
                await Task.Run(() => { }).ConfigureAwait(false);
            }

            var result = await AWSS3UploaderAPI.FullUploadAsync(
                s3Profile,
                log => Debug.Log(log),
                _ =>
                {
                    if (!batch)
                        DispatchEditorUi(() =>
                            EditorUtility.DisplayProgressBar("上传到 S3", "正在上传…", 0.5f));
                },
                s3UploadToken,
                skipInitialReleaseVersionPrecheck: true);

            if (!batch)
                DispatchEditorUi(EditorUtility.ClearProgressBar);

            if (result.IsSuccess)
            {
                if (!string.IsNullOrEmpty(releaseBundleVersionLabel))
                    BuildBundleVersionHistory.SaveLastUploadedBundleVersionFromAnyThread(buildType, releaseBundleVersionLabel);

                Debug.Log("[BuildScript] ✅ S3 上传完成");
                return true;
            }

            Debug.LogError(string.IsNullOrEmpty(result.Message)
                ? "[BuildScript] S3 上传失败，请查看 Console。"
                : $"[BuildScript] S3 上传失败: {result.Message}");
            if (!batch)
                DispatchEditorUi(() =>
                    EditorUtility.DisplayDialog("上传失败",
                        string.IsNullOrEmpty(result.Message) ? "请查看 Console 日志。" : result.Message,
                        "确定"));
            return false;
        }
        catch (Exception e)
        {
            if (!batch)
                DispatchEditorUi(EditorUtility.ClearProgressBar);
            Debug.LogError($"[BuildScript] S3 上传异常: {e.Message}\n{e.StackTrace}");
            if (!batch)
                DispatchEditorUi(() =>
                    EditorUtility.DisplayDialog("上传异常", $"S3 上传时发生错误: {e.Message}", "确定"));
            return false;
        }
        finally
        {
            s3UploadTimeoutCts?.Dispose();
            if (s3Profile != null)
                s3Profile.SecretAccessKey = null;
        }
    }

    /// <summary>
    /// 將 WebGL 構建目錄與 BundleSource 上傳至 S3（與 SSH 流程同源路徑：本機 build 輸出 + Release 對應的 Bundle 目錄）。
    /// </summary>
    /// <param name="versionFolderName">本機輸出目錄名（含時間戳），僅用於日誌與提示。</param>
    /// <param name="releaseBundleVersionLabel">RELEASE 時 S3 <c>v-</c> 段與 Addressables <c>ReleaseVersion</c> 使用的 bundle 版本號；可為 null（CLI 應傳 <see cref="LastReleaseBundleVersionForS3"/>）。</param>
    public static async void UploadBuildVersionToS3Async(string buildPath, string versionFolderName,
        BuildTypeEnum buildType, string releaseBundleVersionLabel = null)
    {
        bool batch = Application.isBatchMode;
        bool ok = await UploadBuildVersionToS3TaskAsync(buildPath, versionFolderName, buildType,
            releaseBundleVersionLabel, batch);
        if (batch || !ok)
            return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string bundleSourcePath = Path.Combine(projectRoot, GetBundleSourceFolderNameForUpload(buildType));
        AWSS3UploaderSettings.S3Profile s3ProfileForDialog =
            CreateConfiguredS3ProfileForUpload(buildPath, bundleSourcePath, buildType, releaseBundleVersionLabel);
        if (s3ProfileForDialog == null)
            return;

        try
        {
            string projectName = SSHUploader.GetProjectName();
            string s3VerLine = buildType == BuildTypeEnum.RELEASE_BUILD &&
                               !string.IsNullOrEmpty(s3ProfileForDialog.ReleaseUploadVersionSegment)
                ? $"S3 版本段 (v-): {s3ProfileForDialog.ReleaseUploadVersionSegment}\n"
                : "";
            DispatchEditorUi(() =>
                EditorUtility.DisplayDialog("上传成功",
                    $"WebGL 与 AssetBundle 已上传到 S3。\n\n" +
                    $"项目: {projectName}\n" +
                    s3VerLine +
                    $"本地输出目录: {versionFolderName}\n" +
                    $"WebGL 前缀: {s3ProfileForDialog.GetWebGlUploadKeyPrefix()}\n" +
                    $"Bundle 前缀: {s3ProfileForDialog.GetAssetBundleS3FullPrefix()}",
                    "确定"));
        }
        finally
        {
            s3ProfileForDialog.SecretAccessKey = null;
        }
    }

    private static string ToAbsolutePathUnderProject(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);
        return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path));
    }

    private static AWSS3UploaderSettings.S3Profile FindS3ProfileForBuild(AWSS3UploaderSettings settings,
        BuildTypeEnum buildType)
    {
        if (settings?.Profiles == null || settings.Profiles.Count == 0)
            return null;

        string primary = GetProfileName(buildType);
        foreach (var p in settings.Profiles)
        {
            if (p.ProfileName == primary)
                return p;
        }

        if (buildType == BuildTypeEnum.RELEASE_BUILD)
        {
            string[] aliases = { "AWS_PROD", "AWS_PRO", "AWS_PRODUCTION" };
            foreach (var alias in aliases)
            {
                foreach (var p in settings.Profiles)
                {
                    if (string.Equals(p.ProfileName, alias, StringComparison.OrdinalIgnoreCase))
                        return p;
                }
            }
        }

        return settings.Profiles[0];
    }

    /// <summary>
    /// 從 Editor 下的 AWSS3UploaderSettings.asset 複製 Profile，並綁定本次構建的 WebGL 目錄與 Bundle 目錄。
    /// </summary>
    private static AWSS3UploaderSettings.S3Profile CreateConfiguredS3ProfileForUpload(
        string webGlBuildPath,
        string bundleSourcePath,
        BuildTypeEnum buildType,
        string releaseBundleVersionLabel)
    {
        var settings = AWSS3UploaderSettings.LoadEditorSettings(false);
        if (settings == null)
        {
            Debug.LogError("[BuildScript] 找不到 AWSS3UploaderSettings.asset（路徑: " +
                           AWSS3UploaderSettings.EditorSettingsAssetPath + "），无法上传 S3。");
            return null;
        }

        var baseProfile = FindS3ProfileForBuild(settings, buildType);
        if (baseProfile == null)
        {
            Debug.LogError("[BuildScript] AWSS3UploaderSettings 中没有任何 S3 Profile。");
            return null;
        }

        var s3Profile = baseProfile.Clone();

        // 憑證以 Editor 下 AWSS3UploaderSettings.asset 為主；若未填 Secret 則嘗試環境變數（供 CI）。
        if (string.IsNullOrEmpty(s3Profile.SecretAccessKey))
        {
            string envSecret = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            if (!string.IsNullOrEmpty(envSecret))
                s3Profile.SecretAccessKey = envSecret;
        }

        s3Profile.LocalDirectoryPath = ToAbsolutePathUnderProject(webGlBuildPath);
        s3Profile.SetAssetBundleDirectoryPath(bundleSourcePath);
        s3Profile.UploadAssetBundle = true;
        s3Profile.SyncWithAddressableProfile = false;
        if (s3Profile.MaxConcurrentUploads < MAX_CONCURRENT_UPLOADS)
            s3Profile.MaxConcurrentUploads = MAX_CONCURRENT_UPLOADS;

        if (buildType == BuildTypeEnum.RELEASE_BUILD)
        {
            s3Profile.UploadToRootDirectory = false;
            string root = string.IsNullOrWhiteSpace(s3Profile.AssetBundleS3Root)
                ? "assets"
                : s3Profile.AssetBundleS3Root.Trim().Trim('/');
            s3Profile.AssetBundleS3Root = root;
            s3Profile.AssetBundleS3Path = GetBundleSourceFolderNameForUpload(buildType);

            string verSeg = SanitizeReleaseUploadVersionSegment(releaseBundleVersionLabel);
            if (!string.IsNullOrEmpty(verSeg))
                s3Profile.ReleaseUploadVersionSegment = verSeg;
            else
                Debug.LogWarning("[BuildScript] RELEASE 上傳 S3：bundle 版本號為空，未設定 v- 目錄段；請確認傳入 releaseBundleVersionLabel。");
        }

        Debug.Log("[BuildScript] S3 上传使用 Profile: " + baseProfile.ProfileName +
                  $"\n  WebGL 目录: {s3Profile.LocalDirectoryPath}" +
                  $"\n  Bundle 目录: {bundleSourcePath}" +
                  $"\n  WebGL S3 前缀: {s3Profile.GetWebGlUploadKeyPrefix()}" +
                  $"\n  Bundle S3 前缀: {s3Profile.GetAssetBundleS3FullPrefix()}");

        return s3Profile;
    }

    /// <summary>
    /// 構建開始前專用：與 RELEASE 上傳相同的 S3 Key 規則（含 v- 段），不綁本地 WebGL 輸出路徑。
    /// </summary>
    private static AWSS3UploaderSettings.S3Profile CreateS3ProfileForReleaseUploadPrecheck(string releaseBundleVersionLabel)
    {
        var settings = AWSS3UploaderSettings.LoadEditorSettings(false);
        if (settings == null)
        {
            Debug.LogError("[BuildScript] 找不到 AWSS3UploaderSettings.asset，無法執行 S3 預檢。");
            return null;
        }

        var baseProfile = FindS3ProfileForBuild(settings, BuildTypeEnum.RELEASE_BUILD);
        if (baseProfile == null)
        {
            Debug.LogError("[BuildScript] AWSS3UploaderSettings 中没有任何 S3 Profile。");
            return null;
        }

        var s3Profile = baseProfile.Clone();
        if (string.IsNullOrEmpty(s3Profile.SecretAccessKey))
        {
            string envSecret = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            if (!string.IsNullOrEmpty(envSecret))
                s3Profile.SecretAccessKey = envSecret;
        }

        s3Profile.LocalDirectoryPath = "";
        s3Profile.SetAssetBundleDirectoryPath("");
        s3Profile.UploadAssetBundle = true;
        s3Profile.SyncWithAddressableProfile = false;

        s3Profile.UploadToRootDirectory = false;
        string root = string.IsNullOrWhiteSpace(s3Profile.AssetBundleS3Root)
            ? "assets"
            : s3Profile.AssetBundleS3Root.Trim().Trim('/');
        s3Profile.AssetBundleS3Root = root;
        s3Profile.AssetBundleS3Path = GetBundleSourceFolderNameForUpload(BuildTypeEnum.RELEASE_BUILD);

        string verSeg = SanitizeReleaseUploadVersionSegment(releaseBundleVersionLabel);
        if (!string.IsNullOrEmpty(verSeg))
            s3Profile.ReleaseUploadVersionSegment = verSeg;

        return s3Profile;
    }

    /// <summary>
    /// RELEASE + WebGL + 自動上傳 S3：在構建前檢查桶內是否已有當前 v- 版本前綴；若已佔用則彈窗並返回 false。
    /// </summary>
    private static bool TryPrecheckS3ReleaseVersionBeforeBuild(string releaseBundleVersionLabel)
    {
        AWSS3UploaderSettings.S3Profile profile = CreateS3ProfileForReleaseUploadPrecheck(releaseBundleVersionLabel);
        if (profile == null)
        {
            EditorUtility.DisplayDialog("无法开始构建", "无法加载 AWSS3Uploader 设置，已中止。", "确定");
            return false;
        }

        try
        {
            // 不可在主執行緒對 UniTask 使用 GetResult() 同步阻塞（會卡住 PlayerLoop，導致「Not yet completed」）。
            // 改為在線程池跑完整個預檢，再以 Task 同步等待結果。
            AWSS3UploaderAPI.ReleaseVersionPathPrecheckResult pre = Task.Run(() =>
                AWSS3UploaderAPI.PrecheckReleaseVersionPathNotOccupiedOnS3Async(
                    profile,
                    checkWebGlPrefix: true,
                    checkAssetBundlePrefix: true,
                    msg => Debug.Log($"[BuildScript] S3 预检: {msg}"),
                    CancellationToken.None,
                    skipLocalPathRequirements: true).AsTask()).GetAwaiter().GetResult();

            if (!pre.IsAllowed)
            {
                EditorUtility.DisplayDialog(
                    "无法开始构建",
                    string.IsNullOrEmpty(pre.Message) ? "S3 版本目录检查未通过，已取消构建。" : pre.Message,
                    "确定");
                Debug.LogWarning("[BuildScript] S3 预检未通过，已取消构建。");
                return false;
            }

            Debug.Log("[BuildScript] S3 版本目录预检通过，将开始构建。");
            return true;
        }
        finally
        {
            profile.SecretAccessKey = null;
        }
    }

    /// <summary>
    /// 从构建目录名推断构建类型。
    /// 例如：2503091200_1_0_0_d -> DEV_BUILD
    /// </summary>
    private static BuildTypeEnum InferBuildTypeFromFolderName(string versionFolderName)
    {
        if (string.IsNullOrEmpty(versionFolderName))
            return BuildTypeEnum.UNKNOWN;

        if (versionFolderName.EndsWith("_d", StringComparison.OrdinalIgnoreCase))
            return BuildTypeEnum.DEV_BUILD;

        if (versionFolderName.EndsWith("_u", StringComparison.OrdinalIgnoreCase))
            return BuildTypeEnum.UAT_BUILD;

        return BuildTypeEnum.RELEASE_BUILD;
    }

    /// <summary>
    /// 获取BundleSource文件夹名称（用于上传）
    /// </summary>
    private static string GetBundleSourceFolderNameForUpload(BuildTypeEnum buildType)
    {
        switch (buildType)
        {
            case BuildTypeEnum.DEV_BUILD:
                return "BundleSource_DEV";
            case BuildTypeEnum.UAT_BUILD:
                return "BundleSource_UAT";
            case BuildTypeEnum.RELEASE_BUILD:
                return "BundleSource";
            default:
                return "BundleSource_DEV";
        }
    }
    
    
        /// <summary>
    /// 清理构建输出目录
    /// </summary>
    private static void CleanBuildOutput(string outputPath)
    {
        try
        {
            if (string.IsNullOrEmpty(outputPath))
                return;

            // 获取项目根目录
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullOutputPath = Path.Combine(projectRoot, outputPath);

            if (Directory.Exists(fullOutputPath))
            {
                Debug.Log($"[BuildScript] 清理构建输出目录: {fullOutputPath}");
                
                // 删除目录下的所有文件和子目录
                string[] files = Directory.GetFiles(fullOutputPath);
                string[] dirs = Directory.GetDirectories(fullOutputPath);

                foreach (string file in files)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        Debug.Log($"[BuildScript] 已删除文件: {file}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[BuildScript] 删除文件失败: {file}, 错误: {e.Message}");
                    }
                }

                foreach (string dir in dirs)
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        Debug.Log($"[BuildScript] 已删除目录: {dir}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[BuildScript] 删除目录失败: {dir}, 错误: {e.Message}");
                    }
                }

                Debug.Log($"[BuildScript] ✅ 构建输出目录清理完成");
            }
            else
            {
                Debug.Log($"[BuildScript] 构建输出目录不存在，跳过清理: {fullOutputPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildScript] 清理构建输出目录异常: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 清理 BundleSource 目录
    /// </summary>
    private static void CleanBundleSource(BuildTypeEnum buildType)
    {
        try
        {
            // 获取项目根目录
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            
            // 根据构建类型获取 BundleSource 文件夹名称
            string bundleSourceFolderName = GetBundleSourceFolderNameForUpload(buildType);
            string bundleSourcePath = Path.Combine(projectRoot, bundleSourceFolderName);

            if (Directory.Exists(bundleSourcePath))
            {
                Debug.Log($"[BuildScript] 清理 BundleSource 目录: {bundleSourcePath}");
                
                // 删除目录下的所有文件和子目录
                string[] files = Directory.GetFiles(bundleSourcePath, "*", SearchOption.AllDirectories);
                string[] dirs = Directory.GetDirectories(bundleSourcePath);

                // 先删除所有文件
                foreach (string file in files)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[BuildScript] 删除文件失败: {file}, 错误: {e.Message}");
                    }
                }

                // 再删除所有目录（从最深层开始）
                Array.Sort(dirs, (a, b) => b.Length.CompareTo(a.Length));
                foreach (string dir in dirs)
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[BuildScript] 删除目录失败: {dir}, 错误: {e.Message}");
                    }
                }

                Debug.Log($"[BuildScript] ✅ BundleSource 目录清理完成: {bundleSourceFolderName}");
            }
            else
            {
                Debug.Log($"[BuildScript] BundleSource 目录不存在，跳过清理: {bundleSourcePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildScript] 清理 BundleSource 目录异常: {e.Message}\n{e.StackTrace}");
        }
    }

 
}