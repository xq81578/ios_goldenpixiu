# AWS S3 上傳器 API 使用指南

## 概述

AWS S3 上傳器現在提供了獨立的API模組，允許外部代碼直接調用各種功能，適用於自動化建置流程、CI/CD 管道整合等場景。

## 主要功能

### 1. 測試AWS連接

```csharp
bool result = await AWSS3UploaderAPI.TestAWSConnectionAsync(profile, logCallback);
```

### 2. 獲取S3檔案列表

```csharp
var fileList = await AWSS3UploaderAPI.GetS3FileListAsync(profile, logCallback);
```

### 3. 上傳主遊戲目錄

```csharp
var result = await AWSS3UploaderAPI.UploadGameDirectoryAsync(
    profile, logCallback, progressCallback, cancellationToken);
```

### 4. 上傳AssetBundle目錄

```csharp
var result = await AWSS3UploaderAPI.UploadAssetBundleDirectoryAsync(
    profile, logCallback, progressCallback, cancellationToken);
```

### 5. 完整上傳流程

```csharp
var result = await AWSS3UploaderAPI.FullUploadAsync(
    profile, logCallback, progressCallback, cancellationToken);
```

## 快速開始

### 1. 使用現有設定檔

```csharp
public static async void MyUploadFunction()
{
    // 載入現有設定檔（僅編輯器；資產位於 AWSS3Uploader/Editor，不進玩家包）
    var settings = AWSS3UploaderSettings.LoadEditorSettings(false);
    var profile = settings.GetSelectedProfile();
    
    // 上傳主遊戲
    var result = await AWSS3UploaderAPI.UploadGameDirectoryAsync(
        profile,
        log => Debug.Log(log),
        progress => Debug.Log($"進度: {progress.ProgressPercentage:F1}%"),
        CancellationToken.None);
    
    if (result.IsSuccess)
    {
        Debug.Log($"上傳成功！耗時: {AWSS3UploaderAPI.FormatDuration(result.Duration)}");
    }
}
```

### 2. 創建自定義設定檔

```csharp
public static async void CustomUpload()
{
    // 創建自定義設定檔
    var customProfile = new AWSS3UploaderSettings.S3Profile
    {
        ProfileName = "自動化建置",
        AwsRegion = "ap-northeast-1",
        AccessKeyId = "YOUR_ACCESS_KEY",
        SecretAccessKey = "YOUR_SECRET_KEY",
        S3BucketName = "my-game-bucket",
        S3KeyPrefix = "game001",
        LocalDirectoryPath = Application.dataPath + "/../builds/WebGL",
        MaxConcurrentUploads = 5,
        
        // AssetBundle設定
        UploadAssetBundle = true,
        AssetBundleDirectoryPath = Application.dataPath + "/../AssetBundles",
        AssetBundleS3Path = "BundleSource",
        SkipDuplicateBundleUploads = true
    };
    
    // 執行完整上傳流程
    var result = await AWSS3UploaderAPI.FullUploadAsync(
        customProfile,
        log => Debug.Log($"[BUILD] {log}"),
        progress => Debug.Log($"[BUILD] 上傳進度: {progress.ProgressPercentage:F1}%"),
        CancellationToken.None);
}
```

## 設定驗證

在調用API之前，建議先驗證設定是否有效：

```csharp
// 驗證主遊戲設定
if (!AWSS3UploaderAPI.IsGameUploadSettingsValid(profile))
{
    Debug.LogError("主遊戲設定無效");
    return;
}

// 驗證AssetBundle設定
if (!AWSS3UploaderAPI.IsAssetBundleSettingsValid(profile))
{
    Debug.LogError("AssetBundle設定無效");
    return;
}

```

## 進度監控

所有上傳方法都支援進度回調：

```csharp
var progressCallback = (AWSS3UploaderAPI.UploadProgress progress) =>
{
    Debug.Log($"上傳進度: {progress.ProgressPercentage:F1}%");
    Debug.Log($"已完成: {progress.CompletedFiles}/{progress.TotalFiles} 檔案");
    Debug.Log($"狀態: {progress.StatusMessage}");
    
    if (progress.ActiveUploads.Count > 0)
    {
        Debug.Log($"正在上傳: {string.Join(", ", progress.ActiveUploads)}");
    }
};
```

## 錯誤處理

```csharp
try
{
    var result = await AWSS3UploaderAPI.UploadGameDirectoryAsync(profile, null, null, cancellationToken);
    
    if (!result.IsSuccess)
    {
        Debug.LogError($"上傳失敗: {result.Message}");
        if (result.Exception != null)
        {
            Debug.LogException(result.Exception);
        }
    }
}
catch (OperationCanceledException)
{
    Debug.Log("上傳被取消");
}
catch (Exception ex)
{
    Debug.LogError($"上傳異常: {ex.Message}");
}
```

## 取消操作

長時間運行的操作支援取消：

```csharp
var cancellationTokenSource = new CancellationTokenSource();

// 5分鐘後自動取消
cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5));

// 開始上傳
var uploadTask = AWSS3UploaderAPI.UploadGameDirectoryAsync(
    profile, null, null, cancellationTokenSource.Token);

// 用戶可以手動取消
if (userWantsToCAncel)
{
    cancellationTokenSource.Cancel();
}

var result = await uploadTask;
```

## CI/CD 整合範例

### Jenkins Pipeline

```groovy
pipeline {
    stages {
        stage('Build Game') {
            steps {
                // 建置遊戲
                bat 'Unity.exe -batchmode -quit -executeMethod BuildScript.BuildWebGL'
            }
        }
        stage('Upload to S3') {
            steps {
                // 調用Unity腳本上傳
                bat 'Unity.exe -batchmode -quit -executeMethod DeployScript.UploadToS3'
            }
        }
    }
}
```

### Unity部署腳本

```csharp
public class DeployScript
{
    public static async void UploadToS3()
    {
        var profile = CreateProductionProfile();
        
        var result = await AWSS3UploaderAPI.FullUploadAsync(
            profile,
            log => Console.WriteLine($"[DEPLOY] {log}"),
            progress => Console.WriteLine($"[DEPLOY] {progress.ProgressPercentage:F1}%"),
            CancellationToken.None);
        
        if (result.IsSuccess)
        {
            Console.WriteLine($"🎉 部署成功！耗時: {AWSS3UploaderAPI.FormatDuration(result.Duration)}");
            EditorApplication.Exit(0); // 成功退出
        }
        else
        {
            Console.WriteLine($"❌ 部署失敗: {result.Message}");
            EditorApplication.Exit(1); // 失敗退出
        }
    }
}
```

## 測試與演示

1. **使用演示選單**：打開 `Tools > AWS S3 上傳器演示` 測試各種功能
2. **查看演示代碼**：參考 `AWSS3UploaderExamples.cs` 了解完整用法
3. **閱讀API文檔**：查看 `AWSS3UploaderAPI.cs` 中的完整方法說明

## 注意事項

1. **異步操作**：所有API都是異步的，必須使用 `async/await`
2. **設定驗證**：建議在調用前先驗證設定是否有效
3. **錯誤處理**：妥善處理異常和取消操作
4. **日誌回調**：提供日誌回調函數以便追蹤操作進度
5. **資源管理**：API會自動管理AWS客戶端資源，無需手動釋放

## 更多資訊

- 完整API文檔：`AWSS3UploaderAPI.cs`
- 使用演示：`AWSS3UploaderExamples.cs`
- 原始視窗：通過 `Tools > AWS S3 上傳器` 打開圖形界面
