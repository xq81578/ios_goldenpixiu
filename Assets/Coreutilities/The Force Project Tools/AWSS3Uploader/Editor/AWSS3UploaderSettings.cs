using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
#endif

/// <summary>
/// AWS S3 上傳器設定檔管理
/// 負責管理多個 S3 設定檔的配置和持久化
/// </summary>
[Serializable]
public class AWSS3UploaderSettings : ScriptableObject
{
    #region Nested Classes

    /// <summary>
    /// S3 設定檔資料結構
    /// 包含單個 AWS S3 連線和上傳設定的完整資訊
    /// </summary>
    [Serializable]
    public class S3Profile
    {
        #region Public Fields

        [Header("設定檔資訊")]
        public string ProfileName = "預設設定";
        public bool SyncWithAddressableProfile = false;

        [Header("S3 設定")]
        public string S3BucketName = "";
        private string _s3KeyPrefix = ""; // 不再序列化，僅作為臨時存儲

        [Tooltip("WebGL 主程式在桶內的區段根目錄（預設 games）。完整路徑為 games/{專案名}，專案名與 SSHUploader.GetProjectName() 一致")]
        public string WebGlBuildS3Root = "games";

        [Header("AWS 认证")]
        public string AccessKeyId = "";
        [Tooltip("勿提交到版本库。凭据存于 Editor 下 AWSS3UploaderSettings.asset，不随玩家包发布；CLI/CI 可设环境变量 AWS_SECRET_ACCESS_KEY。")]
        public string SecretAccessKey = "";
        public string AwsRegion = "ap-northeast-1";

        [Header("CloudFront")]
        [Tooltip("上傳完成後自動執行 Invalidation 時使用的 Distribution ID；留空則跳過清快取")]
        public string CloudFrontDistributionId = "E11W8OXUPNBI1E";

        [Header("上傳設定")]
        [Range(1, 10)]
        public int MaxConcurrentUploads = 3;
        public bool UploadToRootDirectory = false;

        [Header("AssetBundle 設定")]
        public bool UploadAssetBundle = false;
        private string _assetBundleDirectoryPath = ""; // 不再序列化，僅作為臨時存儲
        public string AssetBundleS3Path = "";

        [Tooltip("AssetBundle 在桶內的區段根目錄（預設 assets）。完整路徑為 assets/{專案名}/[Bundle子目錄]，專案名與 SSH 上傳一致")]
        public string AssetBundleS3Root = "assets";

        [Header("進階上傳設定")]
        public bool ExcludeBundleSourceFromClear = true;
        public bool SkipDuplicateBundleUploads = true;

        #endregion

        #region Public Properties

        /// <summary>
        /// 僅執行時設定（不序列化）。正式 RELEASE 時為 <b>PlayerSettings.bundleVersion</b>（與自定義構建「版本號」一致，不含時間戳），
        /// S3 路徑為 …/v-{此值}/（WebGL 與 Bundle 均帶此段）。
        /// </summary>
        public string ReleaseUploadVersionSegment { get; set; } = "";

        public string LocalDirectoryPath { get; set; } = "";

        public string GetS3BucketName()
        {
            if (SyncWithAddressableProfile)
            {
#if UNITY_EDITOR
                var profileData = GetAddressableProfileData();
                if (!string.IsNullOrEmpty(profileData.remoteLoadPath) && profileData.remoteLoadPath.StartsWith("https://"))
                {
                    try
                    {
                        var uri = new Uri(profileData.remoteLoadPath);
                        return uri.Host;
                    }
                    catch
                    {
                        // 忽略錯誤，返回手動設定值
                    }
                }
#endif
            }
            return S3BucketName;
        }

        public string GetS3KeyPrefix()
        {
            if (SyncWithAddressableProfile)
            {
#if UNITY_EDITOR
                var profileData = GetAddressableProfileData();
                if (!string.IsNullOrEmpty(profileData.projectName))
                {
                    return profileData.projectName;
                }
#endif
            }
            return _s3KeyPrefix;
        }

        /// <summary>
        /// 上傳用專案目錄名，與 <see cref="CoreUtilities.The_Force_Project_Tools.Editor.SSHUploader.GetProjectName"/> 相同：
        /// Addressables 作用中 Profile 的 ProjectName，失敗時為 Application.productName。
        /// </summary>
        public string GetUploadProjectName()
        {
#if UNITY_EDITOR
            try
            {
                return NormalizeS3Segment(CoreUtilities.The_Force_Project_Tools.Editor.SSHUploader.GetProjectName());
            }
            catch
            {
                return NormalizeS3Segment(Application.productName);
            }
#else
            return NormalizeS3Segment(Application.productName);
#endif
        }

        /// <summary>
        /// WebGL 主程式在儲存桶內的 Key 前綴：{WebGlBuildS3Root}/{GetUploadProjectName()}[/v-{段}]；若「僅 WebGL 根目錄」則僅 {WebGlBuildS3Root}。
        /// </summary>
        public string GetWebGlUploadKeyPrefix()
        {
            string root = NormalizeS3Segment(WebGlBuildS3Root);
            if (string.IsNullOrEmpty(root))
                root = "games";

            if (UploadToRootDirectory)
                return root;

            string projectName = GetUploadProjectName();
            string basePrefix = string.IsNullOrEmpty(projectName) ? root : root + "/" + projectName;
            return AppendReleaseVersionDirectoryIfSet(basePrefix);
        }

        /// <summary>
        /// AssetBundle 在儲存桶內的完整 Key 前綴：{AssetBundleS3Root}/{GetUploadProjectName()}[/v-{段}]/[GetAssetBundleS3Path()]
        /// </summary>
        public string GetAssetBundleS3FullPrefix()
        {
            string root = NormalizeS3Segment(AssetBundleS3Root);
            if (string.IsNullOrEmpty(root))
                root = "assets";

            string projectName = GetUploadProjectName();
            string basePrefix = string.IsNullOrEmpty(projectName) ? root : root + "/" + projectName;
            basePrefix = AppendReleaseVersionDirectoryIfSet(basePrefix);

            string bundleRel = NormalizeS3Segment(GetAssetBundleS3Path());
            return string.IsNullOrEmpty(bundleRel) ? basePrefix : basePrefix + "/" + bundleRel;
        }

        /// <summary>
        /// CloudFront 失效用的路徑前綴（不含開頭 /）：{專案名} 或 {專案名}/v-{段}。
        /// </summary>
        public string GetWebGlViewerPathPrefixForCloudFront()
        {
            string projectName = GetUploadProjectName();
            if (string.IsNullOrEmpty(projectName))
                return "";

            string seg = NormalizeS3Segment(ReleaseUploadVersionSegment);
            if (string.IsNullOrEmpty(seg))
                return projectName;

            return projectName + "/v-" + seg;
        }

        private string AppendReleaseVersionDirectoryIfSet(string basePrefix)
        {
            string seg = NormalizeS3Segment(ReleaseUploadVersionSegment);
            if (string.IsNullOrEmpty(seg))
                return basePrefix;
            return basePrefix + "/v-" + seg;
        }

        /// <summary>
        /// 獲取上傳時使用的 S3 Key 前綴（WebGL 主程式，等同 GetWebGlUploadKeyPrefix）
        /// </summary>
        public string GetUploadS3KeyPrefix()
        {
            return GetWebGlUploadKeyPrefix();
        }

        private static string NormalizeS3Segment(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";
            return s.Trim().Trim('/').Replace('\\', '/');
        }

        public void SetS3KeyPrefix(string value)
        {
            _s3KeyPrefix = value;
        }

        public string GetAssetBundleDirectoryPath()
        {
            if (SyncWithAddressableProfile)
            {
#if UNITY_EDITOR
                var profileData = GetAddressableProfileData();
                if (!string.IsNullOrEmpty(profileData.remoteBuildPath))
                {
                    var bundleSourceName = GetBundleFolderSegmentFromRemoteBuildPath(profileData.remoteBuildPath);
                    string projectPath = Directory.GetParent(Application.dataPath).FullName;
                    return Path.Combine(projectPath, bundleSourceName);
                }
#endif
            }
            return _assetBundleDirectoryPath;
        }

        public void SetAssetBundleDirectoryPath(string value)
        {
            _assetBundleDirectoryPath = value;
        }

        /// <summary>
        /// AssetBundle 在「assets/專案名/」之下的子路徑（例如 Remote.BuildPath 的 BundleSource_DEV），可為空表示檔案直接放在專案目錄下。
        /// </summary>
        public string GetAssetBundleS3Path()
        {
#if UNITY_EDITOR
            if (SyncWithAddressableProfile)
            {
                var profileData = GetAddressableProfileData();
                return NormalizeS3Segment(GetBundleFolderSegmentFromRemoteBuildPath(profileData.remoteBuildPath));
            }
#endif
            return NormalizeS3Segment(AssetBundleS3Path);
        }

        private static string GetBundleFolderSegmentFromRemoteBuildPath(string remoteBuildPath)
        {
            if (string.IsNullOrEmpty(remoteBuildPath))
                return "";
            var slashIndex = remoteBuildPath.IndexOf('/');
            return slashIndex > 0 ? remoteBuildPath.Substring(0, slashIndex) : remoteBuildPath;
        }

        #endregion

        #region Private Methods

#if UNITY_EDITOR
        private struct AddressableProfileData
        {
            public string remoteLoadPath;
            public string remoteBuildPath;
            public string projectName;
        }

        private AddressableProfileData GetAddressableProfileData()
        {
            var result = new AddressableProfileData();

            try
            {
                if (!AddressableAssetSettingsDefaultObject.SettingsExists)
                    return result;

                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings?.profileSettings == null)
                    return result;

                var profileNames = settings.profileSettings.GetAllProfileNames();
                if (!profileNames.Contains(ProfileName))
                    return result;

                var profileId = settings.profileSettings.GetProfileId(ProfileName);
                if (string.IsNullOrEmpty(profileId))
                    return result;

                result.remoteLoadPath = settings.profileSettings.GetValueByName(profileId, "Remote.LoadPath") ?? "";
                result.remoteBuildPath = settings.profileSettings.GetValueByName(profileId, "Remote.BuildPath") ?? "";
                result.projectName = settings.profileSettings.GetValueByName(profileId, "ProjectName") ?? "";
            }
            catch (Exception)
            {
                // 靜默處理錯誤
            }

            return result;
        }

        public bool ValidateAddressableProfile(out string errorMessage)
        {
            errorMessage = "";

            try
            {
                if (!AddressableAssetSettingsDefaultObject.SettingsExists)
                {
                    errorMessage = "Addressable 設定不存在";
                    return false;
                }

                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings?.profileSettings == null)
                {
                    errorMessage = "Addressable Profile 設定為空";
                    return false;
                }

                var profileNames = settings.profileSettings.GetAllProfileNames();
                if (!profileNames.Contains(ProfileName))
                {
                    errorMessage = $"找不到名稱為 '{ProfileName}' 的 Addressable Profile";
                    return false;
                }

                var profileId = settings.profileSettings.GetProfileId(ProfileName);
                if (string.IsNullOrEmpty(profileId))
                {
                    errorMessage = $"無法獲取 Profile '{ProfileName}' 的 ID";
                    return false;
                }

                var remoteLoadPath = settings.profileSettings.GetValueByName(profileId, "Remote.LoadPath") ?? "";
                var remoteBuildPath = settings.profileSettings.GetValueByName(profileId, "Remote.BuildPath") ?? "";
                var projectName = settings.profileSettings.GetValueByName(profileId, "ProjectName") ?? "";

                var missingValues = new List<string>();
                if (string.IsNullOrEmpty(remoteLoadPath))
                    missingValues.Add("Remote.LoadPath");
                if (string.IsNullOrEmpty(remoteBuildPath))
                    missingValues.Add("Remote.BuildPath");
                if (string.IsNullOrEmpty(projectName))
                    missingValues.Add("ProjectName");

                if (missingValues.Count > 0)
                {
                    errorMessage = $"Profile '{ProfileName}' 中缺少以下設定值：\n{string.Join(", ", missingValues)}";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"檢查 Addressable Profile 時發生錯誤：{ex.Message}";
                return false;
            }
        }

        public bool SyncFromAddressableProfile(out string syncMessage)
        {
            syncMessage = "";

            try
            {
                var profileData = GetAddressableProfileData();

                if (string.IsNullOrEmpty(profileData.remoteLoadPath) &&
                    string.IsNullOrEmpty(profileData.remoteBuildPath) &&
                    string.IsNullOrEmpty(profileData.projectName))
                {
                    syncMessage = $"無法從 Addressable Profile '{ProfileName}' 獲取任何設定";
                    return false;
                }

                var syncedItems = new List<string>();

                // 同步 S3BucketName
                if (!string.IsNullOrEmpty(profileData.remoteLoadPath) && profileData.remoteLoadPath.StartsWith("https://"))
                {
                    try
                    {
                        var uri = new Uri(profileData.remoteLoadPath);
                        S3BucketName = uri.Host;
                        syncedItems.Add("S3 Bucket");
                    }
                    catch
                    {
                        // 忽略錯誤
                    }
                }

                // 同步 AssetBundleDirectoryPath
                if (!string.IsNullOrEmpty(profileData.remoteBuildPath))
                {
                    var bundleSourceName = GetBundleFolderSegmentFromRemoteBuildPath(profileData.remoteBuildPath);
                    string projectPath = Directory.GetParent(Application.dataPath).FullName;
                    _assetBundleDirectoryPath = Path.Combine(projectPath, bundleSourceName);
                    syncedItems.Add("AssetBundle 目錄");
                }

                // 同步 Bundle 在 S3 上的子路徑（與 Remote.BuildPath 首段一致，如 BundleSource_DEV）
                if (!string.IsNullOrEmpty(profileData.remoteBuildPath))
                {
                    AssetBundleS3Path = GetBundleFolderSegmentFromRemoteBuildPath(profileData.remoteBuildPath);
                    syncedItems.Add("AssetBundle S3 子路徑");
                }

                if (syncedItems.Count > 0)
                {
                    syncMessage = $"已同步以下設定：{string.Join(", ", syncedItems)}";
                    return true;
                }
                else
                {
                    syncMessage = "沒有可同步的設定";
                    return false;
                }
            }
            catch (Exception ex)
            {
                syncMessage = $"同步時發生錯誤：{ex.Message}";
                return false;
            }
        }
#endif

        #endregion

        #region Public Methods

        /// <summary>
        /// 創建當前設定檔的深層複製
        /// </summary>
        /// <returns>複製的設定檔物件</returns>
        public S3Profile Clone()
        {
            return new S3Profile
            {
                ProfileName = ProfileName,
                SyncWithAddressableProfile = SyncWithAddressableProfile,
                S3BucketName = S3BucketName,
                _s3KeyPrefix = _s3KeyPrefix,
                AwsRegion = AwsRegion,
                AccessKeyId = AccessKeyId,
                SecretAccessKey = SecretAccessKey,
                CloudFrontDistributionId = CloudFrontDistributionId,
                MaxConcurrentUploads = MaxConcurrentUploads,
                UploadToRootDirectory = UploadToRootDirectory,
                WebGlBuildS3Root = WebGlBuildS3Root,
                UploadAssetBundle = UploadAssetBundle,
                _assetBundleDirectoryPath = _assetBundleDirectoryPath,
                AssetBundleS3Path = AssetBundleS3Path,
                AssetBundleS3Root = AssetBundleS3Root,
                ExcludeBundleSourceFromClear = ExcludeBundleSourceFromClear,
                SkipDuplicateBundleUploads = SkipDuplicateBundleUploads
            };
        }

        /// <summary>
        /// 驗證設定檔是否包含必要的資訊
        /// </summary>
        /// <returns>如果設定檔有效則返回 true</returns>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(ProfileName) &&
                   !string.IsNullOrEmpty(GetS3BucketName()) &&
                   !string.IsNullOrEmpty(AwsRegion) &&
                   !string.IsNullOrEmpty(AccessKeyId) &&
                   !string.IsNullOrEmpty(SecretAccessKey);
        }

        #endregion
    }

    #endregion

    #region Public Fields

    [SerializeField] private List<S3Profile> _profiles = new List<S3Profile>();
    [SerializeField] private int _selectedProfileIndex = 0;

    #endregion

    #region Public Properties

    /// <summary>
    /// 所有設定檔的集合
    /// </summary>
    public List<S3Profile> Profiles => _profiles;

    /// <summary>
    /// 目前選中的設定檔索引
    /// </summary>
    public int SelectedProfileIndex
    {
        get => _selectedProfileIndex;
        set => _selectedProfileIndex = Mathf.Clamp(value, 0, Mathf.Max(0, _profiles.Count - 1));
    }

    /// <summary>
    /// 設定檔總數
    /// </summary>
    public int ProfileCount => _profiles.Count;

    #endregion

    #region Public Methods

    /// <summary>
    /// 獲取目前選中的設定檔
    /// 如果沒有設定檔，會自動創建一個預設設定檔
    /// </summary>
    /// <returns>目前選中的設定檔</returns>
    public S3Profile GetSelectedProfile()
    {
        EnsureProfileExists();
        ValidateSelectedIndex();
        return _profiles[_selectedProfileIndex];
    }

    /// <summary>
    /// 添加新的設定檔
    /// </summary>
    /// <param name="profile">要添加的設定檔</param>
    public void AddProfile(S3Profile profile)
    {
        if (profile == null)
        {
            Debug.LogWarning("嘗試添加空的設定檔");
            return;
        }

        _profiles.Add(profile);
    }

    /// <summary>
    /// 移除指定索引的設定檔
    /// 確保至少保留一個設定檔
    /// </summary>
    /// <param name="index">要移除的設定檔索引</param>
    public void RemoveProfile(int index)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogWarning($"嘗試移除無效索引的設定檔: {index}");
            return;
        }

        if (_profiles.Count <= 1)
        {
            Debug.LogWarning("無法移除最後一個設定檔");
            return;
        }

        _profiles.RemoveAt(index);

        // 調整選中索引
        if (_selectedProfileIndex >= _profiles.Count)
        {
            _selectedProfileIndex = _profiles.Count - 1;
        }
    }

    /// <summary>
    /// 重命名指定索引的設定檔
    /// </summary>
    /// <param name="index">設定檔索引</param>
    /// <param name="newName">新名稱</param>
    public void RenameProfile(int index, string newName)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogWarning($"嘗試重命名無效索引的設定檔: {index}");
            return;
        }

        if (string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("設定檔名稱不能為空");
            return;
        }

        _profiles[index].ProfileName = newName;
    }

    /// <summary>
    /// 複製指定索引的設定檔
    /// </summary>
    /// <param name="index">要複製的設定檔索引</param>
    /// <returns>複製的設定檔，如果失敗則返回 null</returns>
    public S3Profile DuplicateProfile(int index)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogWarning($"嘗試複製無效索引的設定檔: {index}");
            return null;
        }

        var clonedProfile = _profiles[index].Clone();
        clonedProfile.ProfileName += " (複製)";
        _profiles.Add(clonedProfile);

        return clonedProfile;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// 確保至少存在一個設定檔
    /// </summary>
    private void EnsureProfileExists()
    {
        if (_profiles.Count == 0)
        {
            _profiles.Add(new S3Profile());
        }
    }

    /// <summary>
    /// 驗證並修正選中的索引
    /// </summary>
    private void ValidateSelectedIndex()
    {
        if (_selectedProfileIndex < 0 || _selectedProfileIndex >= _profiles.Count)
        {
            _selectedProfileIndex = 0;
        }
    }

    /// <summary>
    /// 檢查索引是否有效
    /// </summary>
    /// <param name="index">要檢查的索引</param>
    /// <returns>如果索引有效則返回 true</returns>
    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < _profiles.Count;
    }

    #endregion

#if UNITY_EDITOR
    #region Editor-Only Asset Paths

    /// <summary>設定檔所在目錄（Editor 下，不會打入 Player/WebGL 包）。</summary>
    public const string EditorSettingsDirectory =
        "Assets/CoreUtilities/The Force Project Tools/AWSS3Uploader/Editor";

    /// <summary>遊戲組設定。</summary>
    public const string EditorSettingsAssetPath = EditorSettingsDirectory + "/AWSS3UploaderSettings.asset";

    /// <summary>平台組設定。</summary>
    public const string EditorPlatformSettingsAssetPath =
        EditorSettingsDirectory + "/AWSS3UploaderSettings_Platform.asset";

    public static AWSS3UploaderSettings LoadEditorSettings(bool platformGroup)
    {
        string path = platformGroup ? EditorPlatformSettingsAssetPath : EditorSettingsAssetPath;
        return AssetDatabase.LoadAssetAtPath<AWSS3UploaderSettings>(path);
    }

    #endregion
#endif
}