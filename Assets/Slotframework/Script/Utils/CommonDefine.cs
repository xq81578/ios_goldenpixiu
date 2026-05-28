public static class CommonDefine
{

    #region Localization
    public const string CommonTableName = "Common_StringTable";
    public const string DialogTableName = "Dialog_StringTable";

    public const string CommonKey_SwitchCombo = "Switch_Combo";
    public const string CommonKey_SwitchReel = "Switch_Reel";

    public const string DialogKey_Confirm = "Btn_Confirm";
    public const string DialogKey_SystemTitle = "System_Title";
    public const string DialogKey_ErrorUnkown = "Error_Unknown";
    public const string DialogKey_MaxWin = "System_MaxWin";
    public const string DialogKey_ErrorBalance = "Error_Balance";
    public const string DialogKey_ErrorNetwork = "Error_Network";
    public const string DialogKey_ErrorDisconnect = "Error_Disconnect";
    public const string DialogKey_ErrorLogin = "Error_Login";
    public const string DialogKey_ErrorCode = "Error_Code_";
    public const string DialogKey_ErrorCode9004 = "Error_Code_9004";
    public const string DialogKey_SystemTitle_zhtw = "系統提示";
    #endregion

    #region GameFlowManager
    public const string ChangeGameScene = "ChangeGameScene";
    public const string DownLoadBundle = "DownLoadBundle";
    public const string LoadingProgressUpdate = "LoadingProgressUpdate";
    public const string LoadingBundleDone = "LoadingBundleDone";
    public const string LoadingProgressInfo = "LoadingProgressInfo";
    public const string SceneUILoaded = "SceneUILoaded";
    public const string GameServiceInit = "GameServiceInit";
    public const string GameServiceReady = "GameServiceReady";
    public const string GameUIInit = "GameUIInit";
    public const string GameUIReady = "GameUIReady";
    public const string GameReady = "GameReady";
    public const string GameService = "GameService";
    #endregion

    #region  ClientClickLog
    public const string ClientClick = "ClientClick";
    public enum EClientClick
    {
        EnterLoading = 2,
        DownLoadBundleStart = 3,
        DownLoadBundleING = 4,
        DownLoadBundleEnd = 5,
        ClickContinue = 6,
        EnterGameScene = 7,
        ClickInfo = 8,
    }
    #endregion
}