using UnityEngine;
using UnityEditor;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Amazon;
using Amazon.Runtime;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

/// <summary>
/// AWS S3 上傳器 API
/// 提供獨立的功能模組供外部調用
/// </summary>
public static class AWSS3UploaderAPI
{
    #region Constants

    // AWS & Upload Constants
    /// <summary>單次 PutObject 整體請求逾時。舊版 60s 在慢網/大檔時極易失敗。</summary>
    private const int AWS_S3_UPLOAD_REQUEST_TIMEOUT_SECONDS = 600;

    /// <summary>兩次讀寫之間允許的空檔（慢速上傳時單 chunk 間隔可能很長）。</summary>
    private const int AWS_S3_UPLOAD_READWRITE_TIMEOUT_SECONDS = 300;

    /// <summary>SDK 內建 MaxErrorRetry（指數退避），弱網可略調高。</summary>
    private const int AWS_MAX_RETRY_COUNT = 6;

    /// <summary>單檔 PutObject 應用層重試：首次嘗試 + 失敗後額外次數（與 SDK 內建 MaxErrorRetry 疊加）。</summary>
    private const int UPLOAD_PUT_APP_EXTRA_RETRIES = 5;

    private const int UPLOAD_PUT_RETRY_BASE_DELAY_MS = 1500;
    private const int UPLOAD_PUT_RETRY_MAX_DELAY_MS = 60000;

    // 檔案操作相關常數
    private const int MAX_FILE_LIST_DISPLAY_COUNT = 1000;
    private const int FILE_SIZE_DISPLAY_PRECISION = 2;

    // 錯誤處理相關常數
    private const int CONNECTION_TEST_TIMEOUT_SECONDS = 30;
    private const int FILE_LIST_TIMEOUT_SECONDS = 60;
    private const int MAX_LOG_FILE_PATH_LENGTH = 60;

    #endregion

    #region Nested Classes

    /// <summary>
    /// S3檔案資訊結構
    /// </summary>
    public struct S3FileInfo
    {
        public long Size;
        public DateTime LastModified;
        public string ETag;
    }

    /// <summary>
    /// 檔案信息結構，包含檔案路徑和大小
    /// </summary>
    public struct FileInfo
    {
        public string FilePath { get; set; }
        public long Size { get; set; }
    }

    /// <summary>
    /// 上傳進度回報結構
    /// </summary>
    public struct UploadProgress
    {
        public int CompletedFiles;
        public int TotalFiles;
        public float ProgressPercentage;
        public string CurrentFileName;
        public string StatusMessage;
        public List<string> ActiveUploads;
    }

    /// <summary>
    /// 上傳結果結構
    /// </summary>
    public struct UploadResult
    {
        public bool IsSuccess;
        public string Message;
        public int TotalFilesUploaded;
        public TimeSpan Duration;
        public Exception Exception;
    }

    /// <summary>
    /// 正式版 <c>v-…</c> 路徑佔用檢查結果（上傳前若 S3 已存在該版本目錄則不應覆寫）。
    /// </summary>
    public struct ReleaseVersionPathPrecheckResult
    {
        public bool IsAllowed;
        public string Message;
    }

    #endregion

    #region Public API Methods

    /// <summary>
    /// 測試AWS連接
    /// </summary>
    /// <param name="profile">S3設定檔</param>
    /// <param name="onLog">日誌回調函數</param>
    /// <returns>連接測試結果</returns>
    /// <summary>
    /// 在上傳前檢查 S3 是否已存在當前 <see cref="AWSS3UploaderSettings.S3Profile.ReleaseUploadVersionSegment"/> 對應的目錄（任一有物件即視為已佔用）。
    /// 若未設定版本段則略過檢查（允許上傳）。
    /// </summary>
    /// <param name="checkWebGlPrefix">是否檢查 WebGL 上傳前綴（<see cref="AWSS3UploaderSettings.S3Profile.GetWebGlUploadKeyPrefix"/>）</param>
    /// <param name="checkAssetBundlePrefix">是否檢查 AssetBundle 完整前綴（<see cref="AWSS3UploaderSettings.S3Profile.GetAssetBundleS3FullPrefix"/>）</param>
    /// <param name="skipLocalPathRequirements">為 true 時不要求本地 WebGL 輸出目錄／Bundle 目錄已存在（僅依 S3 Key 與憑證做佔用檢查，供構建前預檢）。</param>
    public static async UniTask<ReleaseVersionPathPrecheckResult> PrecheckReleaseVersionPathNotOccupiedOnS3Async(
        AWSS3UploaderSettings.S3Profile profile,
        bool checkWebGlPrefix,
        bool checkAssetBundlePrefix,
        System.Action<string> onLog = null,
        CancellationToken cancellationToken = default,
        bool skipLocalPathRequirements = false)
    {
        string seg = NormalizeReleaseUploadVersionSegment(profile?.ReleaseUploadVersionSegment);
        if (string.IsNullOrEmpty(seg))
        {
            return new ReleaseVersionPathPrecheckResult { IsAllowed = true, Message = "" };
        }

        if (!IsS3SettingsValid(profile))
        {
            return new ReleaseVersionPathPrecheckResult
            {
                IsAllowed = false,
                Message = "無法驗證 S3：請先設定 AWS 憑證與儲存桶名稱"
            };
        }

        try
        {
            using (var s3Client = CreateS3Client(profile))
            {
                bool webGlOk = checkWebGlPrefix && (skipLocalPathRequirements
                    ? PrecheckCanListWebGlPrefixWithoutLocalOutput(profile)
                    : IsGameUploadSettingsValid(profile));

                if (webGlOk)
                {
                    string p = profile.GetWebGlUploadKeyPrefix();
                    if (await S3PrefixHasAnyObjectAsync(s3Client, profile.S3BucketName, p, cancellationToken))
                    {
                        string msg =
                            $"S3 上已存在當前發布版本目錄（v-{seg}），已取消上傳以避免覆蓋。\n\n" +
                            $"已有物件前綴: s3://{profile.S3BucketName}/{p}";
                        onLog?.Invoke($"❌ {msg}");
                        return new ReleaseVersionPathPrecheckResult { IsAllowed = false, Message = msg };
                    }
                }

                bool bundleOk = checkAssetBundlePrefix && profile.UploadAssetBundle && (skipLocalPathRequirements
                    ? PrecheckCanListAssetBundlePrefixWithoutLocalBundleDir(profile)
                    : IsAssetBundleSettingsValid(profile));

                if (bundleOk)
                {
                    string p = profile.GetAssetBundleS3FullPrefix();
                    if (await S3PrefixHasAnyObjectAsync(s3Client, profile.S3BucketName, p, cancellationToken))
                    {
                        string msg =
                            $"S3 上已存在當前發布版本目錄（v-{seg}），已取消上傳以避免覆蓋。\n\n" +
                            $"已有物件前綴: s3://{profile.S3BucketName}/{p}";
                        onLog?.Invoke($"❌ {msg}");
                        return new ReleaseVersionPathPrecheckResult { IsAllowed = false, Message = msg };
                    }
                }
            }

            onLog?.Invoke($"✅ 版本目錄 v-{seg} 在 S3 上尚無內容，可繼續上傳");
            return new ReleaseVersionPathPrecheckResult { IsAllowed = true, Message = "" };
        }
        catch (OperationCanceledException)
        {
            return new ReleaseVersionPathPrecheckResult { IsAllowed = false, Message = "版本目錄檢查已取消" };
        }
        catch (Exception ex)
        {
            string msg = $"檢查 S3 版本目錄失敗: {ex.Message}";
            onLog?.Invoke($"❌ {msg}");
            return new ReleaseVersionPathPrecheckResult { IsAllowed = false, Message = msg };
        }
    }

    private static string NormalizeReleaseUploadVersionSegment(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        return s.Trim().Trim('/', '\\').Replace('\\', '_').Replace('/', '_');
    }

    /// <summary>構建前預檢：無本地 WebGL 產物時仍可依桶與 Key 規則列出前綴。</summary>
    private static bool PrecheckCanListWebGlPrefixWithoutLocalOutput(AWSS3UploaderSettings.S3Profile profile)
    {
        if (!IsS3SettingsValid(profile))
            return false;
        if (string.IsNullOrEmpty(profile.GetWebGlUploadKeyPrefix()))
            return false;
        if (!profile.UploadToRootDirectory && string.IsNullOrEmpty(profile.GetUploadProjectName()))
            return false;
        return true;
    }

    /// <summary>構建前預檢：不要求 Bundle 本機目錄已存在。</summary>
    private static bool PrecheckCanListAssetBundlePrefixWithoutLocalBundleDir(AWSS3UploaderSettings.S3Profile profile)
    {
        if (!IsS3SettingsValid(profile))
            return false;
        if (string.IsNullOrEmpty(profile.GetUploadProjectName()))
            return false;
        return !string.IsNullOrEmpty(profile.GetAssetBundleS3FullPrefix());
    }


    private static async UniTask<bool> S3PrefixHasAnyObjectAsync(
        AmazonS3Client s3Client,
        string bucketName,
        string prefix,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(prefix))
            return false;

        var listRequest = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix,
            MaxKeys = 1
        };

        var listResponse = await s3Client.ListObjectsV2Async(listRequest, cancellationToken);
        return listResponse.S3Objects != null && listResponse.S3Objects.Count > 0;
    }

    public static async UniTask<bool> TestAWSConnectionAsync(AWSS3UploaderSettings.S3Profile profile, System.Action<string> onLog = null)
    {
        if (!IsS3SettingsValid(profile))
        {
            onLog?.Invoke("❌ 請先設定 AWS 憑證與 S3 儲存桶名稱");
            onLog?.Invoke("📋 測試連線需要的設定:");
            onLog?.Invoke("  - AWS 區域");
            onLog?.Invoke("  - Access Key ID");
            onLog?.Invoke("  - Secret Access Key");
            onLog?.Invoke("  - S3 儲存桶名稱");
            onLog?.Invoke("  - WebGL 上傳前綴 (games/...)");
            return false;
        }

        using (var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(CONNECTION_TEST_TIMEOUT_SECONDS)))
        {
            try
            {
                using (var s3Client = CreateS3Client(profile))
                {
                    onLog?.Invoke("開始測試 AWS 連線...");
                    string webGlPrefix = profile.GetWebGlUploadKeyPrefix();
                    onLog?.Invoke($"🔍 測試讀取路徑 's3://{profile.S3BucketName}/{webGlPrefix}'");

                    var listRequest = new ListObjectsV2Request
                    {
                        BucketName = profile.S3BucketName,
                        Prefix = webGlPrefix,
                        MaxKeys = 5
                    };

                    var listResponse = await s3Client.ListObjectsV2Async(listRequest, testCts.Token);

                    onLog?.Invoke("✅ AWS 連線成功！");
                    onLog?.Invoke($"✅ 具有讀取指定路徑的權限");

                    if (listResponse.S3Objects.Count > 0)
                    {
                        onLog?.Invoke($"📁 路徑下找到 {listResponse.S3Objects.Count} 個檔案（顯示前幾個）:");
                        int showCount = Math.Min(3, listResponse.S3Objects.Count);
                        for (int i = 0; i < showCount; i++)
                        {
                            var obj = listResponse.S3Objects[i];
                            string relativePath = obj.Key;
                            if (!string.IsNullOrEmpty(webGlPrefix) && obj.Key.StartsWith(webGlPrefix))
                            {
                                relativePath = obj.Key.Substring(webGlPrefix.Length);
                                if (relativePath.StartsWith("/"))
                                    relativePath = relativePath.Substring(1);
                            }

                            string sizeStr = FormatFileSize(obj.Size);
                            onLog?.Invoke($"  📄 {FormatFilePathForLog(relativePath)} ({sizeStr})");
                        }

                        if (listResponse.S3Objects.Count > showCount)
                        {
                            onLog?.Invoke($"  ... 還有 {listResponse.S3Objects.Count - showCount} 個檔案");
                        }
                    }
                    else
                    {
                        onLog?.Invoke("📁 指定路徑下沒有檔案（路徑可能是空的，這也是正常的）");
                    }

                    onLog?.Invoke("🎉 基本功能測試成功！可以進行檔案列表獲取和上傳操作");
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                onLog?.Invoke("❌ 連線測試超時或被取消");
                return false;
            }
            catch (AmazonS3Exception s3Ex)
            {
                onLog?.Invoke($"❌ S3 錯誤: {s3Ex.ErrorCode} - {s3Ex.Message}");
                HandleS3Exception(s3Ex, profile, onLog);
                return false;
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"❌ 連線測試失敗: {ex.Message}");
                HandleGeneralException(ex, profile, onLog);
                return false;
            }
        }
    }

    /// <summary>
    /// 獲取S3檔案列表
    /// </summary>
    /// <param name="profile">S3設定檔</param>
    /// <param name="onLog">日誌回調函數</param>
    /// <returns>S3檔案列表</returns>
    public static async UniTask<List<S3Object>> GetS3FileListAsync(AWSS3UploaderSettings.S3Profile profile, System.Action<string> onLog = null)
    {
        var fileList = new List<S3Object>();

        if (!IsS3SettingsValid(profile))
        {
            onLog?.Invoke("❌ 請先設定 AWS 憑證與 S3 儲存桶名稱");
            return fileList;
        }

        using (var listCts = new CancellationTokenSource(TimeSpan.FromSeconds(FILE_LIST_TIMEOUT_SECONDS)))
        {
            try
            {
                using (var s3Client = CreateS3Client(profile))
                {
                    onLog?.Invoke("開始獲取 S3 檔案列表...");

                    string listWebGlPrefix = profile.GetWebGlUploadKeyPrefix();
                    var listRequest = new ListObjectsV2Request
                    {
                        BucketName = profile.S3BucketName,
                        Prefix = listWebGlPrefix,
                        MaxKeys = MAX_FILE_LIST_DISPLAY_COUNT
                    };

                    var listResponse = await s3Client.ListObjectsV2Async(listRequest, listCts.Token);

                    if (listResponse.S3Objects.Count == 0)
                    {
                        onLog?.Invoke($"📁 S3 路徑 '{profile.S3BucketName}/{listWebGlPrefix}' 下沒有檔案");
                        return fileList;
                    }

                    fileList.AddRange(listResponse.S3Objects);

                    onLog?.Invoke($"📋 找到 {listResponse.S3Objects.Count} 個檔案");
                    onLog?.Invoke("───────────────────────────────────────");

                    var sortedObjects = listResponse.S3Objects.OrderBy(x => x.Key).ToList();

                    foreach (var obj in sortedObjects)
                    {
                        string relativePath = obj.Key;
                        if (!string.IsNullOrEmpty(listWebGlPrefix) && obj.Key.StartsWith(listWebGlPrefix))
                        {
                            relativePath = obj.Key.Substring(listWebGlPrefix.Length);
                            if (relativePath.StartsWith("/"))
                                relativePath = relativePath.Substring(1);
                        }

                        string sizeStr = FormatFileSize(obj.Size);
                        string timeStr = obj.LastModified.ToString("yyyy-MM-dd HH:mm:ss");

                        onLog?.Invoke($"📄 {FormatFilePathForLog(relativePath)} ({sizeStr}) - {timeStr}");
                    }

                    onLog?.Invoke("───────────────────────────────────────");
                    onLog?.Invoke($"✅ 檔案列表獲取完成，共 {listResponse.S3Objects.Count} 個檔案");

                    if (listResponse.IsTruncated)
                    {
                        onLog?.Invoke($"⚠️ 注意：由於檔案數量過多，僅顯示前 {MAX_FILE_LIST_DISPLAY_COUNT} 個檔案");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                onLog?.Invoke("❌ 獲取檔案列表超時或被取消");
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"❌ 獲取 S3 檔案列表失敗: {ex.Message}");
                HandleS3ListException(ex, profile, onLog);
            }
        }

        return fileList;
    }

    /// <summary>
    /// 上傳主遊戲目錄到S3
    /// </summary>
    /// <param name="profile">S3設定檔</param>
    /// <param name="onLog">日誌回調函數</param>
    /// <param name="onProgress">進度回調函數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上傳結果</returns>
    public static async UniTask<UploadResult> UploadGameDirectoryAsync(
        AWSS3UploaderSettings.S3Profile profile,
        System.Action<string> onLog = null,
        System.Action<UploadProgress> onProgress = null,
        CancellationToken cancellationToken = default,
        bool skipReleaseVersionPrecheck = false)
    {
        var result = new UploadResult { IsSuccess = false };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!IsGameUploadSettingsValid(profile))
            {
                result.Message = "設定無效：請檢查主遊戲目錄和S3設定";
                onLog?.Invoke($"❌ {result.Message}");
                return result;
            }

            if (!skipReleaseVersionPrecheck)
            {
                var pre = await PrecheckReleaseVersionPathNotOccupiedOnS3Async(profile, true, false, onLog, cancellationToken);
                if (!pre.IsAllowed)
                {
                    result.Message = string.IsNullOrEmpty(pre.Message)
                        ? "版本目錄檢查未通過，已取消上傳"
                        : pre.Message;
                    return result;
                }
            }

            onLog?.Invoke("🎮 開始上傳主遊戲目錄...");

            using (var s3Client = CreateS3Client(profile))
            {
                // 清除S3目錄
                await ClearS3DirectoryAsync(s3Client, profile, onLog, cancellationToken);

                // 上傳主遊戲檔案
                result = await UploadDirectoryParallelAsync(s3Client, profile, onLog, onProgress, cancellationToken);
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (result.IsSuccess)
            {
                onLog?.Invoke($"✅ 主遊戲上傳完成！總耗時: {FormatDuration(result.Duration)}");
            }
        }
        catch (OperationCanceledException)
        {
            result.Message = "上傳已被取消";
            onLog?.Invoke($"❌ {result.Message}");
        }
        catch (Exception ex)
        {
            result.Exception = ex;
            result.Message = $"上傳失敗: {ex.Message}";
            onLog?.Invoke($"❌ {result.Message}");
        }

        return result;
    }

    /// <summary>
    /// 上傳AssetBundle目錄到S3
    /// </summary>
    /// <param name="profile">S3設定檔</param>
    /// <param name="onLog">日誌回調函數</param>
    /// <param name="onProgress">進度回調函數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上傳結果</returns>
    public static async UniTask<UploadResult> UploadAssetBundleDirectoryAsync(
        AWSS3UploaderSettings.S3Profile profile,
        System.Action<string> onLog = null,
        System.Action<UploadProgress> onProgress = null,
        CancellationToken cancellationToken = default,
        bool skipReleaseVersionPrecheck = false)
    {
        var result = new UploadResult { IsSuccess = false };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!IsAssetBundleSettingsValid(profile))
            {
                result.Message = "設定無效：請檢查AssetBundle目錄和S3設定";
                onLog?.Invoke($"❌ {result.Message}");
                return result;
            }

            if (!skipReleaseVersionPrecheck)
            {
                var preAb = await PrecheckReleaseVersionPathNotOccupiedOnS3Async(profile, false, true, onLog, cancellationToken);
                if (!preAb.IsAllowed)
                {
                    result.Message = string.IsNullOrEmpty(preAb.Message)
                        ? "版本目錄檢查未通過，已取消上傳"
                        : preAb.Message;
                    return result;
                }
            }

            onLog?.Invoke("🎯 開始上傳AssetBundle目錄...");

            using (var s3Client = CreateS3Client(profile))
            {
                result = await UploadAssetBundleDirectoryParallelAsync(s3Client, profile, onLog, onProgress, cancellationToken);
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (result.IsSuccess)
            {
                onLog?.Invoke($"✅ AssetBundle上傳完成！總耗時: {FormatDuration(result.Duration)}");
            }
        }
        catch (OperationCanceledException)
        {
            result.Message = "AssetBundle上傳已被取消";
            onLog?.Invoke($"❌ {result.Message}");
        }
        catch (Exception ex)
        {
            result.Exception = ex;
            result.Message = $"AssetBundle上傳失敗: {ex.Message}";
            onLog?.Invoke($"❌ {result.Message}");
        }

        return result;
    }

    /// <summary>
    /// 完整的上傳流程（主遊戲 + AssetBundle）
    /// </summary>
    /// <param name="profile">S3設定檔</param>
    /// <param name="onLog">日誌回調函數</param>
    /// <param name="onProgress">進度回調函數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="skipInitialReleaseVersionPrecheck">為 true 時跳過開頭的版本目錄 S3 檢查（例如已在構建開始前預檢過）。</param>
    /// <returns>上傳結果</returns>
    public static async UniTask<UploadResult> FullUploadAsync(
        AWSS3UploaderSettings.S3Profile profile,
        System.Action<string> onLog = null,
        System.Action<UploadProgress> onProgress = null,
        CancellationToken cancellationToken = default,
        bool skipInitialReleaseVersionPrecheck = false)
    {
        var result = new UploadResult { IsSuccess = false };
        var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            onLog?.Invoke("🚀 開始完整上傳流程...");

            if (!skipInitialReleaseVersionPrecheck)
            {
                bool willUploadGame = IsGameUploadSettingsValid(profile);
                bool willUploadAb = profile.UploadAssetBundle && IsAssetBundleSettingsValid(profile);
                var preFull = await PrecheckReleaseVersionPathNotOccupiedOnS3Async(profile, willUploadGame, willUploadAb, onLog, cancellationToken);
                if (!preFull.IsAllowed)
                {
                    result.Message = string.IsNullOrEmpty(preFull.Message)
                        ? "版本目錄檢查未通過，已取消上傳"
                        : preFull.Message;
                    onLog?.Invoke($"❌ {result.Message}");
                    overallStopwatch.Stop();
                    result.Duration = overallStopwatch.Elapsed;
                    return result;
                }
            }

            // 上傳主遊戲
            if (IsGameUploadSettingsValid(profile))
            {
                var gameResult = await UploadGameDirectoryAsync(profile, onLog, onProgress, cancellationToken,
                    skipReleaseVersionPrecheck: true);
                if (!gameResult.IsSuccess)
                {
                    return gameResult;
                }
                result.TotalFilesUploaded += gameResult.TotalFilesUploaded;
            }

            // 上傳AssetBundle（如果啟用且設定有效）
            if (profile.UploadAssetBundle && IsAssetBundleSettingsValid(profile))
            {
                var bundleResult = await UploadAssetBundleDirectoryAsync(profile, onLog, onProgress, cancellationToken,
                    skipReleaseVersionPrecheck: true);
                if (!bundleResult.IsSuccess)
                {
                    return bundleResult;
                }
                result.TotalFilesUploaded += bundleResult.TotalFilesUploaded;
            }

            overallStopwatch.Stop();
            result.Duration = overallStopwatch.Elapsed;
            result.IsSuccess = true;
            result.Message = "完整上傳流程成功完成";

            onLog?.Invoke($"🎉 完整上傳流程完成！總耗時: {FormatDuration(result.Duration)}");

            await InvalidateCloudFrontAfterUploadAsync(profile, onLog, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result.Message = "完整上傳流程已被取消";
            onLog?.Invoke($"❌ {result.Message}");
        }
        catch (Exception ex)
        {
            result.Exception = ex;
            result.Message = $"完整上傳流程失敗: {ex.Message}";
            onLog?.Invoke($"❌ {result.Message}");
        }

        return result;
    }

    /// <summary>
    /// 上傳完成後執行 CloudFront Invalidation（與 <see cref="AWSS3UploaderWindow"/> 手動上傳行為一致）。
    /// 內部吞掉例外，不影響上傳成功狀態。
    /// </summary>
    public static async UniTask InvalidateCloudFrontAfterUploadAsync(
        AWSS3UploaderSettings.S3Profile profile,
        Action<string> onLog,
        CancellationToken cancellationToken = default)
    {
        if (profile == null)
            return;

        if (string.IsNullOrEmpty(profile.CloudFrontDistributionId))
        {
            onLog?.Invoke("⚠️ 未设置 CloudFront Distribution ID，跳过 Invalidation");
            return;
        }

        string pathPrefix = profile.GetWebGlViewerPathPrefixForCloudFront();
        if (string.IsNullOrEmpty(pathPrefix))
        {
            onLog?.Invoke("⚠️ 無法取得專案名稱（與 S3 上傳路徑一致），跳过 CloudFront Invalidation");
            return;
        }

        onLog?.Invoke("🌐 开始执行 CloudFront Cache Invalidation...");

        try
        {
            // CloudFront 控制平面 API 必須指定區域；AWS 要求使用 us-east-1（與 S3 桶區域、profile.AwsRegion 無關）。
            // 兩參數構造函數不會設置 RegionEndpoint，在較新 AWSSDK 會拋「No RegionEndpoint or ServiceURL configured」。
            var cfConfig = new AmazonCloudFrontConfig
            {
                RegionEndpoint = RegionEndpoint.USEast1
            };
            var cloudFrontClient = new AmazonCloudFrontClient(
                profile.AccessKeyId,
                profile.SecretAccessKey,
                cfConfig);

            var invalidationBatch = new InvalidationBatch
            {
                Paths = new Paths
                {
                    Quantity = 4,
                    Items = new List<string>
                    {
                        $"/{pathPrefix}/index.html",
                        $"/{pathPrefix}/StreamingAssets/aa/catalog*.json",
                        $"/{pathPrefix}/StreamingAssets/aa/catalog*.hash",
                        $"/{pathPrefix}/StreamingAssets/aa/settings.json"
                    }
                },
                CallerReference = $"UnityUpload_{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            };

            var request = new CreateInvalidationRequest
            {
                DistributionId = profile.CloudFrontDistributionId,
                InvalidationBatch = invalidationBatch
            };

            var response = await cloudFrontClient.CreateInvalidationAsync(request, cancellationToken);

            onLog?.Invoke($"✅ CloudFront Invalidation 提交成功！ID: {response.Invalidation.Id}");
            onLog?.Invoke("   Status: InProgress （正在全球边缘节点刷新缓存，请等待几分钟）");
        }
        catch (Exception ex)
        {
            // 上传延续可能在线程池上执行，避免在此调用 Debug.LogError（与主线程相关的 Unity API 行为因版本而异）。
            onLog?.Invoke($"❌ CloudFront Invalidation 失败: {ex.Message}");
            onLog?.Invoke(ex.StackTrace);
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// 創建S3客戶端
    /// </summary>
    private static AmazonS3Client CreateS3Client(AWSS3UploaderSettings.S3Profile profile)
    {
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(profile.AwsRegion),
            UseHttp = false,
            MaxErrorRetry = AWS_MAX_RETRY_COUNT,
            Timeout = TimeSpan.FromSeconds(AWS_S3_UPLOAD_REQUEST_TIMEOUT_SECONDS),
            ReadWriteTimeout = TimeSpan.FromSeconds(AWS_S3_UPLOAD_READWRITE_TIMEOUT_SECONDS),
            ForcePathStyle = true,
            UseDualstackEndpoint = false
        };

        AWSCredentials credentials = new BasicAWSCredentials(profile.AccessKeyId, profile.SecretAccessKey);
        return new AmazonS3Client(credentials, config);
    }

    private static int UploadPutAppMaxAttempts => 1 + UPLOAD_PUT_APP_EXTRA_RETRIES;

    private static bool IsRetryableAmazonS3PutException(AmazonS3Exception s3)
    {
        int code = (int)s3.StatusCode;
        if (code == 408 || code == 429)
            return true;
        if (code >= 500 && code < 600)
            return true;

        string ec = s3.ErrorCode ?? "";
        if (ec.Equals("SlowDown", StringComparison.OrdinalIgnoreCase)) return true;
        if (ec.Equals("Throttling", StringComparison.OrdinalIgnoreCase)) return true;
        if (ec.Equals("RequestTimeout", StringComparison.OrdinalIgnoreCase)) return true;
        if (ec.Equals("InternalError", StringComparison.OrdinalIgnoreCase)) return true;
        if (ec.Equals("ServiceUnavailable", StringComparison.OrdinalIgnoreCase)) return true;
        if (ec.Equals("IncompleteBody", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// 應用層整次 Put 重試條件。注意：HttpClient 逾時常以 <see cref="TaskCanceledException"/> 呈現，
    /// 若一併當成「使用者取消」則弱網永遠不會重試。
    /// </summary>
    private static bool IsPutFailureEligibleForAppRetry(Exception ex, CancellationToken cancellationToken)
    {
        if (ex is AggregateException agg)
        {
            foreach (Exception inner in agg.Flatten().InnerExceptions)
            {
                if (IsPutFailureEligibleForAppRetry(inner, cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        if (ex is OperationCanceledException)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        for (Exception e = ex; e != null; e = e.InnerException)
        {
            if (e is OperationCanceledException)
            {
                return !cancellationToken.IsCancellationRequested;
            }

            if (e is AmazonS3Exception s3)
            {
                return IsRetryableAmazonS3PutException(s3);
            }

            if (e is AmazonServiceException ase)
            {
                int sc = (int)ase.StatusCode;
                if (sc == 408 || sc == 429)
                {
                    return true;
                }

                if (sc >= 500 && sc < 600)
                {
                    return true;
                }

                if (sc == 0)
                {
                    return true;
                }
            }

            if (e is IOException)
            {
                return true;
            }

            if (e is SocketException)
            {
                return true;
            }

            if (e is WebException)
            {
                return true;
            }

            if (e is HttpRequestException)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetPutRetryDelayMs(int failedAttemptIndex)
    {
        int exp = Math.Min(failedAttemptIndex, 8);
        int delay = UPLOAD_PUT_RETRY_BASE_DELAY_MS * (1 << exp);
        return Math.Min(delay, UPLOAD_PUT_RETRY_MAX_DELAY_MS);
    }

    /// <summary>
    /// 在 SDK 重試之外再做整次 PutObject 級別重試（適合連線中斷、讀取逾時等）。
    /// </summary>
    private static async UniTask PutObjectWithApplicationRetryAsync(
        AmazonS3Client s3Client,
        PutObjectRequest request,
        string logPathLabel,
        System.Action<string> onLog,
        CancellationToken cancellationToken)
    {
        int maxAttempts = UploadPutAppMaxAttempts;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await s3Client.PutObjectAsync(request, cancellationToken);
                if (attempt > 1)
                    onLog?.Invoke($"✅ 上傳重試成功 ({attempt}/{maxAttempts}): {logPathLabel}");
                return;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested && ex is OperationCanceledException)
                {
                    throw;
                }

                bool canRetry = attempt < maxAttempts && IsPutFailureEligibleForAppRetry(ex, cancellationToken);
                if (!canRetry)
                {
                    throw;
                }

                int delayMs = GetPutRetryDelayMs(attempt - 1);
                onLog?.Invoke($"⚠️ 上傳失敗，{delayMs}ms 後重試 ({attempt}/{maxAttempts}): {logPathLabel} — {ex.Message}");
                await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
            }
        }
    }

    /// <summary>
    /// 清除S3目錄
    /// </summary>
    private static async UniTask ClearS3DirectoryAsync(
        AmazonS3Client s3Client,
        AWSS3UploaderSettings.S3Profile profile,
        System.Action<string> onLog,
        CancellationToken cancellationToken)
    {
        onLog?.Invoke("🗑️ 正在清理 S3 目錄...");

        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = profile.S3BucketName,
                Prefix = profile.GetWebGlUploadKeyPrefix()
            };

            var objects = new List<S3Object>();
            ListObjectsV2Response response;
            int pageIndex = 0;

            do
            {
                pageIndex++;
                response = await s3Client.ListObjectsV2Async(listRequest, cancellationToken);
                objects.AddRange(response.S3Objects);
                onLog?.Invoke(
                    $"📄 清理扫描中：第 {pageIndex} 页，本页 {response.S3Objects.Count} 个，累计 {objects.Count} 个...");
                listRequest.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);

            if (objects.Count == 0)
            {
                onLog?.Invoke("✅ 目錄已經是空的，無需清理");
                return;
            }

            var objectsToDelete = FilterObjectsForDeletion(objects, profile);

            if (objectsToDelete.Count == 0)
            {
                onLog?.Invoke("✅ 所有檔案都在排除清單中，無需清理");
                return;
            }

            onLog?.Invoke($"📊 找到 {objects.Count} 個物件，其中 {objectsToDelete.Count} 個需要刪除");

            const int batchSize = 1000;
            int totalBatches = (objectsToDelete.Count + batchSize - 1) / batchSize;
            for (int i = 0; i < objectsToDelete.Count; i += batchSize)
            {
                var batchObjects = objectsToDelete.Skip(i).Take(batchSize).ToList();
                int currentBatch = (i / batchSize) + 1;
                onLog?.Invoke(
                    $"🧹 清理删除中：批次 {currentBatch}/{totalBatches}，本批 {batchObjects.Count} 个...");
                await DeleteObjectsBatchAsync(s3Client, profile.S3BucketName, batchObjects, onLog, cancellationToken);
            }

            onLog?.Invoke("✅ S3 目錄清理完成");
        }
        catch (OperationCanceledException)
        {
            onLog?.Invoke("❌ 清理 S3 目錄操作已被取消");
            throw;
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"❌ 清理 S3 目錄時發生錯誤: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 篩選需要刪除的物件
    /// </summary>
    private static List<S3Object> FilterObjectsForDeletion(List<S3Object> allObjects, AWSS3UploaderSettings.S3Profile profile)
    {
        if (!profile.ExcludeBundleSourceFromClear)
        {
            return allObjects;
        }

        string excludedPath = profile.GetAssetBundleS3FullPrefix();

        var objectsToDelete = new List<S3Object>();

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
    /// 分批刪除S3物件
    /// </summary>
    private static async UniTask DeleteObjectsBatchAsync(
        AmazonS3Client s3Client,
        string bucketName,
        List<S3Object> objects,
        System.Action<string> onLog,
        CancellationToken cancellationToken)
    {
        var deleteRequest = new DeleteObjectsRequest
        {
            BucketName = bucketName,
            Objects = objects.Select(obj => new KeyVersion { Key = obj.Key }).ToList()
        };

        try
        {
            var deleteResponse = await s3Client.DeleteObjectsAsync(deleteRequest, cancellationToken);

            if (deleteResponse.DeletedObjects.Count > 0)
            {
                onLog?.Invoke($"🗑️ 已刪除 {deleteResponse.DeletedObjects.Count} 個物件");
            }

            if (deleteResponse.DeleteErrors.Count > 0)
            {
                foreach (var error in deleteResponse.DeleteErrors)
                {
                    onLog?.Invoke($"❌ 刪除失敗: {error.Key} - {error.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            onLog?.Invoke("❌ 刪除物件操作已被取消");
            throw;
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"❌ 刪除物件時發生錯誤: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 從檔案路徑列表中提取所有需要建立的資料夾路徑
    /// </summary>
    /// <param name="s3Keys">所有檔案的 S3 Key 列表</param>
    /// <returns>需要建立的資料夾路徑集合（去重且按層級排序）</returns>
    private static HashSet<string> ExtractFolderPaths(IEnumerable<string> s3Keys)
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
    /// <param name="s3Client">S3 客戶端</param>
    /// <param name="bucketName">儲存桶名稱</param>
    /// <param name="folderPaths">要建立的資料夾路徑列表</param>
    /// <param name="onLog">日誌回調函數</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async UniTask EnsureFoldersExistAsync(
        AmazonS3Client s3Client,
        string bucketName,
        IEnumerable<string> folderPaths,
        System.Action<string> onLog,
        CancellationToken cancellationToken)
    {
        var sortedFolders = folderPaths.OrderBy(f => f.Count(c => c == '/')).ThenBy(f => f).ToList();

        if (sortedFolders.Count == 0)
        {
            return;
        }

        onLog?.Invoke($"📁 開始建立 {sortedFolders.Count} 個資料夾結構...");

        // 先檢查哪些資料夾已經存在
        var existingFolders = new HashSet<string>();
        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
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
                response = await s3Client.ListObjectsV2Async(listRequest, cancellationToken);
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
            onLog?.Invoke($"⚠️ 檢查現有資料夾時發生錯誤: {ex.Message}，將嘗試建立所有資料夾");
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
                    BucketName = bucketName,
                    Key = folderPath,
                    ContentBody = ""
                };

                await s3Client.PutObjectAsync(request, cancellationToken);
                createdCount++;
                onLog?.Invoke($"📁 已建立資料夾: {folderPath}");
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"⚠️ 建立資料夾失敗: {folderPath} - {ex.Message}");
            }
        }

        if (createdCount > 0 || skippedCount > 0)
        {
            onLog?.Invoke($"✅ 資料夾結構建立完成 - 新建立: {createdCount} 個，已存在: {skippedCount} 個");
        }
    }

    /// <summary>
    /// 並行上傳目錄
    /// </summary>
    private static async UniTask<UploadResult> UploadDirectoryParallelAsync(
        AmazonS3Client s3Client,
        AWSS3UploaderSettings.S3Profile profile,
        System.Action<string> onLog,
        System.Action<UploadProgress> onProgress,
        CancellationToken cancellationToken)
    {
        var result = new UploadResult { IsSuccess = false };
        var gameStopwatch = System.Diagnostics.Stopwatch.StartNew();

        string[] files = Directory.GetFiles(profile.LocalDirectoryPath, "*", SearchOption.AllDirectories);
        int totalFileCount = files.Length;
        int completedFileCount = 0;
        var activeUploads = new List<string>();
        var progressLock = new object();

        onLog?.Invoke($"開始並行上傳 {totalFileCount} 個檔案 (最多同時上傳 {profile.MaxConcurrentUploads} 個)");
        onLog?.Invoke($"ℹ️ 單檔遇可重試錯誤時將最多嘗試 {UploadPutAppMaxAttempts} 次（含首次；與 SDK 內建重試疊加）");

        var sortedFiles = SortFilesBySize(files, onLog);
        onLog?.Invoke($"✅ 檔案已按大小排序 - 小檔案優先上傳");

        // 收集所有 S3 Key 並建立資料夾結構
        string gamePrefix = profile.GetWebGlUploadKeyPrefix();
        var s3Keys = sortedFiles.Select(f =>
        {
            string relativePath = Path.GetRelativePath(profile.LocalDirectoryPath, f.FilePath);
            return gamePrefix + "/" + relativePath.Replace('\\', '/');
        }).ToList();

        var folderPaths = ExtractFolderPaths(s3Keys);
        if (folderPaths.Count > 0)
        {
            await EnsureFoldersExistAsync(s3Client, profile.S3BucketName, folderPaths, onLog, cancellationToken);
        }

        int failedFileCount = 0;
        using (var uploadSemaphore = new SemaphoreSlim(profile.MaxConcurrentUploads, profile.MaxConcurrentUploads))
        {
            var uploadTasks = new List<UniTask<bool>>();

            foreach (var fileInfo in sortedFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string relativePath = Path.GetRelativePath(profile.LocalDirectoryPath, fileInfo.FilePath);
                string s3Key = gamePrefix + "/" + relativePath.Replace('\\', '/');

                var uploadTask = UploadFileParallelAsync(
                    s3Client, fileInfo, s3Key, relativePath, profile.S3BucketName, uploadSemaphore,
                    () =>
                    {
                        lock (progressLock)
                        {
                            completedFileCount++;
                            var progress = new UploadProgress
                            {
                                CompletedFiles = completedFileCount,
                                TotalFiles = totalFileCount,
                                ProgressPercentage = (float)completedFileCount / totalFileCount * 100,
                                StatusMessage = $"上傳進度: {completedFileCount}/{totalFileCount}",
                                ActiveUploads = new List<string>(activeUploads)
                            };
                            onProgress?.Invoke(progress);
                        }
                    },
                    (fileName, isStarting) =>
                    {
                        lock (progressLock)
                        {
                            if (isStarting)
                                activeUploads.Add(fileName);
                            else
                                activeUploads.Remove(fileName);
                        }
                    },
                    onLog, cancellationToken);

                uploadTasks.Add(uploadTask);
            }

            if (uploadTasks.Count > 0)
            {
                var outcomes = await UniTask.WhenAll(uploadTasks);
                foreach (bool ok in outcomes)
                {
                    if (!ok) failedFileCount++;
                }
            }
        }

        gameStopwatch.Stop();

        if (cancellationToken.IsCancellationRequested)
        {
            result.Message = $"主遊戲作業已取消，總耗時: {FormatDuration(gameStopwatch.Elapsed)}";
        }
        else if (failedFileCount > 0)
        {
            result.IsSuccess = false;
            result.TotalFilesUploaded = completedFileCount;
            result.Message =
                $"主遊戲上傳結束：{failedFileCount} 個檔案失敗（已重試），成功 {completedFileCount} 個，總耗時: {FormatDuration(gameStopwatch.Elapsed)}";
        }
        else
        {
            result.IsSuccess = true;
            result.TotalFilesUploaded = completedFileCount;
            result.Message = $"主遊戲作業完成，總耗時: {FormatDuration(gameStopwatch.Elapsed)}";
        }

        onLog?.Invoke($"⏱️ {result.Message}");
        return result;
    }

    /// <summary>
    /// 並行上傳AssetBundle目錄
    /// </summary>
    private static async UniTask<UploadResult> UploadAssetBundleDirectoryParallelAsync(
        AmazonS3Client s3Client,
        AWSS3UploaderSettings.S3Profile profile,
        System.Action<string> onLog,
        System.Action<UploadProgress> onProgress,
        CancellationToken cancellationToken)
    {
        var result = new UploadResult { IsSuccess = false };
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

        string[] files = Directory.GetFiles(profile.GetAssetBundleDirectoryPath(), "*", SearchOption.AllDirectories);
        int totalFileCount = files.Length;
        int completedFileCount = 0;
        var activeUploads = new List<string>();
        var progressLock = new object();

        onLog?.Invoke($"開始並行上傳 {totalFileCount} 個 AssetBundle 檔案 (最多同時上傳 {profile.MaxConcurrentUploads} 個)");
        onLog?.Invoke($"ℹ️ 單檔遇可重試錯誤時將最多嘗試 {UploadPutAppMaxAttempts} 次（含首次；與 SDK 內建重試疊加）");

        var sortedFiles = SortFilesBySize(files, onLog);
        onLog?.Invoke($"✅ AssetBundle 檔案已按大小排序 - 小檔案優先上傳");

        // 處理重複檢查
        Dictionary<string, S3FileInfo> s3FileMap = null;
        int skippedCount = 0;

        if (profile.SkipDuplicateBundleUploads)
        {
            onLog?.Invoke($"✅ 重複檢查功能已啟用");
            onLog?.Invoke("🔍 正在檢查S3上現有的AssetBundle檔案...");
            string fullS3Path = profile.GetAssetBundleS3FullPrefix();
            onLog?.Invoke($"🎯 檢查S3路徑: {fullS3Path}");
            s3FileMap = await GetS3FileInfoMapAsync(s3Client, profile.S3BucketName, fullS3Path, cancellationToken);
            onLog?.Invoke($"📊 S3上現有 {s3FileMap.Count} 個AssetBundle檔案");
        }

        // 收集所有 S3 Key 並建立資料夾結構
        string assetBundleFullS3Path = profile.GetAssetBundleS3FullPrefix();
        var assetBundleS3Keys = sortedFiles.Select(f =>
        {
            string relativePath = Path.GetRelativePath(profile.GetAssetBundleDirectoryPath(), f.FilePath);
            return assetBundleFullS3Path + "/" + relativePath.Replace('\\', '/');
        }).ToList();

        var assetBundleFolderPaths = ExtractFolderPaths(assetBundleS3Keys);
        if (assetBundleFolderPaths.Count > 0)
        {
            await EnsureFoldersExistAsync(s3Client, profile.S3BucketName, assetBundleFolderPaths, onLog, cancellationToken);
        }

        int failedBundleFileCount = 0;
        using (var uploadSemaphore = new SemaphoreSlim(profile.MaxConcurrentUploads, profile.MaxConcurrentUploads))
        {
            var uploadTasks = new List<UniTask<bool>>();
            string fullS3Path = profile.GetAssetBundleS3FullPrefix();

            foreach (var fileInfo in sortedFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string relativePath = Path.GetRelativePath(profile.GetAssetBundleDirectoryPath(), fileInfo.FilePath);
                string s3Key = fullS3Path + "/" + relativePath.Replace('\\', '/');

                // 檢查是否需要上傳
                if (s3FileMap != null && !ShouldUploadFile(fileInfo.FilePath, s3Key, s3FileMap, onLog))
                {
                    skippedCount++;
                    lock (progressLock)
                    {
                        completedFileCount++;
                        var progress = new UploadProgress
                        {
                            CompletedFiles = completedFileCount,
                            TotalFiles = totalFileCount,
                            ProgressPercentage = (float)completedFileCount / totalFileCount * 100,
                            StatusMessage = $"AssetBundle 上傳進度: {completedFileCount}/{totalFileCount}",
                            ActiveUploads = new List<string>(activeUploads)
                        };
                        onProgress?.Invoke(progress);
                    }
                    continue;
                }

                var uploadTask = UploadAssetBundleFileParallelAsync(
                    s3Client, fileInfo, s3Key, relativePath, profile.S3BucketName, uploadSemaphore,
                    () =>
                    {
                        lock (progressLock)
                        {
                            completedFileCount++;
                            var progress = new UploadProgress
                            {
                                CompletedFiles = completedFileCount,
                                TotalFiles = totalFileCount,
                                ProgressPercentage = (float)completedFileCount / totalFileCount * 100,
                                StatusMessage = $"AssetBundle 上傳進度: {completedFileCount}/{totalFileCount}",
                                ActiveUploads = new List<string>(activeUploads)
                            };
                            onProgress?.Invoke(progress);
                        }
                    },
                    (fileName, isStarting) =>
                    {
                        lock (progressLock)
                        {
                            if (isStarting)
                                activeUploads.Add(fileName);
                            else
                                activeUploads.Remove(fileName);
                        }
                    },
                    onLog, cancellationToken);

                uploadTasks.Add(uploadTask);
            }

            if (skippedCount > 0)
            {
                onLog?.Invoke($"⚡ 智能跳過：{skippedCount} 個檔案無需重複上傳，節省了上傳流量");
            }

            if (uploadTasks.Count > 0)
            {
                onLog?.Invoke($"🚀 實際需要上傳 {uploadTasks.Count} 個檔案");
                var outcomes = await UniTask.WhenAll(uploadTasks);
                foreach (bool ok in outcomes)
                {
                    if (!ok) failedBundleFileCount++;
                }
            }
            else
            {
                onLog?.Invoke("✅ 所有AssetBundle檔案都是最新版本，無需上傳");
            }
        }

        totalStopwatch.Stop();

        if (cancellationToken.IsCancellationRequested)
        {
            result.Message = $"AssetBundle作業已取消，總耗時: {FormatDuration(totalStopwatch.Elapsed)}";
        }
        else if (failedBundleFileCount > 0)
        {
            result.IsSuccess = false;
            result.TotalFilesUploaded = completedFileCount;
            result.Message =
                $"AssetBundle 上傳結束：{failedBundleFileCount} 個檔案失敗（已重試），其餘已完成，總耗時: {FormatDuration(totalStopwatch.Elapsed)}";
        }
        else
        {
            result.IsSuccess = true;
            result.TotalFilesUploaded = completedFileCount;
            result.Message = $"AssetBundle作業完成，總耗時: {FormatDuration(totalStopwatch.Elapsed)}";
        }

        onLog?.Invoke($"⏱️ {result.Message}");
        return result;
    }

    /// <summary>
    /// 並行上傳單個檔案
    /// </summary>
    private static async UniTask<bool> UploadFileParallelAsync(
        AmazonS3Client s3Client,
        FileInfo fileInfo,
        string s3Key,
        string relativePath,
        string bucketName,
        SemaphoreSlim uploadSemaphore,
        System.Action onCompleted,
        System.Action<string, bool> onActiveUploadChange,
        System.Action<string> onLog,
        CancellationToken cancellationToken)
    {
        await uploadSemaphore.WaitAsync(cancellationToken);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (cancellationToken.IsCancellationRequested) return false;

            string fileName = Path.GetFileName(fileInfo.FilePath);
            onActiveUploadChange?.Invoke(fileName, true);

            onLog?.Invoke($"🔄 開始上傳: {FormatFilePathForLog(relativePath)} ({FormatFileSize(fileInfo.Size)})");
            onLog?.Invoke($"📍 目標路徑: s3://{bucketName}/{s3Key}");

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key,
                FilePath = fileInfo.FilePath
            };

            ConfigureContentTypeAndEncoding(request, fileName);

            if (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                onLog?.Invoke($"❌ 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
                return false;
            }

            await PutObjectWithApplicationRetryAsync(s3Client, request, FormatFilePathForLog(relativePath), onLog,
                cancellationToken);
            stopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                onLog?.Invoke($"❌ 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
                return false;
            }

            onLog?.Invoke($"✅ 上傳完成: {FormatFilePathForLog(relativePath)} ({FormatFileSize(fileInfo.Size)}, 耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
            onLog?.Invoke($"📍 已上傳至: s3://{bucketName}/{s3Key}");
            onCompleted?.Invoke();
            return true;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            onLog?.Invoke($"❌ 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
            return false;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            onLog?.Invoke($"❌ 上傳失敗: {FormatFilePathForLog(relativePath)} - {ex.Message} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
            return false;
        }
        finally
        {
            string fileName = Path.GetFileName(fileInfo.FilePath);
            onActiveUploadChange?.Invoke(fileName, false);
            uploadSemaphore.Release();
        }
    }

    /// <summary>
    /// 並行上傳單個AssetBundle檔案
    /// </summary>
    private static async UniTask<bool> UploadAssetBundleFileParallelAsync(
        AmazonS3Client s3Client,
        FileInfo fileInfo,
        string s3Key,
        string relativePath,
        string bucketName,
        SemaphoreSlim uploadSemaphore,
        System.Action onCompleted,
        System.Action<string, bool> onActiveUploadChange,
        System.Action<string> onLog,
        CancellationToken cancellationToken)
    {
        await uploadSemaphore.WaitAsync(cancellationToken);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (cancellationToken.IsCancellationRequested) return false;

            string fileName = Path.GetFileName(fileInfo.FilePath);
            onActiveUploadChange?.Invoke(fileName, true);

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key,
                FilePath = fileInfo.FilePath
            };

            ConfigureContentTypeAndEncoding(request, fileName);

            if (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                onLog?.Invoke($"❌ AssetBundle 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
                return false;
            }

            await PutObjectWithApplicationRetryAsync(s3Client, request, FormatFilePathForLog(relativePath), onLog,
                cancellationToken);
            stopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                onLog?.Invoke($"❌ AssetBundle 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
                return false;
            }

            onCompleted?.Invoke();
            return true;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            onLog?.Invoke($"❌ AssetBundle 上傳已取消: {FormatFilePathForLog(relativePath)} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
            return false;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            onLog?.Invoke($"❌ AssetBundle 上傳失敗: {FormatFilePathForLog(relativePath)} - {ex.Message} (耗時: {stopwatch.Elapsed.TotalSeconds:F2}s)");
            return false;
        }
        finally
        {
            string fileName = Path.GetFileName(fileInfo.FilePath);
            onActiveUploadChange?.Invoke(fileName, false);
            uploadSemaphore.Release();
        }
    }

    /// <summary>
    /// 獲取S3檔案資訊映射
    /// </summary>
    private static async UniTask<Dictionary<string, S3FileInfo>> GetS3FileInfoMapAsync(
        AmazonS3Client s3Client,
        string bucketName,
        string s3Prefix,
        CancellationToken cancellationToken)
    {
        var fileInfoMap = new Dictionary<string, S3FileInfo>();

        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = s3Prefix
            };

            ListObjectsV2Response response;
            do
            {
                response = await s3Client.ListObjectsV2Async(listRequest, cancellationToken);

                foreach (var obj in response.S3Objects)
                {
                    fileInfoMap[obj.Key] = new S3FileInfo
                    {
                        Size = obj.Size,
                        LastModified = obj.LastModified,
                        ETag = obj.ETag?.Trim('"')
                    };
                }

                listRequest.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"獲取S3檔案資訊時發生錯誤: {ex.Message}");
        }

        return fileInfoMap;
    }

    /// <summary>
    /// 檢查是否需要上傳檔案
    /// </summary>
    private static bool ShouldUploadFile(string localFilePath, string s3Key, Dictionary<string, S3FileInfo> s3FileMap, System.Action<string> onLog)
    {
        // catalog檔案總是需要上傳，因為內容可能變更但檔案大小相同
        string fileName = Path.GetFileName(localFilePath);
        if (fileName.ToLower().Contains("catalog"))
        {
            onLog?.Invoke($"📋 Catalog檔案總是上傳: {fileName}");
            return true;
        }

        if (!s3FileMap.TryGetValue(s3Key, out S3FileInfo s3FileInfo))
        {
            onLog?.Invoke($"📤 新檔案需要上傳: {Path.GetFileName(localFilePath)}");
            return true;
        }

return true;
        // try
        // {
        //     var localFileInfo = new System.IO.FileInfo(localFilePath);

        //     if (localFileInfo.Length != s3FileInfo.Size)
        //     {
        //         onLog?.Invoke($"📤 檔案大小不同，需要上傳: {Path.GetFileName(localFilePath)} (本地:{FormatFileSize(localFileInfo.Length)} vs S3:{FormatFileSize(s3FileInfo.Size)})");
        //         return true;
        //     }

        //     onLog?.Invoke($"⚡ 檔案相同，跳過: {Path.GetFileName(localFilePath)} ({FormatFileSize(localFileInfo.Length)})");
        //     return false;
        // }
        // catch (Exception ex)
        // {
        //     onLog?.Invoke($"⚠️ 比較檔案時發生錯誤: {ex.Message}");
        //     return true;
        // }
    }

    /// <summary>
    /// 按檔案大小排序
    /// </summary>
    private static FileInfo[] SortFilesBySize(string[] filePaths, System.Action<string> onLog)
    {
        var fileInfos = new List<FileInfo>();
        long totalSize = 0;

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
                onLog?.Invoke($"⚠️ 無法讀取檔案資訊: {filePath} - {ex.Message}");
                fileInfos.Add(new FileInfo
                {
                    FilePath = filePath,
                    Size = 0
                });
            }
        }

        var sortedFiles = fileInfos.OrderBy(f => f.Size).ToArray();

        onLog?.Invoke($"📊 檔案大小統計:");
        onLog?.Invoke($"  • 總檔案大小: {FormatFileSize(totalSize)}");
        onLog?.Invoke($"  • 最小檔案: {FormatFileSize(sortedFiles.First().Size)}");
        onLog?.Invoke($"  • 最大檔案: {FormatFileSize(sortedFiles.Last().Size)}");
        onLog?.Invoke($"  • 平均檔案大小: {FormatFileSize(totalSize / filePaths.Length)}");

        return sortedFiles;
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 驗證S3設定
    /// </summary>
    public static bool IsS3SettingsValid(AWSS3UploaderSettings.S3Profile profile)
    {
        return !string.IsNullOrEmpty(profile.GetS3BucketName()) &&
               !string.IsNullOrEmpty(profile.AccessKeyId) &&
               !string.IsNullOrEmpty(profile.SecretAccessKey);
    }

    /// <summary>
    /// 驗證主遊戲上傳設定
    /// </summary>
    public static bool IsGameUploadSettingsValid(AWSS3UploaderSettings.S3Profile profile)
    {
        if (string.IsNullOrEmpty(profile.LocalDirectoryPath) || !Directory.Exists(profile.LocalDirectoryPath))
            return false;
        if (string.IsNullOrEmpty(profile.GetS3BucketName()) ||
            string.IsNullOrEmpty(profile.AccessKeyId) ||
            string.IsNullOrEmpty(profile.SecretAccessKey))
            return false;
        if (string.IsNullOrEmpty(profile.GetWebGlUploadKeyPrefix()))
            return false;
        if (!profile.UploadToRootDirectory && string.IsNullOrEmpty(profile.GetUploadProjectName()))
            return false;
        return true;
    }

    /// <summary>
    /// 驗證AssetBundle設定
    /// </summary>
    public static bool IsAssetBundleSettingsValid(AWSS3UploaderSettings.S3Profile profile)
    {
        if (!profile.UploadAssetBundle)
            return true;

        return !string.IsNullOrEmpty(profile.GetAssetBundleDirectoryPath()) &&
               !string.IsNullOrEmpty(profile.GetUploadProjectName()) &&
               System.IO.Directory.Exists(profile.GetAssetBundleDirectoryPath()) &&
               IsS3SettingsValid(profile);
    }

    #endregion

    #region Exception Handlers

    private static void HandleS3Exception(AmazonS3Exception s3Ex, AWSS3UploaderSettings.S3Profile profile, System.Action<string> onLog)
    {
        if (s3Ex.ErrorCode == "NoSuchBucket")
        {
            onLog?.Invoke("💡 原因: 儲存桶不存在");
            onLog?.Invoke("🔧 解決方案:");
            onLog?.Invoke("  1. 檢查儲存桶名稱拼寫");
            onLog?.Invoke("  2. 確認儲存桶在正確的 AWS 帳戶中");
        }
        else if (s3Ex.ErrorCode == "AccessDenied")
        {
            onLog?.Invoke("💡 原因: 沒有權限存取此路徑");
            onLog?.Invoke("🔧 解決方案: 請確認 IAM 用戶具有以下權限:");
            onLog?.Invoke("  - s3:ListBucket (必須)");
            onLog?.Invoke("  - s3:PutObject (上傳時需要)");
            onLog?.Invoke("  - s3:DeleteObject (清空資料夾時需要)");
            onLog?.Invoke($"  - 針對資源: arn:aws:s3:::{profile.S3BucketName}/*");
        }
        else if (s3Ex.ErrorCode == "InvalidBucketName")
        {
            onLog?.Invoke("💡 原因: 儲存桶名稱格式不正確");
            onLog?.Invoke("🔧 解決方案: 儲存桶名稱必須符合 AWS 命名規則");
        }
        else
        {
            onLog?.Invoke("💡 其他 S3 錯誤，請檢查設定或聯絡管理員");
        }
    }

    private static void HandleGeneralException(Exception ex, AWSS3UploaderSettings.S3Profile profile, System.Action<string> onLog)
    {
        if (ex.Message.Contains("The request signature we calculated does not match"))
        {
            onLog?.Invoke("💡 原因: AWS 憑證錯誤");
            onLog?.Invoke("🔧 解決方案: 檢查 Access Key ID 和 Secret Access Key");
        }
        else if (ex.Message.Contains("Unable to resolve service endpoint"))
        {
            onLog?.Invoke("💡 原因: AWS 區域設定錯誤");
            onLog?.Invoke($"🔧 解決方案: 確認區域 '{profile.AwsRegion}' 是否正確");
            onLog?.Invoke("   常用區域: us-east-1, us-west-2, ap-northeast-1, ap-southeast-1");
        }
        else if (ex.Message.Contains("The security token included in the request is invalid"))
        {
            onLog?.Invoke("💡 原因: AWS 憑證無效或已過期");
            onLog?.Invoke("🔧 解決方案: 檢查並更新 Access Key ID 和 Secret Access Key");
        }
        else if (ex.Message.Contains("sending the request"))
        {
            onLog?.Invoke("💡 原因: 網路連線問題");
            onLog?.Invoke("🔧 解決方案:");
            onLog?.Invoke("  1. 檢查網路連線");
            onLog?.Invoke("  2. 檢查防火牆設定");
            onLog?.Invoke("  3. 檢查代理伺服器設定");
            onLog?.Invoke("  4. 確認可以存取 AWS 服務");
        }

        onLog?.Invoke("📋 基本除錯檢查清單:");
        onLog?.Invoke("  ✓ 網路連線是否正常");
        onLog?.Invoke("  ✓ AWS 憑證是否正確");
        onLog?.Invoke("  ✓ 儲存桶名稱是否正確");
        onLog?.Invoke("  ✓ AWS 區域是否正確");
        onLog?.Invoke("  ✓ IAM 權限是否足夠");
    }

    private static void HandleS3ListException(Exception ex, AWSS3UploaderSettings.S3Profile profile, System.Action<string> onLog)
    {
        if (ex.Message.Contains("The request signature we calculated does not match"))
        {
            onLog?.Invoke("💡 建議: Access Key 或 Secret Access Key 不正確");
        }
        else if (ex.Message.Contains("The specified bucket does not exist"))
        {
            onLog?.Invoke($"💡 建議: 儲存桶 '{profile.S3BucketName}' 不存在，請檢查名稱");
        }
        else if (ex.Message.Contains("Access Denied"))
        {
            onLog?.Invoke("💡 建議: 沒有讀取該儲存桶的權限，請檢查 IAM 權限設定");
        }
        else if (ex.Message.Contains("Unable to resolve service endpoint"))
        {
            onLog?.Invoke($"💡 建議: AWS 區域 '{profile.AwsRegion}' 設定錯誤");
        }
        else if (ex.Message.Contains("sending the request"))
        {
            onLog?.Invoke("💡 建議: 網路連線問題，請檢查:");
            onLog?.Invoke("  - 網路連線是否正常");
            onLog?.Invoke("  - 防火牆或代理伺服器設定");
            onLog?.Invoke("  - DNS 設定是否正確");
        }
        else if (ex.Message.Contains("The security token included in the request is invalid"))
        {
            onLog?.Invoke("💡 建議: AWS 憑證無效或已過期，請檢查並更新");
        }

        onLog?.Invoke("🔧 除錯步驟:");
        onLog?.Invoke("1. 先點擊「測試 AWS 連線」確認基本連線");
        onLog?.Invoke("2. 檢查儲存桶名稱是否正確");
        onLog?.Invoke("3. 確認 IAM 用戶具有 s3:ListBucket 權限");
    }

    #endregion

    #region File Content Configuration

    /// <summary>
    /// 配置檔案的 Content-Type 和 Content-Encoding
    /// </summary>
    private static void ConfigureContentTypeAndEncoding(PutObjectRequest request, string fileName)
    {
        // if (fileName.EndsWith(".wasm.br"))
        // {
        //     request.ContentType = "application/wasm";
        //     request.Headers["Content-Encoding"] = "br";
        // }
        // else if (fileName.EndsWith(".data.br"))
        // {
        //     request.ContentType = "application/octet-stream";
        //     request.Headers["Content-Encoding"] = "br";
        // }
        // else if (fileName.EndsWith(".js.br"))
        // {
        //     request.ContentType = "application/javascript";
        //     request.Headers["Content-Encoding"] = "br";
        // }
        // else if (fileName.EndsWith(".br"))
        // {
        //     request.Headers["Content-Encoding"] = "br";
        // }
        
        bool isGzip = fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

        if (isGzip)
        {
            // 统一设置 Content-Encoding 为 gzip
            request.Headers["Content-Encoding"] = "gzip";
        }

        // 根据文件类型设置正确的 Content-Type（非常重要，避免 MIME mismatch）
        if (fileName.EndsWith(".wasm.gz", StringComparison.OrdinalIgnoreCase) || 
            fileName.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
        {
            request.ContentType = "application/wasm";
        }
        else if (fileName.EndsWith(".js.gz", StringComparison.OrdinalIgnoreCase) || 
                 fileName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            request.ContentType = "application/javascript";
        }
        else if (fileName.EndsWith(".data.gz", StringComparison.OrdinalIgnoreCase) || 
                 fileName.EndsWith(".data", StringComparison.OrdinalIgnoreCase) ||
                 fileName.EndsWith(".unityweb", StringComparison.OrdinalIgnoreCase))
        {
            request.ContentType = "application/octet-stream";
        }
        else if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            request.ContentType = "application/json";
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 格式化檔案大小
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        if (bytes == 0)
            return "0 B";

        if (bytes < 0)
            return "Unknown";

        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size = size / 1024;
        }

        string formatString = $"F{FILE_SIZE_DISPLAY_PRECISION}";
        string formattedSize = size.ToString(formatString);

        if (formattedSize.Contains("."))
        {
            formattedSize = formattedSize.TrimEnd('0').TrimEnd('.');
        }

        return $"{formattedSize} {sizes[order]}";
    }

    /// <summary>
    /// 格式化時間長度
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalMinutes >= 1
            ? $"{duration.TotalMinutes:F1}分鐘"
            : $"{duration.TotalSeconds:F1}秒";
    }

    /// <summary>
    /// 格式化檔案路徑顯示
    /// </summary>
    public static string FormatFilePathForLog(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "";

        if (filePath.Length <= MAX_LOG_FILE_PATH_LENGTH)
            return filePath;

        string fileName = Path.GetFileName(filePath);
        if (fileName.Length >= MAX_LOG_FILE_PATH_LENGTH)
        {
            return fileName;
        }

        string directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directoryPath))
        {
            return fileName;
        }

        int availableLength = MAX_LOG_FILE_PATH_LENGTH - fileName.Length - 3;
        if (availableLength <= 0)
        {
            return fileName;
        }

        if (directoryPath.Length <= availableLength)
        {
            return Path.Combine(directoryPath, fileName);
        }

        string truncatedDirectory = directoryPath.Substring(0, availableLength);
        return $"{truncatedDirectory}.../{fileName}";
    }

    #endregion
}