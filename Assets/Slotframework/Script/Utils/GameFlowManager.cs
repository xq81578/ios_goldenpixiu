using System;
using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using StateMachine;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// 遊戲流程管理器
/// </summary>
public class GameFlowManager : Singleton<GameFlowManager>
{
    public enum GameState
    {
        Init, // 初始化
        GameServiceInit, // 遊戲服務初始化
        DownLoadBundle, // 下載Bundle
        ChangeGameScene, // 切換遊戲場景
        GameUIInit, // 遊戲UI初始化
        InitWaiting, // 等待初始化
        GameReady // 遊戲準備完成
    }

    protected StateMachine<GameState> _flowStateMachine;

    public bool IsGameReady { get; private set; } = false;
    [SerializeField] private AssetReference gameSceneReference = null;
    private Tween _fakeLoadingTween = null;
    private Timer _downLoadBundleTimer = null;
    private int _sendCDTime = 2;
    [SerializeField] private bool _isServiceReady = false;
    [SerializeField] private bool _isGameUIReady = false;

    [SerializeField] private bool _waitGameUIReady;

    #region Game Event

    private GameFlowProgressInfoEvent _gameFlowProgressInfoEvent = new();
    private LoadingProgressEvent _loadingProgressEvent = new();
    private ClientClickEvent _clientClickEvent = new();
    private Action _onGameServiceListener;
    private Action _onGameReadyListener;

    #endregion

    private void OnEnable()
    {
        GameEventHub.Bind(this);
    }

    private void OnDisable()
    {
        GameEventHub.Unbind(this);
        _loadingProgressEvent = null;
        _gameFlowProgressInfoEvent = null;
        _clientClickEvent = null;
        _onGameServiceListener = null;
        _onGameReadyListener = null;
    }

    protected override void Awake()
    {
        base.Awake();
        _flowStateMachine = new StateMachine<GameState>(this);
    }

    private void Start()
    {
        ChangeState(GameState.Init);
    }

    private void Update()
    {
        _flowStateMachine.Driver.Update.Invoke();
    }

    private void ChangeState(GameState state)
    {
        _gameFlowProgressInfoEvent.SetProgressInfo(state.ToString()).Publish(this);
        _flowStateMachine.ChangeState(state);
    }

    private void Init_Enter()
    {
        LogUtils.Log("[GameFlowManager] Init_Enter");
        IsGameReady = false;
        _isServiceReady = false;
        _isGameUIReady = false;

        // ChangeLanguage();  // 這裡應該會預設英文語系，不需特別設定，Testing ,TODO Delete
        // 初始化進度為0%
        _loadingProgressEvent.SetProgress(0f).Publish(this);

        if (_waitGameUIReady)
        {
            ChangeState(GameState.DownLoadBundle);
        }
        else
        {
            ChangeState(GameState.GameServiceInit);
        }
    }

    private void GameServiceInit_Enter()
    {
        LogUtils.Log("[GameFlowManager] GameServiceInit_Enter");

        _isServiceReady = false;
        _onGameServiceListener = GameEventHub.Listen<GameServiceReadyEvent>(this
            , (e) =>
            {
                _onGameServiceListener();
                _isServiceReady = true;
                LogUtils.Log("[GameFlowManager] Service Ready");
            });
        new GameServiceInitEvent().Publish(this);
        _loadingProgressEvent.SetProgress(0.05f).Publish(this);
        ChangeState(GameState.DownLoadBundle);
    }

    private async void DownLoadBundle_Enter()
    {
        LogUtils.Log("[GameFlowManager] DownLoadBundle_Enter");
        _clientClickEvent.SetClick(CommonDefine.EClientClick.DownLoadBundleStart).Publish(this);

        try
        {
            await InitializeAndLoadLocaleAsync();
            await SourceManager.Instance.PreLoadScene(gameSceneReference, OnPreloadSceneProgress);
        }
        catch (Exception ex)
        {
            LogUtils.LogError($"[GameFlowManager] DownLoadBundle failed: {ex.Message}\n{ex.StackTrace}");
            CancelTimer();
            DialogMediator.ShowDialog(
                "System Notice",
                "Network connection error. Please reconnect.",
                new ActionButton("OK", GoBackToPreviousPage),
                false
            );
            return;
        }

        LogUtils.Log("[GameFlowManager] All bundles download complete.");
        CancelTimer();

        _clientClickEvent.SetClick(CommonDefine.EClientClick.DownLoadBundleEnd).Publish(this);

        ChangeState(GameState.ChangeGameScene);
    }

    private void GoBackToPreviousPage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLPageReloader.BackToPreviousPage();
#else
        LogUtils.Log("[GameFlowManager] GoBackToPreviousPage only works in WebGL build.");
#endif
    }

    private async void ChangeGameScene_Enter()
    {
        LogUtils.Log("[GameFlowManager] ChangeGameScene_Enter");
        bool isSceneLoaded = await SourceManager.Instance.ActivateScene(OnSceneLoadProgress);

        if (isSceneLoaded)
        {
            OnSceneLoadedSucceeded();
        }
        else
        {
            OnSceneLoadedFailed();
        }
    }

    private void GameUIInit_Enter()
    {
        LogUtils.Log("[GameFlowManager] GameUIInit_Enter");

        _isGameUIReady = false;
        _onGameReadyListener = GameEventHub.Listen<GameUIReadyEvent>(this
            , (e) =>
            {
                _onGameReadyListener();
                _isGameUIReady = true;
                _loadingProgressEvent.SetProgress(0.95f).Publish(this); // 90 ~ 95%
                LogUtils.Log("[GameFlowManager] GameUI Ready");
            });
        new GameUIInitEvent().Publish(this);
        ChangeState(GameState.InitWaiting);
    }

    private void InitWaiting_Update()
    {
        if (_waitGameUIReady)
        {
            if (_isGameUIReady)
            {
                ChangeState(GameState.GameReady);
            }
        }
        else
        {
            if (_isServiceReady && _isGameUIReady)
            {
                ChangeState(GameState.GameReady);
            }
        }
    }

    private async void GameReady_Enter()
    {
        LogUtils.Log("[GameFlowManager] GameReady_Enter");
        IsGameReady = true;
        new GameReadyEvent(_waitGameUIReady).Publish(this);

        await UniTask.WaitForEndOfFrame();
        enabled = false;
        if (_waitGameUIReady)
        {
            _isServiceReady = false;
            _onGameServiceListener = GameEventHub.Listen<GameServiceReadyEvent>(this
                , (e) =>
                {
                    _onGameServiceListener();
                    _isServiceReady = true;
                    LogUtils.Log("[GameFlowManager] Service Ready");
                });
            new GameServiceInitEvent().Publish(this);
        }
    }

    private async void ChangeLanguage()
    {
        await LocalizationSettings.InitializationOperation;

        LocalizationSettings.SelectedLocale =
            LocalizationSettings.AvailableLocales.Locales[(int)LocalizationDefine.LanguageType.en];
    }
    
    /// <summary>
    /// 負責初始化系統並預載當前語系的所有內容
    /// </summary>
    private async UniTask InitializeAndLoadLocaleAsync()
    {
        // 1. 主動觸發 Localization 系統初始化，這瞬間，Unity 會去跑 UrlParamLocaleSelector 邏輯
        LogUtils.Log("[GameFlowManager] Initializing Localization Settings...");
        await LocalizationSettings.InitializationOperation.Task.AsUniTask();

        // 2. 取得要載入的語系，這裡的 SelectedLocale 就是 URL 指定的語言！
        var localeToLoad = LocalizationSettings.SelectedLocale;

        LogUtils.Log($"[GameFlowManager] Preloading locale: {localeToLoad.Identifier.Code}");

        // 3. 預先下載/載入該語系的資源
        await PreloadLanguageDataAsync(localeToLoad);
        _loadingProgressEvent.SetProgress(0.1f).Publish(this);
    }

    private async UniTask PreloadLanguageDataAsync(Locale locale)
    {
        var stringDb = LocalizationSettings.StringDatabase;
        var assetDb = LocalizationSettings.AssetDatabase;

        LogUtils.Log("[GameFlowManager] Preloading String Tables and Asset Tables in parallel...");

        // 下载 String Tables 和 Asset Tables
        var stringTablesTask = stringDb.GetAllTables(locale).Task.AsUniTask();
        var assetTablesTask = assetDb.GetAllTables(locale).Task.AsUniTask();

        await UniTask.WhenAll(stringTablesTask, assetTablesTask);
        LogUtils.Log("[GameFlowManager] Both String Tables and Asset Tables preloading completed.");
    }

    private void OnPreloadSceneProgress(float progress)
    {
        float totalProgress = 0.1f + progress * 0.7f;
        _loadingProgressEvent.SetProgress(totalProgress).Publish(this);
    }

    private void OnSceneLoadProgress(float progress)
    {
        _fakeLoadingTween?.Kill();

        float totalProgress = 0.8f + (progress * 0.1f);
        _loadingProgressEvent.SetProgress(totalProgress).Publish(this);
    }

    private void OnSceneLoadedSucceeded()
    {
        ChangeState(GameState.GameUIInit);
    }

    private void OnSceneLoadedFailed()
    {
        LogUtils.LogError("[GameFlowManager] OnSceneLoadedFailed");
    }

    private void SendDownLoadingLog()
    {
        CancelTimer();
        _downLoadBundleTimer = this.AttachTimer(_sendCDTime,
            () => { _clientClickEvent.SetClick(CommonDefine.EClientClick.DownLoadBundleING).Publish(this); });
    }

    private void CancelTimer()
    {
        Timer.Cancel(_downLoadBundleTimer);
        _downLoadBundleTimer = null;
    }
}
