using UnityEngine;
using UnityEditor;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon;
using Amazon.Runtime;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// AWS S3 上傳器編輯器視窗
/// 提供圖形化界面來管理 AWS S3 檔案上傳功能
/// </summary>
public class AWSS3UploaderWindow : EditorWindow
{
    #region Constants

    // UI Layout Constants
    private const float UI_MIN_WINDOW_WIDTH = 600f;
    private const float UI_MIN_WINDOW_HEIGHT = 720f;
    private const float UI_BUTTON_HEIGHT = 25f;
    private const float UI_LABEL_WIDTH = 110f;
    private const float UI_PROFILE_SECTION_HEIGHT_COLLAPSED = 35f;
    private const float UI_PROFILE_SECTION_HEIGHT_EXPANDED = 80f;
    private const float UI_AWS_SETTINGS_HEIGHT = 65f;
    private const float UI_UPLOAD_SETTINGS_HEIGHT = 70f;
    private const float UI_ASSETBUNDLE_UPLOAD_HEIGHT = 120f;
    private const float UI_ACTION_BUTTONS_HEIGHT = 100f;
    private const float UI_PROGRESS_HEIGHT = 140f;
    private const float UI_LOG_TITLE_HEIGHT = 20f;
    private const float UI_LOG_CONTROLS_HEIGHT = 35f;
    private const float UI_MARGINS_TOTAL = 35f;
    private const float UI_BOTTOM_MARGIN = 25f;
    private const float UI_MIN_LOG_HEIGHT = 200f;
    private const float UI_UPLOADING_EXTRA_LOG_SPACE = 50f;
    private const float UI_UPLOADING_EXTRA_WINDOW_HEIGHT = 80f;

    // AWS & Upload Constants
    private const int AWS_CONNECTION_TIMEOUT_SECONDS = 60;
    private const int AWS_MAX_RETRY_COUNT = 3;
    private const int MIN_CONCURRENT_UPLOADS = 1;
    private const int MAX_CONCURRENT_UPLOADS = 10;

    // S3 Bucket Name Validation
    private const int S3_BUCKET_NAME_MIN_LENGTH = 3;
    private const int S3_BUCKET_NAME_MAX_LENGTH = 63;

    // 檔案操作相關常數
    private const int MAX_FILE_LIST_DISPLAY_COUNT = 1000;
    private const int FILE_SIZE_DISPLAY_PRECISION = 2;
    private const int LOG_MAX_ENTRIES = 500;
    private const int MAX_PARALLEL_FILE_DISPLAY = 3;

    // 錯誤處理相關常數
    private const int MAX_ERROR_MESSAGE_LENGTH = 200;
    private const int CONNECTION_TEST_TIMEOUT_SECONDS = 30;
    private const int FILE_LIST_TIMEOUT_SECONDS = 60;
    private const int MAX_LOG_FILE_PATH_LENGTH = 60;

    #endregion

    #region Nested Classes

    /// <summary>
    /// S3檔案資訊結構
    /// </summary>
    private struct S3FileInfo
    {
        public long Size;
        public DateTime LastModified;
        public string ETag;
    }

    #endregion

    #region Nested Enums

    /// <summary>
    /// 設定檔類型枚舉
    /// </summary>
    public enum SettingsType
    {
        Game,     // 遊戲組 - Editor/AWSS3UploaderSettings.asset
        Platform  // 平台組 - Editor/AWSS3UploaderSettings_Platform.asset
    }

    #endregion

    #region Private Fields

    // 設定檔管理
    private AWSS3UploaderSettings _settings;
    private AWSS3UploaderSettings.S3Profile _currentProfile;
    private SettingsType _currentSettingsType = SettingsType.Game;

    // 上傳狀態
    private bool _isUploading = false;
    private float _uploadProgress = 0f;
    private string _statusMessage = "準備就緒";
    private int _totalFileCount = 0;
    private List<string> _activeUploadingFileNames = new List<string>();
    private int _completedFileCount = 0;

    // AssetBundle 上傳進度追蹤
    private bool _isUploadingAssetBundle = false;
    private int _assetBundleFileCount = 0;
    private int _assetBundleCompletedFileCount = 0;
    private float _assetBundleProgress = 0f;

    // UI 狀態
    private Vector2 _logScrollPosition;
    private List<string> _uploadLog = new List<string>();
    private bool _showProfileSection = false;
    private string _newProfileName = "新設定檔";
    private bool _autoScrollLog = true;

    // AWS 客戶端
    private AmazonS3Client _s3Client;
    private SemaphoreSlim _uploadSemaphore;
    private readonly object _progressLock = new object();
    private CancellationTokenSource _cancellationTokenSource;

    #endregion

    #region Public Properties

    /// <summary>
    /// 目前是否正在上傳
    /// </summary>
    public bool IsUploading => _isUploading;

    /// <summary>
    /// 目前上傳進度 (0-1)
    /// </summary>
    public float UploadProgress => _uploadProgress;

    /// <summary>
    /// 目前狀態訊息
    /// </summary>
    public string StatusMessage => _statusMessage;



    /// <summary>
    /// 總檔案數量
    /// </summary>
    public int TotalFileCount => _totalFileCount;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 視窗啟用時的初始化
    /// </summary>
    private void OnEnable()
    {
        LoadSettings();
    }

    /// <summary>
    /// 視窗停用時的清理
    /// </summary>
    private void OnDisable()
    {
        // 如果還有正在進行的上傳，先取消它們
        if (_isUploading && _cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _isUploading = false;
        }

        SaveSettings();
        CleanupS3Client();
    }

    /// <summary>
    /// 繪製視窗 GUI
    /// </summary>
    private void OnGUI()
    {
        CalculateAndSetWindowSize();
        DrawWindowContent();
    }

    #endregion

    #region Menu Items

    /// <summary>
    /// 顯示 AWS S3 上傳器視窗的選單項目
    /// </summary>
    // [MenuItem("Tools/AWS S3 上傳器")]
    public static void ShowWindow()
    {
        var window = GetWindow<AWSS3UploaderWindow>("AWS S3 上傳器");
        window.minSize = new Vector2(UI_MIN_WINDOW_WIDTH, UI_MIN_WINDOW_HEIGHT);
    }



    #endregion

    #region Settings Management

    /// <summary>
    /// 載入設定檔
    /// </summary>
    private void LoadSettings()
    {
        LoadUISettings(); // 先載入 UI 設定以獲取正確的設定類型
        LoadSettingsByType(_currentSettingsType);
    }

    /// <summary>
    /// 根據類型載入設定檔
    /// </summary>
    /// <param name="type">設定類型</param>
    private void LoadSettingsByType(SettingsType type)
    {
        _settings = AWSS3UploaderSettings.LoadEditorSettings(type == SettingsType.Platform);

        if (_settings == null)
        {
            if (type == SettingsType.Game)
            {
                CreateDefaultSettings();
            }
            else
            {
                CreatePlatformSettings();
            }
        }

        _currentProfile = _settings.GetSelectedProfile();
        _currentProfile.LocalDirectoryPath = "";

        // 檢查 Addressable Profile 設定
        if (!_currentProfile.ValidateAddressableProfile(out string errorMessage))
        {
            // EditorUtility.DisplayDialog(
            //     "Addressable Profile 設定問題",
            //     $"{errorMessage}\n\n請到 Window → Asset Management → Addressables → Profiles 進行設定",
            //     "確定"
            // );
            Debug.LogWarning(errorMessage);
        }
    }

    /// <summary>
    /// 儲存設定檔
    /// </summary>
    private void SaveSettings()
    {
        if (_settings != null && _currentProfile != null)
        {
            _settings.Profiles[_settings.SelectedProfileIndex] = _currentProfile;
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
        }

        SaveUISettings();
    }

    /// <summary>
    /// 創建預設設定檔
    /// </summary>
    private void CreateDefaultSettings()
    {
        _settings = CreateInstance<AWSS3UploaderSettings>();

        string editorDir = AWSS3UploaderSettings.EditorSettingsDirectory;
        if (!Directory.Exists(editorDir))
        {
            Directory.CreateDirectory(editorDir);
        }

        AssetDatabase.CreateAsset(_settings, AWSS3UploaderSettings.EditorSettingsAssetPath);
        AssetDatabase.SaveAssets();

        AddLog("已創建預設遊戲組設定檔（Editor 目錄，不進玩家包）");
    }

    /// <summary>
    /// 創建平台組設定檔
    /// </summary>
    private void CreatePlatformSettings()
    {
        _settings = CreateInstance<AWSS3UploaderSettings>();

        // 複製遊戲組的 Profile 結構作為範本
        var gameSettings = AWSS3UploaderSettings.LoadEditorSettings(false);
        if (gameSettings != null && gameSettings.Profiles.Count > 0)
        {
            foreach (var gameProfile in gameSettings.Profiles)
            {
                var platformProfile = gameProfile.Clone();
                // 清空 S3 相關設定，讓使用者重新配置
                platformProfile.S3BucketName = "";
                platformProfile.SetS3KeyPrefix("");
                platformProfile.SetAssetBundleDirectoryPath("");
                platformProfile.AssetBundleS3Path = "";
                // 停用 Addressable 同步，因為平台組通常需要手動設定
                platformProfile.SyncWithAddressableProfile = false;

                _settings.AddProfile(platformProfile);
            }
        }

        string editorDir = AWSS3UploaderSettings.EditorSettingsDirectory;
        if (!Directory.Exists(editorDir))
        {
            Directory.CreateDirectory(editorDir);
        }

        AssetDatabase.CreateAsset(_settings, AWSS3UploaderSettings.EditorPlatformSettingsAssetPath);
        AssetDatabase.SaveAssets();

        AddLog("已創建平台組設定檔（Editor 目錄），請重新配置 S3 路徑設定");
    }

    /// <summary>
    /// 切換設定類型
    /// </summary>
    /// <param name="newType">新的設定類型</param>
    private void SwitchSettingsType(SettingsType newType)
    {
        if (_currentSettingsType == newType)
            return;

        // 儲存當前設定
        SaveSettings();

        // 切換到新的設定類型
        _currentSettingsType = newType;
        LoadSettingsByType(_currentSettingsType);

        // 儲存設定類型選擇
        EditorPrefs.SetInt("AWSS3Uploader.SettingsType", (int)_currentSettingsType);

        // 記錄切換
        string typeName = _currentSettingsType == SettingsType.Game ? "遊戲組" : "平台組";
        AddLog($"已切換至{typeName}設定");
    }

    /// <summary>
    /// 載入 UI 設定
    /// </summary>
    private void LoadUISettings()
    {
        _autoScrollLog = EditorPrefs.GetBool("AWSS3Uploader.AutoScrollLog", true);
        _showProfileSection = EditorPrefs.GetBool("AWSS3Uploader.ShowProfileSection", false);
        _newProfileName = EditorPrefs.GetString("AWSS3Uploader.NewProfileName", "新設定檔");

        // 載入設定類型選擇
        _currentSettingsType = (SettingsType)EditorPrefs.GetInt("AWSS3Uploader.SettingsType", (int)SettingsType.Game);

        // 注意：不載入視窗位置設定以保持停靠功能
    }

    /// <summary>
    /// 儲存 UI 設定
    /// </summary>
    private void SaveUISettings()
    {
        EditorPrefs.SetBool("AWSS3Uploader.AutoScrollLog", _autoScrollLog);
        EditorPrefs.SetBool("AWSS3Uploader.ShowProfileSection", _showProfileSection);
        EditorPrefs.SetString("AWSS3Uploader.NewProfileName", _newProfileName);

        // 注意：不儲存視窗位置以保持停靠功能
    }

    #endregion

    #region Window Layout

    /// <summary>
    /// 計算並設定視窗大小
    /// </summary>
    private void CalculateAndSetWindowSize()
    {
        // 根據當前狀態計算各區域高度
        float profileSectionHeight = _showProfileSection ? UI_PROFILE_SECTION_HEIGHT_EXPANDED : UI_PROFILE_SECTION_HEIGHT_COLLAPSED;
        float progressHeight = _isUploading ? UI_PROGRESS_HEIGHT : 0f;

        // 計算所有固定UI元素的總高度
        float fixedTotalHeight = profileSectionHeight + UI_AWS_SETTINGS_HEIGHT +
                                UI_UPLOAD_SETTINGS_HEIGHT + UI_ASSETBUNDLE_UPLOAD_HEIGHT + UI_ACTION_BUTTONS_HEIGHT + progressHeight +
                                UI_LOG_TITLE_HEIGHT + UI_LOG_CONTROLS_HEIGHT + UI_MARGINS_TOTAL;

        // 在多檔上傳時，為日誌區域提供額外的空間以顯示更多上傳資訊
        float logSpace = _isUploading ? UI_MIN_LOG_HEIGHT + UI_UPLOADING_EXTRA_LOG_SPACE : UI_MIN_LOG_HEIGHT;
        float minWindowHeight = fixedTotalHeight + logSpace + UI_BOTTOM_MARGIN;

        // 注意：不直接調整視窗大小以保持停靠功能

        // 更新視窗最小大小限制，在上傳時提供更大的最小高度以改善使用體驗
        float currentMinHeight = _isUploading ? UI_MIN_WINDOW_HEIGHT + UI_UPLOADING_EXTRA_WINDOW_HEIGHT : UI_MIN_WINDOW_HEIGHT;
        minSize = new Vector2(UI_MIN_WINDOW_WIDTH, currentMinHeight);
    }

    /// <summary>
    /// 繪製視窗內容
    /// </summary>
    private void DrawWindowContent()
    {
        try
        {
            // 計算各區域高度
            float profileSectionHeight = _showProfileSection ? UI_PROFILE_SECTION_HEIGHT_EXPANDED : UI_PROFILE_SECTION_HEIGHT_COLLAPSED;
            float progressHeight = _isUploading ? UI_PROGRESS_HEIGHT : 0f;

            // 繪製各個區域
            DrawProfileSectionWithHeight(profileSectionHeight);
            GUILayout.Space(5);  // 設定檔區域與 AWS 設定區域之間的間距

            DrawAWSSettingsWithHeight(UI_AWS_SETTINGS_HEIGHT);
            GUILayout.Space(5);  // AWS 設定區域與上傳設定區域之間的間距

            DrawUploadSettingsWithHeight(UI_UPLOAD_SETTINGS_HEIGHT);
            GUILayout.Space(5);  // 上傳設定區域與 AssetBundle 上傳區域之間的間距

            DrawAssetBundleSettingsWithHeight(UI_ASSETBUNDLE_UPLOAD_HEIGHT);
            GUILayout.Space(5);  // AssetBundle 上傳區域與操作按鈕區域之間的間距

            DrawActionButtons();

            if (_isUploading)
            {
                GUILayout.Space(5);  // 操作按鈕區域與進度區域之間的間距
                DrawProgressSection();
            }

            GUILayout.Space(5);  // 操作按鈕或進度區域與日誌區域之間的間距

            // 計算日誌區域高度 - 確保有足夠的空間顯示日誌
            float fixedTotalHeight = profileSectionHeight + UI_AWS_SETTINGS_HEIGHT +
                                    UI_UPLOAD_SETTINGS_HEIGHT + UI_ASSETBUNDLE_UPLOAD_HEIGHT + UI_ACTION_BUTTONS_HEIGHT + progressHeight +
                                    UI_LOG_TITLE_HEIGHT + UI_LOG_CONTROLS_HEIGHT + UI_MARGINS_TOTAL;

            float logContentHeight = position.height - fixedTotalHeight - UI_BOTTOM_MARGIN;

            // 確保日誌區域至少有最小高度，在進度顯示時額外增加空間
            float minLogSpace = _isUploading ? UI_MIN_LOG_HEIGHT + UI_UPLOADING_EXTRA_LOG_SPACE : UI_MIN_LOG_HEIGHT;
            if (logContentHeight < minLogSpace)
            {
                logContentHeight = minLogSpace;

                // 注意：不動態調整視窗高度以保持停靠功能
            }

            DrawLogSectionWithHeight(logContentHeight);
        }
        catch (System.Exception ex)
        {
            // 記錄異常並顯示錯誤信息
            Debug.LogError($"GUI 繪製異常: {ex.Message}");
            GUILayout.Label($"GUI 錯誤: {ex.Message}", EditorStyles.helpBox);
        }
    }

    #endregion

    #region UI Drawing Methods

    /// <summary>
    /// 繪製操作按鈕區域
    /// </summary>
    private void DrawActionButtons()
    {
        // 計算按鈕寬度 - 精確控制間距以確保右邊按鈕貼齊邊界
        float sideMargin = 10f; // 左右邊距
        float buttonSpacing = 5f; // 按鈕間距
        float buttonWidth = (position.width - (sideMargin * 2) - buttonSpacing) / 2f; // 精確計算按鈕寬度

        // 第一行按鈕：測試AWS連線 獲取S3檔案列表
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(sideMargin); // 左邊距

        GUI.enabled = !_isUploading && IsS3SettingsValid();
        if (GUILayout.Button("測試 AWS 連線", GUILayout.Width(buttonWidth), GUILayout.Height(25)))
        {
            TestAWSConnection();
        }

        GUILayout.Space(buttonSpacing); // 按鈕間距

        if (GUILayout.Button("獲取 S3 檔案列表", GUILayout.Width(buttonWidth), GUILayout.Height(25)))
        {
            GetS3FileList();
        }

        GUILayout.Space(sideMargin); // 右邊距
        EditorGUILayout.EndHorizontal();

        // 第二行按鈕：AssetBundle 手動上傳
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(sideMargin);

        GUI.enabled = !_isUploading && IsAssetBundleSettingsValid();
        if (GUILayout.Button("AssetBundle手動上傳", GUILayout.Height(25)))
        {
            StartAssetBundleUpload();
        }

        GUILayout.Space(sideMargin);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        // 第三行按鈕：開始上傳 取消上傳
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(sideMargin); // 左邊距

        GUI.enabled = !_isUploading && IsSettingsValid();
        if (GUILayout.Button("開始上傳", GUILayout.Width(buttonWidth), GUILayout.Height(25)))
        {
            StartUpload();
        }

        GUILayout.Space(buttonSpacing); // 按鈕間距

        GUI.enabled = _isUploading;
        if (GUILayout.Button("取消上傳", GUILayout.Width(buttonWidth), GUILayout.Height(25)))
        {
            CancelUpload();
        }

        GUILayout.Space(sideMargin); // 右邊距
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製上傳進度區域
    /// </summary>
    private void DrawProgressSection()
    {
        if (_isUploading)
        {
            GUILayout.Label("上傳進度", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // 判斷上傳情況
            bool hasGameFiles = _totalFileCount > 0;
            bool hasBundleFiles = _assetBundleFileCount > 0;
            bool isMixedUpload = hasGameFiles && hasBundleFiles;

            if (isMixedUpload)
            {
                // 同時上傳主遊戲和AssetBundle：顯示分別進度和整體進度
                // 主遊戲上傳進度
                float gameProgress = _totalFileCount > 0 ? (float)_completedFileCount / _totalFileCount : 0f;
                string gameProgressText = $"主遊戲上傳: {(gameProgress * 100):F1}%";
                if (_totalFileCount > 0)
                {
                    gameProgressText += $" ({_completedFileCount}/{_totalFileCount} 個檔案)";
                }
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), gameProgress, gameProgressText);

                // AssetBundle 上傳進度
                GUILayout.Space(2);
                string bundleProgressText = $"AssetBundle 上傳: {(_assetBundleProgress * 100):F1}%";
                if (_assetBundleFileCount > 0)
                {
                    bundleProgressText += $" ({_assetBundleCompletedFileCount}/{_assetBundleFileCount} 個檔案)";
                }
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), _assetBundleProgress, bundleProgressText);

                // 整體進度
                GUILayout.Space(2);
                float overallProgress = CalculateOverallProgress();
                string overallProgressText = $"整體進度: {(overallProgress * 100):F1}%";
                int totalAllFiles = _totalFileCount + _assetBundleFileCount;
                int completedAllFiles = _completedFileCount + _assetBundleCompletedFileCount;
                overallProgressText += $" ({completedAllFiles}/{totalAllFiles} 個檔案)";
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), overallProgress, overallProgressText);
            }
            else if (hasGameFiles)
            {
                // 只上傳主遊戲：只顯示主遊戲進度
                float gameProgress = _totalFileCount > 0 ? (float)_completedFileCount / _totalFileCount : 0f;
                string gameProgressText = $"主遊戲上傳: {(gameProgress * 100):F1}%";
                if (_totalFileCount > 0)
                {
                    gameProgressText += $" ({_completedFileCount}/{_totalFileCount} 個檔案)";
                }
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), gameProgress, gameProgressText);
            }
            else if (hasBundleFiles)
            {
                // 只上傳AssetBundle：只顯示AssetBundle進度
                string bundleProgressText = $"AssetBundle 上傳: {(_assetBundleProgress * 100):F1}%";
                if (_assetBundleFileCount > 0)
                {
                    bundleProgressText += $" ({_assetBundleCompletedFileCount}/{_assetBundleFileCount} 個檔案)";
                }
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), _assetBundleProgress, bundleProgressText);
            }

            // 並行上傳狀態 - 更緊湊的顯示
            lock (_progressLock)
            {
                if (_activeUploadingFileNames.Count > 0)
                {
                    GUILayout.Space(2);

                    // 使用水平佈局顯示並行狀態，節省垂直空間
                    EditorGUILayout.BeginHorizontal();
                    string uploadLabel = _isUploadingAssetBundle ? "正在上傳 AssetBundle" : "正在上傳主遊戲";
                    GUILayout.Label($"{uploadLabel} {_activeUploadingFileNames.Count} 個檔案:", EditorStyles.miniLabel, GUILayout.Width(140));

                    // 將檔案名稱用逗號分隔在同一行顯示，使用預定義的顯示限制常數
                    var displayFiles = _activeUploadingFileNames.Take(MAX_PARALLEL_FILE_DISPLAY).ToArray();
                    string fileList = string.Join(", ", displayFiles);
                    if (_activeUploadingFileNames.Count > MAX_PARALLEL_FILE_DISPLAY)
                    {
                        fileList += $" (+{_activeUploadingFileNames.Count - MAX_PARALLEL_FILE_DISPLAY}個)";
                    }
                    GUILayout.Label(fileList, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }

            // 狀態訊息
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Space(1);
                GUILayout.Label(_statusMessage, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// 計算整體上傳進度
    /// </summary>
    /// <returns>整體進度（0-1）</returns>
    private float CalculateOverallProgress()
    {
        bool hasAssetBundle = _currentProfile.UploadAssetBundle && IsAssetBundleSettingsValid();

        if (!hasAssetBundle)
        {
            // 只有主遊戲上傳
            return _totalFileCount > 0 ? (float)_completedFileCount / _totalFileCount : 0f;
        }

        // 主遊戲和AssetBundle都需要上傳
        int totalAllFiles = _totalFileCount + _assetBundleFileCount;
        int completedAllFiles = _completedFileCount + _assetBundleCompletedFileCount;

        return totalAllFiles > 0 ? (float)completedAllFiles / totalAllFiles : 0f;
    }

    /// <summary>
    /// 繪製設定檔管理區域
    /// </summary>
    /// <param name="height">區域高度</param>
    private void DrawProfileSectionWithHeight(float height)
    {
        GUILayout.Label("設定檔管理", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box", GUILayout.Height(height));

        // 設定類型選擇
        DrawSettingsTypeSelection();

        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();

        // 設定檔選擇
        DrawProfileSelection();

        // 顯示/隱藏設定檔管理
        if (GUILayout.Button(_showProfileSection ? "隱藏管理" : "管理設定檔", GUILayout.Width(80)))
        {
            _showProfileSection = !_showProfileSection;
        }

        EditorGUILayout.EndHorizontal();

        // Addressable Profile 同步設定（永遠顯示）
        GUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("使用Profile路徑:", GUILayout.Width(UI_LABEL_WIDTH));
        bool newSyncState = EditorGUILayout.Toggle(_currentProfile.SyncWithAddressableProfile);

        if (newSyncState != _currentProfile.SyncWithAddressableProfile)
        {
            _currentProfile.SyncWithAddressableProfile = newSyncState;

            if (newSyncState)
            {
                // 檢查並同步設定
                if (!_currentProfile.ValidateAddressableProfile(out string errorMessage))
                {
                    // EditorUtility.DisplayDialog(
                    //     "Addressable Profile 設定問題",
                    //     $"{errorMessage}\n\n請到 Window → Asset Management → Addressables → Profiles 進行設定",
                    //     "確定"
                    // );
                    Debug.LogWarning(errorMessage);
                    _currentProfile.SyncWithAddressableProfile = false; // 回復原狀態
                }
                else
                {
                    AddLog($"已啟用 Addressable Profile 同步：{_currentProfile.ProfileName}");
                }
            }
            else
            {
                AddLog($"已停用 Addressable Profile 同步：{_currentProfile.ProfileName}");
            }
        }

        EditorGUILayout.EndHorizontal();

        // 設定檔管理介面
        if (_showProfileSection)
        {
            GUILayout.Space(2);  // 設定檔選項與管理介面之間的間距
            DrawProfileManagement();
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 繪製設定類型選擇
    /// </summary>
    private void DrawSettingsTypeSelection()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("設定類型:", GUILayout.Width(UI_LABEL_WIDTH));

        string[] typeNames = { "遊戲組", "平台組" };
        int currentTypeIndex = (int)_currentSettingsType;
        int newTypeIndex = EditorGUILayout.Popup(currentTypeIndex, typeNames);

        if (newTypeIndex != currentTypeIndex)
        {
            SwitchSettingsType((SettingsType)newTypeIndex);
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製設定檔選擇下拉選單
    /// </summary>
    private void DrawProfileSelection()
    {
        string[] profileNames = new string[_settings.Profiles.Count];
        for (int i = 0; i < _settings.Profiles.Count; i++)
        {
            profileNames[i] = _settings.Profiles[i].ProfileName;
        }

        GUILayout.Label("當前設定檔:", GUILayout.Width(UI_LABEL_WIDTH));
        int newSelectedIndex = EditorGUILayout.Popup(_settings.SelectedProfileIndex, profileNames);
        if (newSelectedIndex != _settings.SelectedProfileIndex)
        {
            SaveSettings(); // 先保存當前設定
            _settings.SelectedProfileIndex = newSelectedIndex;
            _currentProfile = _settings.GetSelectedProfile();
            _currentProfile.LocalDirectoryPath = "";

            // 檢查 Addressable Profile 設定
            if (!_currentProfile.ValidateAddressableProfile(out string errorMessage))
            {
                // EditorUtility.DisplayDialog(
                //     "Addressable Profile 設定問題",
                //     $"{errorMessage}\n\n請到 Window → Asset Management → Addressables → Profiles 進行設定",
                //     "確定"
                // );
                Debug.LogWarning(errorMessage);
            }
        }
    }

    /// <summary>
    /// 繪製設定檔管理介面（新增、刪除、重命名）
    /// </summary>
    private void DrawProfileManagement()
    {
        // 新增設定檔
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("新設定檔名稱:", GUILayout.Width(UI_LABEL_WIDTH));
        _newProfileName = EditorGUILayout.TextField(_newProfileName);
        if (GUILayout.Button("新增", GUILayout.Width(60)))
        {
            CreateNewProfile();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(1);  // 新增區域與刪除區域之間的間距

        // 刪除當前設定檔
        if (_settings.Profiles.Count > 1)
        {
            if (GUILayout.Button("刪除當前設定檔"))
            {
                DeleteCurrentProfile();
            }
        }

        GUILayout.Space(1);  // 刪除區域與重命名區域之間的間距

        // 重命名當前設定檔
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("重命名:", GUILayout.Width(UI_LABEL_WIDTH));
        _currentProfile.ProfileName = EditorGUILayout.TextField(_currentProfile.ProfileName);
        EditorGUILayout.EndHorizontal();
    }



    /// <summary>
    /// 繪製 AWS 設定區域
    /// </summary>
    /// <param name="height">區域高度</param>
    private void DrawAWSSettingsWithHeight(float height)
    {
        GUILayout.Label("AWS 設定", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box", GUILayout.Height(height));

        DrawAWSRegionSelection();
        DrawAWSCredentialsFields();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// AWS 支援的區域清單
    /// </summary>
    private static readonly string[] AWS_REGIONS = {
        "us-east-1",      // 美國東部 (維吉尼亞北部)
        "us-west-1",      // 美國西部 (加利福尼亞北部)
        "us-west-2",      // 美國西部 (俄勒岡)
        "ap-northeast-1", // 亞太區域 (東京)
        "ap-northeast-2", // 亞太區域 (首爾)
        "ap-southeast-1", // 亞太區域 (新加坡)
        "ap-southeast-2", // 亞太區域 (雪梨)
        "ap-south-1",     // 亞太區域 (孟買)
        "eu-west-1",      // 歐洲 (愛爾蘭)
        "eu-central-1",   // 歐洲 (法蘭克福)
        "ca-central-1",   // 加拿大 (中部)
        "sa-east-1"       // 南美洲 (聖保羅)
    };

    /// <summary>
    /// 繪製 AWS 區域選擇
    /// </summary>
    private void DrawAWSRegionSelection()
    {
        int currentRegionIndex = System.Array.IndexOf(AWS_REGIONS, _currentProfile.AwsRegion);
        if (currentRegionIndex == -1) currentRegionIndex = 0; // 預設為 us-east-1

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("AWS 區域:", GUILayout.Width(UI_LABEL_WIDTH));
        int newRegionIndex = EditorGUILayout.Popup(currentRegionIndex, AWS_REGIONS);
        _currentProfile.AwsRegion = AWS_REGIONS[newRegionIndex];
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製 AWS 憑證欄位
    /// </summary>
    private void DrawAWSCredentialsFields()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Access Key ID:", GUILayout.Width(UI_LABEL_WIDTH));
        _currentProfile.AccessKeyId = EditorGUILayout.TextField(_currentProfile.AccessKeyId);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Secret Access Key:", GUILayout.Width(UI_LABEL_WIDTH));
        _currentProfile.SecretAccessKey = EditorGUILayout.PasswordField(_currentProfile.SecretAccessKey);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("CloudFront Distribution ID:", GUILayout.Width(UI_LABEL_WIDTH));
        _currentProfile.CloudFrontDistributionId = EditorGUILayout.TextField(_currentProfile.CloudFrontDistributionId);
        EditorGUILayout.EndHorizontal();
    }



    /// <summary>
    /// 繪製上傳設定區域
    /// </summary>
    /// <param name="height">區域高度</param>
    private void DrawUploadSettingsWithHeight(float height)
    {
        GUILayout.Label("主遊戲上傳設定", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box", GUILayout.Height(height));

        DrawLocalDirectorySelection();
        DrawS3BucketSettings();
        DrawConcurrentUploadSettings();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 繪製主遊戲目錄選擇
    /// </summary>
    private void DrawLocalDirectorySelection()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("主遊戲目錄:", GUILayout.Width(UI_LABEL_WIDTH));
        _currentProfile.LocalDirectoryPath = EditorGUILayout.TextField(_currentProfile.LocalDirectoryPath);
        if (GUILayout.Button("瀏覽", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("選擇上傳目錄", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                _currentProfile.LocalDirectoryPath = path;
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製 S3 儲存桶設定
    /// </summary>
    private void DrawS3BucketSettings()
    {
        // 整合的遊戲 S3 路徑設定
        EditorGUILayout.BeginHorizontal();

        // 標籤文字
        GUILayout.Label("遊戲 S3路徑:", GUILayout.Width(UI_LABEL_WIDTH));

        bool isEditable = !_currentProfile.SyncWithAddressableProfile;

        // S3 儲存桶名稱
        GUI.enabled = isEditable;
        if (isEditable)
        {
            _currentProfile.S3BucketName = EditorGUILayout.TextField(_currentProfile.S3BucketName, GUILayout.Width(190));
        }
        else
        {
            EditorGUILayout.TextField(_currentProfile.GetS3BucketName(), GUILayout.Width(190));
        }

        // 分隔符
        GUILayout.Label("/", EditorStyles.boldLabel, GUILayout.Width(10));

        // 專案名（與 SSHUploader.GetProjectName / 作用中 Addressables Profile 的 ProjectName 一致）
        GUI.enabled = false;
        if (_currentProfile.UploadToRootDirectory)
            EditorGUILayout.TextField("(僅根目錄，無專案段)", GUILayout.Width(160));
        else
            EditorGUILayout.TextField(_currentProfile.GetUploadProjectName(), GUILayout.Width(160));
        GUI.enabled = true;

        // 顯示完整路徑在同一行右側
        string displayBucketName = isEditable ? _currentProfile.S3BucketName : _currentProfile.GetS3BucketName();

        if (!string.IsNullOrEmpty(displayBucketName))
        {
            string fullPath = $"s3://{displayBucketName}/{_currentProfile.GetWebGlUploadKeyPrefix()}";
            GUILayout.Space(20);
            GUILayout.Label(fullPath);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("WebGL 根目錄:", GUILayout.Width(UI_LABEL_WIDTH));
        if (isEditable)
            _currentProfile.WebGlBuildS3Root = EditorGUILayout.TextField(_currentProfile.WebGlBuildS3Root ?? "games");
        else
            EditorGUILayout.TextField(_currentProfile.WebGlBuildS3Root ?? "games");
        GUILayout.Space(8);
        GUILayout.Label("(桶內主程式目錄，預設 games)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // 驗證 S3 儲存桶名稱格式
        string currentBucketName = isEditable ? _currentProfile.S3BucketName : _currentProfile.GetS3BucketName();
        if (!string.IsNullOrEmpty(currentBucketName) &&
            !ValidateS3BucketName(currentBucketName))
        {
            EditorGUILayout.HelpBox(
                "S3 儲存桶名稱格式不正確。規則：\n" +
                "• 長度 3-63 個字符\n" +
                "• 只能包含小寫字母、數字、連字符和點號\n" +
                "• 必須以字母或數字開頭和結尾\n" +
                "• 不能包含連續的連字符或點號\n" +
                "• 點號和連字符不能相鄰\n" +
                "• 不能看起來像 IP 地址",
                MessageType.Warning);
        }

        // 上傳至根目錄選項
        GUILayout.Space(3);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("僅 WebGL根目錄:", GUILayout.Width(UI_LABEL_WIDTH));
        bool newUploadToRoot = EditorGUILayout.Toggle(_currentProfile.UploadToRootDirectory);

        if (newUploadToRoot != _currentProfile.UploadToRootDirectory)
        {
            _currentProfile.UploadToRootDirectory = newUploadToRoot;

            if (newUploadToRoot)
            {
                AddLog($"⚠️ 已啟用「僅 WebGL 根目錄」：{_currentProfile.ProfileName}");
                AddLog($"注意：不上傳專案子路徑，檔案在 s3://bucket/{(_currentProfile.WebGlBuildS3Root ?? "games").Trim().Trim('/')}/ 下");
            }
            else
            {
                AddLog($"✅ 已停用「僅 WebGL 根目錄」：{_currentProfile.ProfileName}");
            }
        }

        GUILayout.Space(10);
        if (_currentProfile.UploadToRootDirectory)
        {
            GUILayout.Label("僅 WebGL 區段根（不加專案名，檔案在 games/ 下）", EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label("專案名取自 SSH 上傳相同邏輯，路徑為 games/{專案名}/", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製並行上傳設定
    /// </summary>
    private void DrawConcurrentUploadSettings()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("同時上傳檔案數:", GUILayout.Width(UI_LABEL_WIDTH));
        _currentProfile.MaxConcurrentUploads = EditorGUILayout.IntField(_currentProfile.MaxConcurrentUploads, GUILayout.Width(50));
        // 確保數值在合理範圍內
        _currentProfile.MaxConcurrentUploads = Mathf.Clamp(_currentProfile.MaxConcurrentUploads, MIN_CONCURRENT_UPLOADS, MAX_CONCURRENT_UPLOADS);
        GUILayout.Label($"({_currentProfile.MaxConcurrentUploads} 個)", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製 AssetBundle 設定
    /// </summary>
    private void DrawAssetBundleSettingsWithHeight(float height)
    {
        GUILayout.Label("AssetBundle上傳設定", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box", GUILayout.Height(height));

        // AssetBundle 主選項
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("自動上傳:", GUILayout.Width(UI_LABEL_WIDTH));
        _currentProfile.UploadAssetBundle = EditorGUILayout.Toggle(_currentProfile.UploadAssetBundle);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(3);

        // 移除enable/disable控制，所有控件保持啟用狀態
        DrawAssetBundleDirectorySelection();
        DrawAssetBundleS3PathSettings();

        GUILayout.Space(3);

        // 進階設定選項
        DrawAdvancedUploadSettings();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 繪製 AssetBundle 目錄選擇
    /// </summary>
    private void DrawAssetBundleDirectorySelection()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Bundle 目錄:", GUILayout.Width(UI_LABEL_WIDTH));

        bool isEditable = !_currentProfile.SyncWithAddressableProfile;
        GUI.enabled = isEditable;

        if (isEditable)
        {
            string newAssetBundleDirectoryPath = EditorGUILayout.TextField(_currentProfile.GetAssetBundleDirectoryPath());
            _currentProfile.SetAssetBundleDirectoryPath(newAssetBundleDirectoryPath);
        }
        else
        {
            EditorGUILayout.TextField(_currentProfile.GetAssetBundleDirectoryPath());
        }

        if (isEditable && GUILayout.Button("瀏覽", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("選擇 AssetBundle 目錄", _currentProfile.GetAssetBundleDirectoryPath(), "");
            if (!string.IsNullOrEmpty(path))
            {
                _currentProfile.SetAssetBundleDirectoryPath(path);
            }
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製 AssetBundle S3 路徑設定
    /// </summary>
    private void DrawAssetBundleS3PathSettings()
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("Bundle S3路徑:", GUILayout.Width(UI_LABEL_WIDTH));

        GUI.enabled = false;
        string displayBucketName = _currentProfile.SyncWithAddressableProfile ? _currentProfile.GetS3BucketName() : _currentProfile.S3BucketName;
        EditorGUILayout.TextField(displayBucketName, GUILayout.Width(190));

        GUILayout.Label("/", EditorStyles.boldLabel, GUILayout.Width(10));

        bool isEditable = !_currentProfile.SyncWithAddressableProfile;
        GUI.enabled = isEditable;

        if (isEditable)
            _currentProfile.AssetBundleS3Root = EditorGUILayout.TextField(_currentProfile.AssetBundleS3Root ?? "assets", GUILayout.Width(72));
        else
        {
            string displayBundleRoot = string.IsNullOrWhiteSpace(_currentProfile.AssetBundleS3Root)
                ? "assets"
                : _currentProfile.AssetBundleS3Root.Trim().Trim('/');
            EditorGUILayout.TextField(displayBundleRoot, GUILayout.Width(72));
        }

        GUILayout.Label("/", EditorStyles.boldLabel, GUILayout.Width(8));

        GUI.enabled = false;
        EditorGUILayout.TextField(_currentProfile.GetUploadProjectName(), GUILayout.Width(120));
        GUI.enabled = true;

        GUILayout.Label("/", EditorStyles.boldLabel, GUILayout.Width(8));

        GUI.enabled = isEditable;
        if (isEditable)
            _currentProfile.AssetBundleS3Path = EditorGUILayout.TextField(_currentProfile.AssetBundleS3Path);
        else
            EditorGUILayout.TextField(_currentProfile.GetAssetBundleS3Path());
        GUI.enabled = true;

        string fullPath = $"s3://{displayBucketName ?? ""}/{_currentProfile.GetAssetBundleS3FullPrefix()}";
        GUILayout.Space(12);
        GUILayout.Label(fullPath);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(UI_LABEL_WIDTH));
        GUILayout.Label("桶 / assets 根 / 專案名(同SSH) / Bundle 子目錄", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製進階上傳設定
    /// </summary>
    private void DrawAdvancedUploadSettings()
    {
        // 不清Bundle資料夾設定
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("不清Bundle資料夾:", GUILayout.Width(UI_LABEL_WIDTH));
        _currentProfile.ExcludeBundleSourceFromClear = EditorGUILayout.Toggle(_currentProfile.ExcludeBundleSourceFromClear);
        GUILayout.Space(10);
        string bundleSub = _currentProfile.GetAssetBundleS3Path();
        string assetBundlePath = string.IsNullOrEmpty(bundleSub)
            ? "assets/專案名"
            : bundleSub.TrimStart('/');
        GUILayout.Label($"清除主包時保留「{assetBundlePath}」對應 S3 前綴");
        EditorGUILayout.EndHorizontal();

        // Bundle重複檢查設定
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("跳過重複Bundle:", GUILayout.Width(UI_LABEL_WIDTH));
        _currentProfile.SkipDuplicateBundleUploads = EditorGUILayout.Toggle(_currentProfile.SkipDuplicateBundleUploads);
        GUILayout.Space(10);
        GUILayout.Label("避免重複上傳省流量");
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製操作日誌區域
    /// </summary>
    /// <param name="height">區域高度</param>
    private void DrawLogSectionWithHeight(float height)
    {
        // 操作日誌標題行，包含所有控制按鈕
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("操作日誌", EditorStyles.boldLabel);

        // 清空日誌按鈕
        if (GUILayout.Button("清空日誌", GUILayout.Width(60), GUILayout.Height(18)))
        {
            _uploadLog.Clear();
        }

        GUILayout.FlexibleSpace();

        // 自動滾動checkbox
        _autoScrollLog = GUILayout.Toggle(_autoScrollLog, "自動滾動", GUILayout.Width(80));

        // 滾動到底部按鈕
        if (GUILayout.Button("滾動到底部", GUILayout.Width(80), GUILayout.Height(18)))
        {
            _logScrollPosition = new Vector2(0, float.MaxValue);
        }
        EditorGUILayout.EndHorizontal();

        // 確保日誌區域有足夠的高度
        float logSectionHeight = Mathf.Max(height, UI_MIN_LOG_HEIGHT + UI_LOG_CONTROLS_HEIGHT);

        // 使用不帶背景的垂直佈局，避免多層嵌套背景
        EditorGUILayout.BeginVertical(GUILayout.Height(logSectionHeight));

        DrawLogControls();
        DrawLogContent(logSectionHeight);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 繪製日誌控制分隔線
    /// </summary>
    private void DrawLogControls()
    {
        // 添加一個淺色分隔線
        EditorGUILayout.Space(2);

        // 使用 Rect 來繪製分隔線，避免佈局問題
        Rect separatorRect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(separatorRect, EditorGUIUtility.isProSkin
            ? new Color(0.6f, 0.6f, 0.6f, 0.3f)
            : new Color(0.3f, 0.3f, 0.3f, 0.3f));

        EditorGUILayout.Space(3);
    }

    /// <summary>
    /// 繪製日誌內容
    /// </summary>
    /// <param name="totalHeight">總高度</param>
    private void DrawLogContent(float totalHeight)
    {
        // 日誌內容區域 - 減去按鈕區域的高度，並確保有足夠的邊距
        float contentHeight = totalHeight - UI_LOG_CONTROLS_HEIGHT - 20; // 增加更多邊距確保不被截掉

        // 確保內容高度至少有一個合理的最小值
        contentHeight = Mathf.Max(contentHeight, 100f);

        // 使用系統默認的背景，不添加自定義背景色
        _logScrollPosition = GUILayout.BeginScrollView(_logScrollPosition, GUI.skin.box, GUILayout.Height(contentHeight));

        // 為了確保最後一行日誌顯示完整，添加頂部和底部間距
        GUILayout.Space(5); // 頂部間距

        if (_uploadLog.Count > 0)
        {
            // 使用適合深色UI的文字樣式
            var logStyle = new GUIStyle(EditorStyles.label);
            logStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            logStyle.wordWrap = true;
            logStyle.richText = true;
            logStyle.padding = new RectOffset(5, 5, 2, 2); // 添加內邊距

            foreach (string log in _uploadLog)
            {
                GUILayout.Label(log, logStyle);
            }

            // 增加底部間距，確保最後一行日誌完全可見，特別是在多檔上傳時
            GUILayout.Space(35);
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("尚無日誌記錄", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndScrollView();
    }

    #endregion

    #region Profile Management

    /// <summary>
    /// 創建新的設定檔
    /// </summary>
    private void CreateNewProfile()
    {
        var newProfile = _currentProfile.Clone();
        newProfile.ProfileName = _newProfileName;
        _settings.AddProfile(newProfile);
        _settings.SelectedProfileIndex = _settings.Profiles.Count - 1;
        _currentProfile = _settings.GetSelectedProfile();
        _currentProfile.LocalDirectoryPath = "";

        // 檢查 Addressable Profile 設定
        if (!_currentProfile.ValidateAddressableProfile(out string errorMessage))
        {
            // EditorUtility.DisplayDialog(
            //     "Addressable Profile 設定問題",
            //     $"{errorMessage}\n\n請到 Window → Asset Management → Addressables → Profiles 進行設定",
            //     "確定"
            // );
            Debug.LogWarning(errorMessage);
        }

        _newProfileName = "新設定檔";
        AddLog($"已創建新設定檔: {newProfile.ProfileName}");
    }

    /// <summary>
    /// 刪除當前設定檔
    /// </summary>
    private void DeleteCurrentProfile()
    {
        if (EditorUtility.DisplayDialog("確認刪除",
            $"確定要刪除設定檔 '{_currentProfile.ProfileName}' 嗎？",
            "確定", "取消"))
        {
            string deletedProfileName = _currentProfile.ProfileName;
            _settings.RemoveProfile(_settings.SelectedProfileIndex);
            _currentProfile = _settings.GetSelectedProfile();
            _currentProfile.LocalDirectoryPath = "";

            // 檢查 Addressable Profile 設定
            if (!_currentProfile.ValidateAddressableProfile(out string errorMessage))
            {
                EditorUtility.DisplayDialog(
                    "Addressable Profile 設定問題",
                    $"{errorMessage}\n\n請到 Window → Asset Management → Addressables → Profiles 進行設定",
                    "確定"
                );
            }

            AddLog($"已刪除設定檔: {deletedProfileName}");
        }
    }



    #endregion

    #region Validation

    /// <summary>
    /// 驗證上傳操作所需的所有設定（包含主遊戲目錄）
    /// </summary>
    private bool IsSettingsValid()
    {
        return AWSS3UploaderAPI.IsGameUploadSettingsValid(_currentProfile);
    }

    /// <summary>
    /// 驗證 S3 操作所需的設定（不包含主遊戲目錄）
    /// </summary>
    private bool IsS3SettingsValid()
    {
        return !string.IsNullOrEmpty(_currentProfile.GetS3BucketName()) &&
               !string.IsNullOrEmpty(_currentProfile.AccessKeyId) &&
               !string.IsNullOrEmpty(_currentProfile.SecretAccessKey);
    }

    /// <summary>
    /// 檢查 AssetBundle 設定是否有效
    /// </summary>
    /// <returns>如果 AssetBundle 設定有效則返回 true</returns>
    private bool IsAssetBundleSettingsValid()
    {
        if (!_currentProfile.UploadAssetBundle)
            return true;

        return AWSS3UploaderAPI.IsAssetBundleSettingsValid(_currentProfile);
    }

    /// <summary>
    /// 驗證 S3 儲存桶名稱是否符合 AWS 規範
    /// </summary>
    /// <param name="bucketName">要驗證的儲存桶名稱</param>
    /// <returns>如果名稱有效則返回 true</returns>
    private bool ValidateS3BucketName(string bucketName)
    {
        if (string.IsNullOrEmpty(bucketName))
            return false;

        // 檢查長度
        if (bucketName.Length < S3_BUCKET_NAME_MIN_LENGTH ||
            bucketName.Length > S3_BUCKET_NAME_MAX_LENGTH)
            return false;

        // 檢查是否看起來像 IP 地址（AWS 不允許）
        if (System.Text.RegularExpressions.Regex.IsMatch(
            bucketName, @"^\d+\.\d+\.\d+\.\d+$"))
            return false;

        // 檢查字符規則：只能包含小寫字母、數字、連字符和點號
        if (!System.Text.RegularExpressions.Regex.IsMatch(
            bucketName, @"^[a-z0-9][a-z0-9\.\-]*[a-z0-9]$"))
            return false;

        // 檢查是否以字母或數字開頭和結尾
        if (!char.IsLetterOrDigit(bucketName[0]) || !char.IsLetterOrDigit(bucketName[bucketName.Length - 1]))
            return false;

        // 檢查是否包含連續的連字符（AWS 不允許）
        if (bucketName.Contains("--"))
            return false;

        // 檢查是否包含連續的點號（AWS 不允許）
        if (bucketName.Contains(".."))
            return false;

        // 檢查點號和連字符不能相鄰（AWS 不允許）
        if (bucketName.Contains(".-") || bucketName.Contains("-."))
            return false;

        return true;
    }

    private async void TestAWSConnection()
    {
        if (!IsS3SettingsValid())
        {
            AddLog("❌ 請先設定 AWS 憑證和 S3 儲存桶名稱");
            return;
        }

        using (var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(CONNECTION_TEST_TIMEOUT_SECONDS)))
        {
            try
            {
                CreateS3Client();
                AddLog("開始測試 AWS 連線...");
                string testPrefix = _currentProfile.GetWebGlUploadKeyPrefix();
                string testPath = $"s3://{_currentProfile.GetS3BucketName()}/{testPrefix}";
                AddLog($"🔍 測試讀取路徑 '{testPath}'");

                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _currentProfile.GetS3BucketName(),
                    Prefix = testPrefix,
                    MaxKeys = 5
                };

                var listResponse = await _s3Client.ListObjectsV2Async(listRequest, testCts.Token);

                AddLog("✅ AWS 連線成功！");
                AddLog("🎉 基本功能測試成功！可以進行檔案列表獲取和上傳操作");
            }
            catch (OperationCanceledException)
            {
                AddLog("❌ 連線測試超時或被取消");
            }
            catch (Exception ex)
            {
                AddLog($"❌ 連線測試失敗: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 創建並配置 S3 客戶端
    /// 使用預定義的常數來設定超時和重試參數
    /// </summary>
    private void CreateS3Client()
    {
        // 清理舊的客戶端
        _s3Client?.Dispose();

        // 創建 S3 配置，使用常數來確保一致的設定
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_currentProfile.AwsRegion),
            UseHttp = false, // 強制使用 HTTPS 提升安全性
            MaxErrorRetry = AWS_MAX_RETRY_COUNT, // 設定重試次數
            Timeout = TimeSpan.FromSeconds(AWS_CONNECTION_TIMEOUT_SECONDS), // 連線超時時間
            ReadWriteTimeout = TimeSpan.FromSeconds(AWS_CONNECTION_TIMEOUT_SECONDS), // 讀寫超時時間
            ForcePathStyle = true, // 使用路徑風格 URL，相容性更好
            UseDualstackEndpoint = false // 停用雙堆疊端點，避免不必要的複雜性
        };

        // 使用長期憑證
        AWSCredentials credentials = new BasicAWSCredentials(
            _currentProfile.AccessKeyId,
            _currentProfile.SecretAccessKey);

        _s3Client = new AmazonS3Client(credentials, config);
    }

    private async void StartUpload()
    {
        // 先檢查路徑是否匹配
        if (!CheckProjectPathMatch())
        {
            AddLog("上傳已被用戶取消");
            return;
        }

        // 顯示上傳確認對話框
        bool confirmed = ShowUploadConfirmationDialog();
        if (!confirmed)
        {
            AddLog("上傳已被用戶取消");
            return;
        }

        var preRelease = await AWSS3UploaderAPI.PrecheckReleaseVersionPathNotOccupiedOnS3Async(
            _currentProfile,
            checkWebGlPrefix: true,
            checkAssetBundlePrefix: _currentProfile.UploadAssetBundle && IsAssetBundleSettingsValid(),
            AddLog,
            CancellationToken.None);
        if (!preRelease.IsAllowed)
        {
            EditorUtility.DisplayDialog(
                "無法上傳",
                string.IsNullOrEmpty(preRelease.Message) ? "版本目錄檢查未通過。" : preRelease.Message,
                "確定");
            return;
        }

        // 記錄整體作業開始時間
        var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 初始化所有進度變數
        _isUploading = true;
        _uploadProgress = 0f;
        _totalFileCount = 0;
        _completedFileCount = 0;
        _statusMessage = "準備上傳...";
        AddLog("開始上傳程序");

        // 初始化取消令牌
        _cancellationTokenSource = new CancellationTokenSource();

        // 初始化並行上傳控制
        int maxUploads = _currentProfile.MaxConcurrentUploads;
        _uploadSemaphore = new SemaphoreSlim(maxUploads, maxUploads);

        lock (_progressLock)
        {
            _activeUploadingFileNames.Clear();
        }

        try
        {
            CreateS3Client();
            await ClearS3Directory(_cancellationTokenSource.Token);
            await UploadDirectoryParallel(_cancellationTokenSource.Token);

            if (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                AddLog("✅ 主要內容上傳完成！");

                // 檢查是否需要上傳 AssetBundle
                if (_currentProfile.UploadAssetBundle && IsAssetBundleSettingsValid())
                {
                    AddLog("🎯 開始上傳 AssetBundle...");
                    await UploadAssetBundleDirectoryParallel(_cancellationTokenSource.Token);

                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        AddLog("✅ AssetBundle 上傳完成！");
                    }
                }
                else if (_currentProfile.UploadAssetBundle && !IsAssetBundleSettingsValid())
                {
                    AddLog("⚠️ 已啟用 AssetBundle 上傳，但設定不完整，請檢查 Bundle 目錄和 S3 路徑");
                }

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    AddLog("✅ 所有上傳任務完成！");
                    await InvalidateCloudFrontAfterUpload(_cancellationTokenSource.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            AddLog("❌ 上傳已被取消");
        }
        catch (Exception ex)
        {
            AddLog($"上傳失敗: {ex.Message}");
        }
        finally
        {
            // 停止整體計時器並輸出總耗時
            overallStopwatch.Stop();
            string overallTimeFormat = overallStopwatch.Elapsed.TotalMinutes >= 1
                ? $"{overallStopwatch.Elapsed.TotalMinutes:F1}分鐘"
                : $"{overallStopwatch.Elapsed.TotalSeconds:F1}秒";

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                AddLog($"⏱️ 整體作業已取消，總耗時: {overallTimeFormat}");
            }
            else
            {
                AddLog($"⏱️ 整體作業完成，總耗時: {overallTimeFormat}");
            }

            _isUploading = false;
            _statusMessage = _cancellationTokenSource.Token.IsCancellationRequested ? "上傳已取消" : "上傳完成";
            ResetProgressVariables();
            _uploadSemaphore?.Dispose();
            _uploadSemaphore = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// 檢查本地路徑和 S3 路徑前綴是否匹配
    /// </summary>
    /// <returns>如果路徑匹配或用戶確認繼續則返回 true</returns>
    private bool CheckProjectPathMatch()
    {
        if (_currentProfile.UploadToRootDirectory)
            return true;

        string localPath = _currentProfile.LocalDirectoryPath;
        string s3ProjectName = _currentProfile.GetUploadProjectName();

        if (string.IsNullOrEmpty(localPath) || string.IsNullOrEmpty(s3ProjectName))
        {
            return true;
        }

        string projectNameFromPath = ExtractProjectNameFromPath(localPath);
        string projectIdFromPath = ExtractProjectIdFromName(projectNameFromPath);
        string projectIdFromS3 = ExtractProjectIdFromName(s3ProjectName);

        if (!string.IsNullOrEmpty(projectIdFromPath) && !string.IsNullOrEmpty(projectIdFromS3))
        {
            if (!projectIdFromPath.Equals(projectIdFromS3, StringComparison.OrdinalIgnoreCase))
            {
                return ShowProjectPathMismatchDialog(projectIdFromPath, s3ProjectName, localPath);
            }
        }

        return true;
    }

    /// <summary>
    /// 從本地路徑提取專案名稱
    /// </summary>
    /// <param name="localPath">本地路徑</param>
    /// <returns>專案名稱</returns>
    private string ExtractProjectNameFromPath(string localPath)
    {
        try
        {
            // 將路徑標準化
            string normalizedPath = localPath.Replace('\\', '/');

            // 分割路徑
            string[] pathParts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // 尋找包含 "slot" 的路徑段
            foreach (string part in pathParts)
            {
                if (part.ToLower().Contains("slot") && part.Contains("_"))
                {
                    return part;
                }
            }
        }
        catch (Exception ex)
        {
            AddLog($"⚠️ 提取專案名稱時發生錯誤: {ex.Message}");
        }

        return "";
    }

    /// <summary>
    /// 從專案名稱中提取專案編號
    /// </summary>
    /// <param name="projectName">專案名稱</param>
    /// <returns>專案編號</returns>
    private string ExtractProjectIdFromName(string projectName)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return "";
        }

        // 如果包含底線，取底線前的部分
        int underscoreIndex = projectName.IndexOf('_');
        if (underscoreIndex > 0)
        {
            return projectName.Substring(0, underscoreIndex);
        }

        return projectName;
    }

    /// <summary>
    /// 顯示專案路徑不匹配警告對話框
    /// </summary>
    /// <param name="localProjectId">本地專案編號</param>
    /// <param name="s3Prefix">S3 路徑前綴</param>
    /// <param name="localPath">本地路徑</param>
    /// <returns>如果用戶確認繼續則返回 true</returns>
    private bool ShowProjectPathMismatchDialog(string localProjectId, string s3Prefix, string localPath)
    {
        string message = $"⚠️ 路徑不匹配警告\n\n" +
                        $"偵測到主遊戲目錄與 S3 專案名（與 SSH 上傳一致）可能不一致：\n\n" +
                        $"📁 本地專案編號: {localProjectId}\n" +
                        $"☁️ S3 專案名: {s3Prefix}\n\n" +
                        $"完整本地路徑:\n{localPath}\n\n" +
                        $"這可能表示您選擇了錯誤的專案目錄或 S3 路徑。\n" +
                        $"建議檢查設定後再進行上傳。\n\n" +
                        $"您確定要繼續上傳嗎？";

        bool continueUpload = EditorUtility.DisplayDialog("路徑不匹配警告", message, "確定繼續", "取消上傳");

        if (continueUpload)
        {
            AddLog($"⚠️ 用戶確認繼續上傳，儘管路徑不匹配 (本地:{localProjectId} vs S3:{s3Prefix})");
        }
        else
        {
            AddLog($"📋 用戶取消上傳，路徑不匹配 (本地:{localProjectId} vs S3:{s3Prefix})");
        }

        return continueUpload;
    }

    /// <summary>
    /// 顯示上傳確認對話框
    /// </summary>
    /// <returns>如果用戶確認則返回 true</returns>
    private bool ShowUploadConfirmationDialog()
    {
        string localPath = _currentProfile.LocalDirectoryPath;
        string s3BucketName = _currentProfile.GetS3BucketName();
        string uploadPrefix = _currentProfile.GetUploadS3KeyPrefix();

        string message = $"確認上傳設定\n\n" +
                        $"📁 主遊戲目錄:\n{localPath}\n\n" +
                        $"☁️ S3 目標位置 (WebGL):\n s3://{s3BucketName}/{uploadPrefix}\n\n" +
                        $"⚠️ 重要警告:\n" +
                        $"上傳過程將會:\n" +
                        $"• 清除 S3 目標目錄中的所有現有檔案\n" +
                        $"• 上傳主遊戲目錄中的所有檔案到 S3\n" +
                        $"• 此操作無法復原，請確認路徑設定正確";

        return EditorUtility.DisplayDialog("確認上傳設定", message, "確認上傳", "取消");
    }

    /// <summary>
    /// 開始AssetBundle手動上傳
    /// </summary>
    private async void StartAssetBundleUpload()
    {
        if (!IsAssetBundleSettingsValid())
        {
            AddLog("❌ AssetBundle設定無效，請檢查設定");
            return;
        }

        string s3Path = $"s3://{_currentProfile.GetS3BucketName()}/{_currentProfile.GetAssetBundleS3FullPrefix()}";

        string message = $"確定要手動上傳AssetBundle嗎？\n\n" +
                        $"📁 本地目錄:\n{_currentProfile.GetAssetBundleDirectoryPath()}\n\n" +
                        $"☁️ 上傳至:\n{s3Path}";

        if (!EditorUtility.DisplayDialog("確認AssetBundle上傳", message, "確定上傳", "取消"))
        {
            AddLog("❌ 用戶取消AssetBundle上傳");
            return;
        }

        var preReleaseAb = await AWSS3UploaderAPI.PrecheckReleaseVersionPathNotOccupiedOnS3Async(
            _currentProfile,
            checkWebGlPrefix: false,
            checkAssetBundlePrefix: true,
            AddLog,
            CancellationToken.None);
        if (!preReleaseAb.IsAllowed)
        {
            EditorUtility.DisplayDialog(
                "無法上傳",
                string.IsNullOrEmpty(preReleaseAb.Message) ? "版本目錄檢查未通過。" : preReleaseAb.Message,
                "確定");
            return;
        }

        // 初始化上傳狀態
        _isUploading = true;
        _isUploadingAssetBundle = true;
        _uploadProgress = 0f;
        _statusMessage = "準備上傳AssetBundle...";
        AddLog("🎯 開始AssetBundle手動上傳");

        // 初始化取消令牌
        _cancellationTokenSource = new CancellationTokenSource();

        // 初始化並行上傳控制
        int maxUploads = _currentProfile.MaxConcurrentUploads;
        _uploadSemaphore = new SemaphoreSlim(maxUploads, maxUploads);

        lock (_progressLock)
        {
            _activeUploadingFileNames.Clear();
        }

        try
        {
            CreateS3Client();
            await UploadAssetBundleDirectoryParallel(_cancellationTokenSource.Token);

            if (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                AddLog("✅ AssetBundle手動上傳完成！");
                _statusMessage = "AssetBundle上傳完成";
            }
        }
        catch (OperationCanceledException)
        {
            AddLog("❌ AssetBundle上傳已被取消");
            _statusMessage = "AssetBundle上傳已取消";
        }
        catch (Exception ex)
        {
            AddLog($"❌ AssetBundle上傳失敗: {ex.Message}");
            _statusMessage = "AssetBundle上傳失敗";
        }
        finally
        {
            _isUploading = false;
            _isUploadingAssetBundle = false;
            ResetProgressVariables();
            _uploadSemaphore?.Dispose();
            _uploadSemaphore = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }



    /// <summary>
    /// 清除 S3 目錄中的所有物件
    /// </summary>
    private async UniTask ClearS3Directory(CancellationToken cancellationToken)
    {
        if (_currentProfile.UploadToRootDirectory)
        {
            await ClearS3RootDirectoryFiles(cancellationToken);
        }
        else
        {
            await ClearS3DirectoryFiles(cancellationToken);
        }
    }

    /// <summary>
    /// 清除 S3 根目錄中對應的檔案
    /// </summary>
    private async UniTask ClearS3RootDirectoryFiles(CancellationToken cancellationToken)
    {
        string webGlPrefix = _currentProfile.GetWebGlUploadKeyPrefix();
        AddLog($"🗑️ 正在清理 S3 WebGL 前綴「{webGlPrefix}/」內與本地上傳對應的檔案...");

        try
        {
            // 獲取本地要上傳的檔案列表
            string[] localFiles = Directory.GetFiles(_currentProfile.LocalDirectoryPath, "*", SearchOption.AllDirectories);
            var filesToDelete = new List<string>();

            foreach (string localFile in localFiles)
            {
                string relativePath = Path.GetRelativePath(_currentProfile.LocalDirectoryPath, localFile);
                string rel = relativePath.Replace('\\', '/');
                string s3Key = string.IsNullOrEmpty(webGlPrefix) ? rel : webGlPrefix + "/" + rel;
                filesToDelete.Add(s3Key);
            }

            if (filesToDelete.Count == 0)
            {
                AddLog("✅ 沒有對應的檔案需要清理");
                return;
            }

            AddLog($"📊 準備清理 {filesToDelete.Count} 個對應檔案");

            // 檢查這些檔案在 S3 中是否存在
            var existingObjects = new List<S3Object>();
            foreach (string s3Key in filesToDelete)
            {
                try
                {
                    var headRequest = new GetObjectMetadataRequest
                    {
                        BucketName = _currentProfile.GetS3BucketName(),
                        Key = s3Key
                    };

                    await _s3Client.GetObjectMetadataAsync(headRequest, cancellationToken);

                    // 如果沒有拋出異常，表示檔案存在
                    existingObjects.Add(new S3Object { Key = s3Key });
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 檔案不存在，忽略
                }
            }

            if (existingObjects.Count == 0)
            {
                AddLog("✅ S3 WebGL 前綴下沒有需要清理的對應檔案");
                return;
            }

            AddLog($"📊 找到 {existingObjects.Count} 個現有檔案需要清理");

            // 分批刪除物件
            const int batchSize = 1000;
            for (int i = 0; i < existingObjects.Count; i += batchSize)
            {
                var batchObjects = existingObjects.Skip(i).Take(batchSize).ToList();
                await DeleteObjectsBatch(batchObjects, cancellationToken);
            }

            AddLog("✅ S3 WebGL 前綴下對應檔案清理完成");
        }
        catch (OperationCanceledException)
        {
            AddLog("❌ 清理 S3 WebGL 對應檔案操作已被取消");
            throw;
        }
        catch (Exception ex)
        {
            AddLog($"❌ 清理 S3 WebGL 對應檔案時發生錯誤: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 清除 S3 指定目錄中的所有檔案
    /// </summary>
    private async UniTask ClearS3DirectoryFiles(CancellationToken cancellationToken)
    {
        AddLog("🗑️ 正在清理 S3 目錄...");

        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _currentProfile.GetS3BucketName(),
                Prefix = _currentProfile.GetUploadS3KeyPrefix()
            };

            var objects = new List<S3Object>();
            ListObjectsV2Response response;

            do
            {
                response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);
                objects.AddRange(response.S3Objects);
                listRequest.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);

            if (objects.Count == 0)
            {
                AddLog("✅ 目錄已經是空的，無需清理");
                return;
            }

            // 智能篩選：排除指定的資料夾
            var objectsToDelete = FilterObjectsForDeletion(objects);

            if (objectsToDelete.Count == 0)
            {
                AddLog("✅ 所有檔案都在排除清單中，無需清理");
                return;
            }

            AddLog($"📊 找到 {objects.Count} 個物件，其中 {objectsToDelete.Count} 個需要刪除");

            if (_currentProfile.ExcludeBundleSourceFromClear && objects.Count > objectsToDelete.Count)
            {
                int excludedCount = objects.Count - objectsToDelete.Count;
                AddLog($"🛡️ 智能保護：已排除 {excludedCount} 個位於「{_currentProfile.GetAssetBundleS3FullPrefix()}」的物件不被清除");
            }

            // 分批刪除物件（AWS 限制每次最多 1000 個）
            const int batchSize = 1000;
            for (int i = 0; i < objectsToDelete.Count; i += batchSize)
            {
                var batchObjects = objectsToDelete.Skip(i).Take(batchSize).ToList();
                await DeleteObjectsBatch(batchObjects, cancellationToken);
            }

            AddLog("✅ S3 目錄清理完成");
        }
        catch (OperationCanceledException)
        {
            AddLog("❌ 清理 S3 目錄操作已被取消");
            throw;
        }
        catch (Exception ex)
        {
            AddLog($"❌ 清理 S3 目錄時發生錯誤: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 篩選需要刪除的物件，排除指定的資料夾
    /// </summary>
    private List<S3Object> FilterObjectsForDeletion(List<S3Object> allObjects)
    {
        if (!_currentProfile.ExcludeBundleSourceFromClear)
        {
            return allObjects; // 如果沒有啟用智能清除，返回所有物件
        }

        var objectsToDelete = new List<S3Object>();

        string excludedPath = _currentProfile.GetAssetBundleS3FullPrefix();

        foreach (var obj in allObjects)
        {
            bool shouldExclude = obj.Key.StartsWith(excludedPath + "/") || obj.Key == excludedPath;

            if (!shouldExclude)
            {
                objectsToDelete.Add(obj);
            }
        }

        return objectsToDelete;
    }

    /// <summary>
    /// 分批刪除 S3 物件
    /// </summary>
    private async UniTask DeleteObjectsBatch(List<S3Object> objects, CancellationToken cancellationToken)
    {
        var deleteRequest = new DeleteObjectsRequest
        {
            BucketName = _currentProfile.GetS3BucketName(),
            Objects = objects.Select(obj => new KeyVersion { Key = obj.Key }).ToList()
        };

        try
        {
            var deleteResponse = await _s3Client.DeleteObjectsAsync(deleteRequest, cancellationToken);

            if (deleteResponse.DeletedObjects.Count > 0)
            {
                AddLog($"🗑️ 已刪除 {deleteResponse.DeletedObjects.Count} 個物件");
            }

            if (deleteResponse.DeleteErrors.Count > 0)
            {
                foreach (var error in deleteResponse.DeleteErrors)
                {
                    AddLog($"❌ 刪除失敗: {error.Key} - {error.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            AddLog("❌ 刪除物件操作已被取消");
            throw;
        }
        catch (Exception ex)
        {
            AddLog($"❌ 刪除物件時發生錯誤: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 從檔案路徑列表中提取所有需要建立的資料夾路徑
    /// </summary>
    /// <param name="s3Keys">所有檔案的 S3 Key 列表</param>
    /// <returns>需要建立的資料夾路徑集合（去重且按層級排序）</returns>
    private HashSet<string> ExtractFolderPaths(IEnumerable<string> s3Keys)
    {
        var folderPaths = new HashSet<string>();

        foreach (var s3Key in s3Keys)
        {
            // 取得資料夾路徑（去除檔案名稱）
            string directoryPath = Path.GetDirectoryName(s3Key)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directoryPath))
                continue;

            // 將路徑拆分並逐層加入
            string[] pathParts = directoryPath.Split('/');
            string currentPath = "";

            foreach (var part in pathParts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;
                folderPaths.Add(currentPath + "/");
            }
        }

        return folderPaths;
    }

    /// <summary>
    /// 在 S3 上建立資料夾結構
    /// </summary>
    /// <param name="folderPaths">要建立的資料夾路徑列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async UniTask EnsureFoldersExistAsync(IEnumerable<string> folderPaths, CancellationToken cancellationToken)
    {
        var sortedFolders = folderPaths.OrderBy(f => f.Count(c => c == '/')).ThenBy(f => f).ToList();

        if (sortedFolders.Count == 0)
        {
            return;
        }

        AddLog($"📁 開始建立 {sortedFolders.Count} 個資料夾結構...");

        // 先檢查哪些資料夾已經存在
        var existingFolders = new HashSet<string>();
        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _currentProfile.GetS3BucketName(),
                MaxKeys = 1000
            };

            // 只檢查資料夾的根前綴
            if (sortedFolders.Count > 0)
            {
                string commonPrefix = sortedFolders.First().Split('/').First();
                listRequest.Prefix = commonPrefix;
            }

            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);
                foreach (var obj in response.S3Objects)
                {
                    if (obj.Key.EndsWith("/"))
                    {
                        existingFolders.Add(obj.Key);
                    }
                }
                listRequest.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated && !cancellationToken.IsCancellationRequested);
        }
        catch (Exception ex)
        {
            AddLog($"⚠️ 檢查現有資料夾時發生錯誤: {ex.Message}，將嘗試建立所有資料夾");
        }

        int createdCount = 0;
        int skippedCount = 0;

        foreach (var folderPath in sortedFolders)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // 如果資料夾已存在則跳過
            if (existingFolders.Contains(folderPath))
            {
                skippedCount++;
                continue;
            }

            try
            {
                var request = new PutObjectRequest
                {
                    BucketName = _currentProfile.GetS3BucketName(),
                    Key = folderPath,
                    ContentBody = ""
                };

                await _s3Client.PutObjectAsync(request, cancellationToken);
                createdCount++;
                AddLog($"📁 已建立資料夾: {folderPath}");
            }
            catch (Exception ex)
            {
                AddLog($"⚠️ 建立資料夾失敗: {folderPath} - {ex.Message}");
            }
        }

        if (createdCount > 0 || skippedCount > 0)
        {
            AddLog($"✅ 資料夾結構建立完成 - 新建立: {createdCount} 個，已存在: {skippedCount} 個");
        }
    }

    /// <summary>
    /// 並行上傳目錄中的所有檔案到 S3
    /// </summary>
    private async UniTask UploadDirectoryParallel(CancellationToken cancellationToken)
    {
        // 記錄主遊戲上傳開始時間
        var gameStopwatch = System.Diagnostics.Stopwatch.StartNew();

        string[] files = Directory.GetFiles(_currentProfile.LocalDirectoryPath, "*", SearchOption.AllDirectories);
        _totalFileCount = files.Length;
        _completedFileCount = 0;

        AddLog($"開始並行上傳 {_totalFileCount} 個檔案 (最多同時上傳 {_currentProfile.MaxConcurrentUploads} 個)");

        // 按檔案大小排序（小檔案優先）
        var sortedFiles = SortFilesBySize(files);
        AddLog($"✅ 檔案已按大小排序 - 小檔案優先上傳");

        // 收集所有 S3 Key
        string uploadPrefix = _currentProfile.GetUploadS3KeyPrefix();
        var s3Keys = sortedFiles.Select(f =>
        {
            string relativePath = Path.GetRelativePath(_currentProfile.LocalDirectoryPath, f.FilePath);
            if (string.IsNullOrEmpty(uploadPrefix))
            {
                return relativePath.Replace('\\', '/');
            }
            else
            {
                return uploadPrefix + "/" + relativePath.Replace('\\', '/');
            }
        }).ToList();

        // 建立資料夾結構
        var folderPaths = ExtractFolderPaths(s3Keys);
        if (folderPaths.Count > 0)
        {
            await EnsureFoldersExistAsync(folderPaths, cancellationToken);
        }

        // 建立上傳任務清單
        var uploadTasks = new List<UniTask>();

        foreach (var fileInfo in sortedFiles)
        {
            if (cancellationToken.IsCancellationRequested) break; // 檢查是否取消

            string relativePath = Path.GetRelativePath(_currentProfile.LocalDirectoryPath, fileInfo.FilePath);
            string s3Key;

            if (string.IsNullOrEmpty(uploadPrefix))
            {
                // 根目錄上傳：直接使用相對路徑作為 S3 Key
                s3Key = relativePath.Replace('\\', '/');
            }
            else
            {
                // 一般上傳：加上前綴
                s3Key = uploadPrefix + "/" + relativePath.Replace('\\', '/');
            }

            // 建立上傳任務
            var uploadTask = UploadFileParallel(fileInfo.FilePath, s3Key, relativePath, cancellationToken);
            uploadTasks.Add(uploadTask);
        }

        // 等待所有上傳完成
        await UniTask.WhenAll(uploadTasks);

        // 停止計時器並輸出主遊戲上傳耗時
        gameStopwatch.Stop();
        string gameTimeFormat = gameStopwatch.Elapsed.TotalMinutes >= 1
            ? $"{gameStopwatch.Elapsed.TotalMinutes:F1}分鐘"
            : $"{gameStopwatch.Elapsed.TotalSeconds:F1}秒";

        if (cancellationToken.IsCancellationRequested)
        {
            AddLog($"⏱️ 主遊戲作業已取消，總耗時: {gameTimeFormat}");
        }
        else
        {
            AddLog($"⏱️ 主遊戲作業完成，總耗時: {gameTimeFormat}");
        }
    }

    /// <summary>
    /// 並行上傳 AssetBundle 目錄中的所有檔案到 S3
    /// </summary>
    private async UniTask UploadAssetBundleDirectoryParallel(CancellationToken cancellationToken)
    {
        // 記錄開始時間
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

        string[] files = Directory.GetFiles(_currentProfile.GetAssetBundleDirectoryPath(), "*", SearchOption.AllDirectories);

        // 設置AssetBundle上傳狀態
        _isUploadingAssetBundle = true;
        _assetBundleFileCount = files.Length;
        _assetBundleCompletedFileCount = 0;
        _assetBundleProgress = 0f;

        AddLog($"開始並行上傳 {_assetBundleFileCount} 個 AssetBundle 檔案 (最多同時上傳 {_currentProfile.MaxConcurrentUploads} 個)");

        // 按檔案大小排序（小檔案優先）
        var sortedFiles = SortFilesBySize(files);
        AddLog($"✅ AssetBundle 檔案已按大小排序 - 小檔案優先上傳");

        // 如果啟用了重複檢查，先獲取S3上的檔案資訊
        Dictionary<string, S3FileInfo> s3FileMap = null;
        if (_currentProfile.SkipDuplicateBundleUploads)
        {
            AddLog($"✅ 重複檢查功能已啟用");
            AddLog("🔍 正在檢查S3上現有的AssetBundle檔案...");
            string fullS3Path = _currentProfile.GetAssetBundleS3FullPrefix();
            AddLog($"🎯 檢查S3路徑: {fullS3Path}");
            s3FileMap = await GetS3FileInfoMap(fullS3Path, cancellationToken);
            AddLog($"📊 S3上現有 {s3FileMap.Count} 個AssetBundle檔案");

            if (s3FileMap.Count > 0)
            {
                AddLog($"📋 S3現有檔案列表前5個:");
                int count = 0;
                foreach (var kvp in s3FileMap)
                {
                    if (count >= 5) break;
                    AddLog($"   {kvp.Key} ({FormatFileSize(kvp.Value.Size)})");
                    count++;
                }
            }
        }
        else
        {
            AddLog($"❌ 重複檢查功能已停用，將上傳所有檔案");
        }

        // 收集所有 S3 Key 並建立資料夾結構
        string assetBundleFullS3Path = _currentProfile.GetAssetBundleS3FullPrefix();

        var assetBundleS3Keys = sortedFiles.Select(f =>
        {
            string relativePath = Path.GetRelativePath(_currentProfile.GetAssetBundleDirectoryPath(), f.FilePath);
            return assetBundleFullS3Path + "/" + relativePath.Replace('\\', '/');
        }).ToList();

        var assetBundleFolderPaths = ExtractFolderPaths(assetBundleS3Keys);
        if (assetBundleFolderPaths.Count > 0)
        {
            await EnsureFoldersExistAsync(assetBundleFolderPaths, cancellationToken);
        }

        // 建立上傳任務清單
        var uploadTasks = new List<UniTask>();
        int skippedCount = 0;

        foreach (var fileInfo in sortedFiles)
        {
            if (cancellationToken.IsCancellationRequested) break; // 檢查是否取消

            string relativePath = Path.GetRelativePath(_currentProfile.GetAssetBundleDirectoryPath(), fileInfo.FilePath);
            string s3Key = assetBundleFullS3Path + "/" + relativePath.Replace('\\', '/');

            // 檢查是否需要上傳
            if (s3FileMap != null && !ShouldUploadFile(fileInfo.FilePath, s3Key, s3FileMap))
            {
                skippedCount++;
                // 直接更新進度，不實際上傳
                lock (_progressLock)
                {
                    _assetBundleCompletedFileCount++;
                    _assetBundleProgress = _assetBundleFileCount > 0 ? (float)_assetBundleCompletedFileCount / _assetBundleFileCount : 0f;
                }
                continue; // 跳過此檔案
            }

            // 建立上傳任務
            var uploadTask = UploadAssetBundleFileParallel(fileInfo.FilePath, s3Key, relativePath, cancellationToken);
            uploadTasks.Add(uploadTask);
        }

        if (skippedCount > 0)
        {
            AddLog($"⚡ 智能跳過：{skippedCount} 個檔案無需重複上傳，節省了上傳流量");
        }

        if (uploadTasks.Count > 0)
        {
            AddLog($"🚀 實際需要上傳 {uploadTasks.Count} 個檔案");
            // 等待所有上傳完成
            await UniTask.WhenAll(uploadTasks);
        }
        else
        {
            AddLog("✅ 所有AssetBundle檔案都是最新版本，無需上傳");
        }

        // 停止計時器並輸出總耗時
        totalStopwatch.Stop();
        string timeFormat = totalStopwatch.Elapsed.TotalMinutes >= 1
            ? $"{totalStopwatch.Elapsed.TotalMinutes:F1}分鐘"
            : $"{totalStopwatch.Elapsed.TotalSeconds:F1}秒";

        if (cancellationToken.IsCancellationRequested)
        {
            AddLog($"⏱️ AssetBundle作業已取消，總耗時: {timeFormat}");
        }
        else
        {
            AddLog($"⏱️ AssetBundle作業完成，總耗時: {timeFormat}");
        }

        // 重置AssetBundle上傳狀態
        _isUploadingAssetBundle = false;
    }



    /// <summary>
    /// 並行上傳單個檔案
    /// </summary>
    private async UniTask UploadFileParallel(string filePath, string s3Key, string relativePath, CancellationToken cancellationToken)
    {
        // 等待信號量，控制並行數量
        await _uploadSemaphore.WaitAsync(cancellationToken);

        // 記錄開始時間
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (cancellationToken.IsCancellationRequested) return; // 檢查是否取消

            string fileName = Path.GetFileName(filePath);

            // 獲取檔案大小
            long fileSize = 0;
            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                fileSize = fileInfo.Length;
            }
            catch (Exception ex)
            {
                AddLog($"⚠️ 無法讀取檔案大小: {fileName} - {ex.Message}");
            }

            // 線程安全地添加到正在上傳列表並更新狀態
            lock (_progressLock)
            {
                _activeUploadingFileNames.Add(fileName);
                _statusMessage = $"並行上傳中... ({_activeUploadingFileNames.Count}/{_currentProfile.MaxConcurrentUploads})";
            }

            AddLog($"🔄 開始上傳: {FormatFilePathForLog(relativePath)} ({FormatFileSize(fileSize)})");
            AddLog($"📍 目標路徑: s3://{_currentProfile.GetS3BucketName()}/{s3Key}");

            var request = new PutObjectRequest
            {
                BucketName = _currentProfile.GetS3BucketName(),
                Key = s3Key,
                FilePath = filePath
            };

            // 根據檔案副檔名設定 Content-Type 和 Content-Encoding
            ConfigureContentTypeAndEncoding(request, fileName);

            // 檢查是否在上傳前被取消
            if (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                AddLog($"❌ 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
                return;
            }

            await _s3Client.PutObjectAsync(request, cancellationToken);

            // 停止計時器
            stopwatch.Stop();

            // 檢查是否在上傳完成後被取消
            if (cancellationToken.IsCancellationRequested)
            {
                AddLog($"❌ 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
                return;
            }

            AddLog($"✅ 上傳完成: {FormatFilePathForLog(relativePath)} ({FormatFileSize(fileSize)}, 耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
            AddLog($"📍 已上傳至: s3://{_currentProfile.GetS3BucketName()}/{s3Key}");

            // 線程安全地更新進度和狀態
            lock (_progressLock)
            {
                _completedFileCount++;
                _uploadProgress = (float)_completedFileCount / _totalFileCount;
                _activeUploadingFileNames.Remove(fileName);

                // 根據剩餘上傳數量更新狀態訊息
                if (_activeUploadingFileNames.Count > 0)
                {
                    _statusMessage = $"並行上傳中... ({_activeUploadingFileNames.Count}/{_currentProfile.MaxConcurrentUploads})";
                }
                else
                {
                    _statusMessage = $"上傳進度: {_completedFileCount}/{_totalFileCount}";
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 上傳被取消，這是正常情況
            string fileName = Path.GetFileName(filePath);
            stopwatch.Stop();
            AddLog($"❌ 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");

            lock (_progressLock)
            {
                _activeUploadingFileNames.Remove(fileName);

                // 根據剩餘上傳數量更新狀態訊息
                if (_activeUploadingFileNames.Count > 0)
                {
                    _statusMessage = $"並行上傳中... ({_activeUploadingFileNames.Count}/{_currentProfile.MaxConcurrentUploads})";
                }
                else
                {
                    _statusMessage = $"上傳進度: {_completedFileCount}/{_totalFileCount}";
                }
            }
        }
        catch (Exception ex)
        {
            string fileName = Path.GetFileName(filePath);
            stopwatch.Stop();
            AddLog($"❌ 上傳失敗: {FormatFilePathForLog(relativePath)} - {ex.Message} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");

            lock (_progressLock)
            {
                _activeUploadingFileNames.Remove(fileName);

                // 根據剩餘上傳數量更新狀態訊息
                if (_activeUploadingFileNames.Count > 0)
                {
                    _statusMessage = $"並行上傳中... ({_activeUploadingFileNames.Count}/{_currentProfile.MaxConcurrentUploads})";
                }
                else
                {
                    _statusMessage = $"上傳進度: {_completedFileCount}/{_totalFileCount}";
                }
            }
        }
        finally
        {
            // 釋放信號量
            _uploadSemaphore.Release();
        }
    }

    /// <summary>
    /// 並行上傳單個 AssetBundle 檔案
    /// </summary>
    private async UniTask UploadAssetBundleFileParallel(string filePath, string s3Key, string relativePath, CancellationToken cancellationToken)
    {
        // 等待信號量，控制並行數量
        await _uploadSemaphore.WaitAsync(cancellationToken);

        // 記錄開始時間
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (cancellationToken.IsCancellationRequested) return; // 檢查是否取消

            string fileName = Path.GetFileName(filePath);

            // 獲取檔案大小
            long fileSize = 0;
            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                fileSize = fileInfo.Length;
            }
            catch (Exception ex)
            {
                AddLog($"⚠️ 無法讀取 AssetBundle 檔案大小: {fileName} - {ex.Message}");
            }

            // 線程安全地添加到正在上傳列表並更新狀態
            lock (_progressLock)
            {
                _activeUploadingFileNames.Add(fileName);
                _statusMessage = $"並行上傳 AssetBundle 中... ({_activeUploadingFileNames.Count}/{_currentProfile.MaxConcurrentUploads})";
            }

            AddLog($"🎯 開始上傳 AssetBundle: {FormatFilePathForLog(relativePath)} ({FormatFileSize(fileSize)})");
            AddLog($"📍 目標路徑: s3://{_currentProfile.GetS3BucketName()}/{s3Key}");

            var request = new PutObjectRequest
            {
                BucketName = _currentProfile.GetS3BucketName(),
                Key = s3Key,
                FilePath = filePath
            };

            // 根據檔案副檔名設定 Content-Type 和 Content-Encoding
            ConfigureContentTypeAndEncoding(request, fileName);

            // 檢查是否在上傳前被取消
            if (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                AddLog($"❌ AssetBundle 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
                return;
            }

            await _s3Client.PutObjectAsync(request, cancellationToken);

            // 停止計時器
            stopwatch.Stop();

            // 檢查是否在上傳完成後被取消
            if (cancellationToken.IsCancellationRequested)
            {
                AddLog($"❌ AssetBundle 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
                return;
            }

            AddLog($"✅ AssetBundle 上傳完成: {FormatFilePathForLog(relativePath)} ({FormatFileSize(fileSize)}, 耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
            AddLog($"📍 已上傳至: s3://{_currentProfile.GetS3BucketName()}/{s3Key}");

            // 線程安全地更新狀態和進度
            lock (_progressLock)
            {
                _activeUploadingFileNames.Remove(fileName);
                _assetBundleCompletedFileCount++;
                _assetBundleProgress = _assetBundleFileCount > 0 ? (float)_assetBundleCompletedFileCount / _assetBundleFileCount : 0f;

                // 根據剩餘上傳數量更新狀態訊息
                if (_activeUploadingFileNames.Count > 0)
                {
                    _statusMessage = $"並行上傳 AssetBundle 中... ({_activeUploadingFileNames.Count}/{_currentProfile.MaxConcurrentUploads})";
                }
                else
                {
                    _statusMessage = $"AssetBundle 上傳進度: {_assetBundleCompletedFileCount}/{_assetBundleFileCount}";
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 上傳被取消，這是正常情況
            string fileName = Path.GetFileName(filePath);
            stopwatch.Stop();
            AddLog($"❌ AssetBundle 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");

            lock (_progressLock)
            {
                _activeUploadingFileNames.Remove(fileName);

                // 根據剩餘上傳數量更新狀態訊息
                if (_activeUploadingFileNames.Count > 0)
                {
                    _statusMessage = $"並行上傳 AssetBundle 中... ({_activeUploadingFileNames.Count}/{_currentProfile.MaxConcurrentUploads})";
                }
                else
                {
                    _statusMessage = $"AssetBundle 上傳進度: {_assetBundleCompletedFileCount}/{_assetBundleFileCount}";
                }
            }
        }
        catch (Exception ex)
        {
            string fileName = Path.GetFileName(filePath);
            stopwatch.Stop();
            AddLog($"❌ AssetBundle 上傳失敗: {FormatFilePathForLog(relativePath)} - {ex.Message} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");

            lock (_progressLock)
            {
                _activeUploadingFileNames.Remove(fileName);

                // 根據剩餘上傳數量更新狀態訊息
                if (_activeUploadingFileNames.Count > 0)
                {
                    _statusMessage = $"並行上傳 AssetBundle 中... ({_activeUploadingFileNames.Count}/{_currentProfile.MaxConcurrentUploads})";
                }
                else
                {
                    _statusMessage = $"AssetBundle 上傳進度: {_assetBundleCompletedFileCount}/{_assetBundleFileCount}";
                }
            }
        }
        finally
        {
            // 釋放信號量
            _uploadSemaphore.Release();
        }
    }

    /// <summary>
    /// 配置檔案的 Content-Type 和 Content-Encoding
    /// </summary>
    private void ConfigureContentTypeAndEncoding(PutObjectRequest request, string fileName)
    {
        if (fileName.EndsWith(".wasm.br"))
        {
            request.ContentType = "application/wasm";
            request.Headers["Content-Encoding"] = "br";
        }
        else if (fileName.EndsWith(".data.br"))
        {
            request.ContentType = "application/octet-stream";
            request.Headers["Content-Encoding"] = "br";
        }
        else if (fileName.EndsWith(".js.br"))
        {
            request.ContentType = "application/javascript";
            request.Headers["Content-Encoding"] = "br";
        }
        else if (fileName.EndsWith(".br"))
        {
            request.Headers["Content-Encoding"] = "br";
        }
    }

    private void CancelUpload()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            AddLog("🛑 正在取消上傳操作...");
        }

        _isUploading = false;
        _statusMessage = "正在取消上傳...";

        // 清空正在上傳的檔案列表
        lock (_progressLock)
        {
            _activeUploadingFileNames.Clear();
        }

        ResetProgressVariables();
    }

    /// <summary>
    /// 重置所有進度變數到初始狀態
    /// </summary>
    private void ResetProgressVariables()
    {
        _totalFileCount = 0;
        _completedFileCount = 0;

        // 重置AssetBundle進度變數
        _isUploadingAssetBundle = false;
        _assetBundleFileCount = 0;
        _assetBundleCompletedFileCount = 0;
        _assetBundleProgress = 0f;

        lock (_progressLock)
        {
            _activeUploadingFileNames.Clear();
        }
    }

    private async void GetS3FileList()
    {
        if (!IsS3SettingsValid())
        {
            AddLog("❌ 請先設定 AWS 憑證和 S3 儲存桶名稱");
            return;
        }

        using (var listCts = new CancellationTokenSource(TimeSpan.FromSeconds(FILE_LIST_TIMEOUT_SECONDS)))
        {
            try
            {
                CreateS3Client();
                AddLog("開始獲取 S3 檔案列表...");

                string listPrefix = _currentProfile.GetWebGlUploadKeyPrefix();
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _currentProfile.GetS3BucketName(),
                    Prefix = listPrefix,
                    MaxKeys = MAX_FILE_LIST_DISPLAY_COUNT
                };

                var listResponse = await _s3Client.ListObjectsV2Async(listRequest, listCts.Token);

                if (listResponse.S3Objects.Count == 0)
                {
                    string displayPath = $"'{_currentProfile.GetS3BucketName()}/{listPrefix}' (WebGL 前綴)";
                    AddLog($"📁 S3 路徑 {displayPath} 下沒有檔案");
                    return;
                }

                AddLog($"✅ 檔案列表獲取完成，共 {listResponse.S3Objects.Count} 個檔案");
            }
            catch (OperationCanceledException)
            {
                AddLog("❌ 獲取檔案列表超時或被取消");
            }
            catch (Exception ex)
            {
                AddLog($"❌ 獲取 S3 檔案列表失敗: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 檔案信息結構，包含檔案路徑和大小
    /// </summary>
    private struct FileInfo
    {
        public string FilePath { get; set; }
        public long Size { get; set; }
    }

    /// <summary>
    /// 按檔案大小排序檔案列表（小檔案優先）
    /// </summary>
    private FileInfo[] SortFilesBySize(string[] filePaths)
    {
        var fileInfos = new List<FileInfo>();
        long totalSize = 0;

        // 收集檔案資訊
        foreach (string filePath in filePaths)
        {
            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                long size = fileInfo.Length;
                totalSize += size;

                fileInfos.Add(new FileInfo
                {
                    FilePath = filePath,
                    Size = size
                });
            }
            catch (Exception ex)
            {
                AddLog($"⚠️ 無法讀取檔案資訊: {filePath} - {ex.Message}");
                // 如果無法讀取檔案大小，設為 0（會被優先上傳）
                fileInfos.Add(new FileInfo
                {
                    FilePath = filePath,
                    Size = 0
                });
            }
        }

        // 按大小排序（小到大）
        var sortedFiles = fileInfos.OrderBy(f => f.Size).ToArray();

        // 顯示排序統計
        AddLog($"📊 檔案大小統計:");
        AddLog($"  • 總檔案大小: {FormatFileSize(totalSize)}");
        AddLog($"  • 最小檔案: {FormatFileSize(sortedFiles.First().Size)}");
        AddLog($"  • 最大檔案: {FormatFileSize(sortedFiles.Last().Size)}");
        AddLog($"  • 平均檔案大小: {FormatFileSize(totalSize / filePaths.Length)}");

        return sortedFiles;
    }

    /// <summary>
    /// 格式化檔案大小顯示，使用預定義的精度常數
    /// </summary>
    /// <param name="bytes">檔案大小（位元組）</param>
    /// <returns>格式化後的檔案大小字串</returns>
    private string FormatFileSize(long bytes)
    {
        if (bytes == 0)
            return "0 B";

        if (bytes < 0)
            return "Unknown";

        // 使用標準的位元組單位
        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int order = 0;
        double size = bytes;

        // 計算適當的單位
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size = size / 1024;
        }

        // 使用預定義的精度常數格式化數字
        string formatString = $"F{FILE_SIZE_DISPLAY_PRECISION}";
        string formattedSize = size.ToString(formatString);

        // 移除不必要的尾隨零
        if (formattedSize.Contains("."))
        {
            formattedSize = formattedSize.TrimEnd('0').TrimEnd('.');
        }

        return $"{formattedSize} {sizes[order]}";
    }

    /// <summary>
    /// 添加日誌訊息並管理記憶體使用
    /// </summary>
    /// <param name="message">日誌訊息</param>
    private void AddLog(string message)
    {
        // 防止空訊息
        if (string.IsNullOrEmpty(message))
            return;

        // 限制訊息長度，防止過長的錯誤訊息影響性能
        if (message.Length > MAX_ERROR_MESSAGE_LENGTH)
        {
            message = message.Substring(0, MAX_ERROR_MESSAGE_LENGTH) + "...";
        }

        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        // 允許使用富文本標籤，使用統一的時間戳顏色
        string logEntry = $"<color=#888888>[{timestamp}]</color> {message}";
        _uploadLog.Add(logEntry);

        // 記憶體管理：使用預定義的常數來限制日誌條數
        while (_uploadLog.Count > LOG_MAX_ENTRIES)
        {
            _uploadLog.RemoveAt(0);
        }

        // 根據設定決定是否自動滾動到最新日誌
        if (_autoScrollLog)
        {
            _logScrollPosition = new Vector2(0, float.MaxValue);
        }

        // 重繪視窗
        Repaint();
    }

    /// <summary>
    /// 清理 AWS S3 客戶端資源
    /// </summary>
    private void CleanupS3Client()
    {
        _s3Client?.Dispose();
        _s3Client = null;

        _uploadSemaphore?.Dispose();
        _uploadSemaphore = null;

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    /// <summary>
    /// 格式化檔案路徑顯示，優先保留完整的檔案名，只截斷目錄路徑
    /// </summary>
    /// <param name="filePath">檔案路徑</param>
    /// <returns>格式化後的檔案路徑</returns>
    private string FormatFilePathForLog(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "";

        // 如果路徑不超過最大長度，直接返回
        if (filePath.Length <= MAX_LOG_FILE_PATH_LENGTH)
            return filePath;

        // 優先保留完整的檔案名（包括副檔名）
        string fileName = Path.GetFileName(filePath);

        // 如果檔案名本身就超過最大長度，直接返回檔案名（不截斷）
        if (fileName.Length >= MAX_LOG_FILE_PATH_LENGTH)
        {
            return fileName;
        }

        // 獲取目錄路徑
        string directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directoryPath))
        {
            return fileName;
        }

        // 計算可用於顯示目錄路徑的字符數（保留"..."的3個字符）
        int availableLength = MAX_LOG_FILE_PATH_LENGTH - fileName.Length - 3;

        if (availableLength <= 0)
        {
            return fileName;
        }

        // 如果目錄路徑可以完全顯示
        if (directoryPath.Length <= availableLength)
        {
            return Path.Combine(directoryPath, fileName);
        }

        // 截斷目錄路徑並加上省略號，保留完整檔案名
        string truncatedDirectory = directoryPath.Substring(0, availableLength);
        return $"{truncatedDirectory}.../{fileName}";
    }

    #endregion

    /// <summary>
    /// 檢查S3上的檔案資訊
    /// </summary>
    private async UniTask<Dictionary<string, S3FileInfo>> GetS3FileInfoMap(string s3Prefix, CancellationToken cancellationToken)
    {
        var fileInfoMap = new Dictionary<string, S3FileInfo>();

        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _currentProfile.GetS3BucketName(),
                Prefix = s3Prefix
            };

            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

                foreach (var obj in response.S3Objects)
                {
                    fileInfoMap[obj.Key] = new S3FileInfo
                    {
                        Size = obj.Size,
                        LastModified = obj.LastModified,
                        ETag = obj.ETag?.Trim('"') // 移除ETag的引號
                    };
                }

                listRequest.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);
        }
        catch (Exception ex)
        {
            AddLog($"⚠️ 獲取S3檔案資訊時發生錯誤: {ex.Message}");
        }

        return fileInfoMap;
    }

    /// <summary>
    /// 檢查本地檔案是否需要上傳（與S3檔案比較）
    /// 只比較檔案大小，避免時區問題
    /// </summary>
    private bool ShouldUploadFile(string localFilePath, string s3Key, Dictionary<string, S3FileInfo> s3FileMap)
    {
        if (!_currentProfile.SkipDuplicateBundleUploads)
        {
            return true; // 如果沒有啟用重複檢查，總是上傳
        }

        // catalog檔案總是需要上傳，因為內容可能變更但檔案大小相同
        string fileName = Path.GetFileName(localFilePath);
        if (fileName.ToLower().Contains("catalog"))
        {
            AddLog($"📋 Catalog檔案總是上傳: {fileName}");
            return true;
        }

        if (!s3FileMap.TryGetValue(s3Key, out S3FileInfo s3FileInfo))
        {
            AddLog($"📤 新檔案需要上傳: {Path.GetFileName(localFilePath)}");
            return true; // S3上沒有這個檔案，需要上傳
        }

        try
        {
            var localFileInfo = new System.IO.FileInfo(localFilePath);

            // 只比較檔案大小
            if (localFileInfo.Length != s3FileInfo.Size)
            {
                AddLog($"📤 檔案大小不同，需要上傳: {Path.GetFileName(localFilePath)} (本地:{FormatFileSize(localFileInfo.Length)} vs S3:{FormatFileSize(s3FileInfo.Size)})");
                return true; // 大小不同，需要上傳
            }

            AddLog($"⚡ 檔案相同，跳過: {Path.GetFileName(localFilePath)} ({FormatFileSize(localFileInfo.Length)})");
            return false; // 檔案相同，跳過上傳
        }
        catch (Exception ex)
        {
            AddLog($"⚠️ 比較檔案時發生錯誤: {ex.Message}");
            return true; // 發生錯誤時，預設上傳
        }
    }
    
    /// <summary>
    /// 上传完成后自动执行 CloudFront Invalidation（實作在 <see cref="AWSS3UploaderAPI"/>，供 BuildScript 等共用）。
    /// </summary>
    private async UniTask InvalidateCloudFrontAfterUpload(CancellationToken cancellationToken)
    {
        await AWSS3UploaderAPI.InvalidateCloudFrontAfterUploadAsync(_currentProfile, AddLog, cancellationToken);
    }
}


