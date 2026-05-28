using System;
using System.Collections.Generic;
using System.Linq;
using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Protobuf.Gateway;
using Sirenix.OdinInspector;
using Slot.Common;
using UnityEngine;
using UnityEngine.Networking;
using VContainer;

public class BaseGameService : MonoBehaviour
{
    private enum RuntimeServerMode
    {
        Dev,
        Demo,
        Pro
    }

    #region ErrorCode

    private enum ErrorCode
    {
        //5014 账号登出
        Error_SOCKET_KICK = 5014,
    }

    #endregion

    [SerializeField] private GameInfoSO _gameInfoData;

    [Inject] private PlatformData _platformData;
    [Inject] private GameStateData _gameStateData;
    [Inject] protected PlayerBetData _playerBetData;
    [Inject] protected GameInfoSO _gameInfoSO;
    public string webSocketUri;
    [SerializeField, InlineEditor] public ServerSettingScriptableObject _serverSetting;
    public virtual string GAME_ID => "UnDefineGameID";
    public const string GLOBAL_BROADCAST_CMD = "global_broadcast";
    public const string JIONROOM_CMD = "join_room";
    public const string GET_BALANCE_CMD = "GetBalance";
    public const string GET_RECORD_CMD = "game_history_uri";
    public const string GET_HOMEURL_CMD = "game_home_uri";
    public const string GET_SYSTELOG_CMD = "20002";
    public const string DEVINFO_CMD = "DeviceInfo";
    private const string SYSTEM_URL_API = "https://game-api.gowin.fun/play/game/uri";
    private const string SYSTEM_URL_UAT_API = "https://game.gowin-api.online/play/game/uri";
    public bool IsReady => _isLogin;
    public bool IsServiceReady => _isGetInfo && _isGetBalance;

    public event Action<int, string> OnSendFailEvent;

    private Action<ulong> _OnGetBalanceCallback;
    private Action<double[]> _OnGetGameInfoCallback;
    private Action<string, ulong, ByteString> _OnSpinCallback;
    private Action<JToken> OnSpinCallback;
    private Action _OnUpdDeviceInfoCallback;
    private Action<string> _OnAutoSettingCallback;
    private Action _OnSendClientClickCallback;

    private bool _isLogin = false;
    private bool _isInitialize = false;
    private bool _isGetInfo = false;
    private bool _isGetBalance = false;
    private string _lastAutoSpinId = "";
    private string _recordUrl = "";
    private MemberPreference _preference = null;
    private bool _isProcessingQueue = false;
    private bool _isRefreshingSystemUrl = false;
    private bool _isWaitingRecordUrlResponse = false;
    private bool _isWaitingHomeUrlResponse = false;
    private Action<bool> _onRefreshSystemUrlCompleted;

    private ClientSpinCmd _tempSpinCmd = null;

    // #if UNITY_EDITOR
    [SerializeField] public string Token;

    [SerializeField] public bool isAutoJionRoom = true;

    [SerializeField] private bool isHaveRoomList = false;
    // #endif

#if !DISABLE_SRDEBUGGER
    private BaseGameOptionContainer _optionContainer;
#endif


    private void Awake()
    {
#if UNITY_EDITOR
#if DEV_BUILD
        LogUtils.Log("This is the Dev environment.");
        webSocketUri = _serverSetting.DevWebSocketUri;
#elif UAT_BUILD
        webSocketUri = _serverSetting.UATWebSocketUri;
#elif RELEASE_BUILD
 webSocketUri = _serverSetting.ProdWebSocketUri;
#endif
        return;
#endif
#if DEV_BUILD
        LogUtils.Log("This is the Dev environment.");
        webSocketUri = _serverSetting.DevWebSocketUri;
#elif UAT_BUILD || RELEASE_BUILD


        switch (GetRuntimeServerMode())
        {
            case RuntimeServerMode.Dev:
                LogUtils.Log("This is the Dev environment.");
                webSocketUri = _serverSetting.DevWebSocketUri;
                break;
            case RuntimeServerMode.Demo:
                LogUtils.Log("This is the Demo environment.");
                webSocketUri = _serverSetting.UATWebSocketUri;
                break;
            default:
                LogUtils.Log("This is the Release environment.");
                webSocketUri = _serverSetting.ProdWebSocketUri;
                break;
        }
#else
        LogUtils.LogError("Environment not defined.");
#endif
    }

    private static RuntimeServerMode GetRuntimeServerMode()
    {
        string mode = URLParameter.GetURLParameter("mode");
        if (string.Equals(mode?.Trim(), "dev", StringComparison.OrdinalIgnoreCase))
            return RuntimeServerMode.Dev;
        if (string.Equals(mode?.Trim(), "demo", StringComparison.OrdinalIgnoreCase))
            return RuntimeServerMode.Demo;
        return RuntimeServerMode.Pro;
    }

    private static bool IsRuntimeDevMode()
    {
        return GetRuntimeServerMode() == RuntimeServerMode.Dev;
    }

    private static string GetSystemUrlApi()
    {
        return GetRuntimeServerMode() == RuntimeServerMode.Demo ? SYSTEM_URL_UAT_API : SYSTEM_URL_API;
    }

    private void OnEnable()
    {
        GameEventHub.Bind(this);
    }

    private void OnDisable()
    {
        GameEventHub.Unbind(this);
    }

    protected virtual void Start()
    {
        LogUtils.Log("BaseGameService Start.");
        GameServerHandler.Instance.AddResponseAction<JToken>(JIONROOM_CMD, OnJionRoom);
        GameServerHandler.Instance.AddResponseAction<JToken>(GET_BALANCE_CMD, OnGetBalanceResponse);
        GameServerHandler.Instance.AddResponseAction<JToken>(GLOBAL_BROADCAST_CMD, OnGlobalBroadcastResponse);

        GameServerHandler.Instance.AddResponseAction<JToken>(GET_SYSTELOG_CMD, OnSystemLogResponse);
        GameServerHandler.Instance.OnOpen += OnWebSocketOpen;
        GameServerHandler.Instance.OnSendFail += OnSendFail;
        GameServerHandler.Instance.OnConnectTimeout += OnConnectTimeout;


#if !DISABLE_SRDEBUGGER
        _optionContainer = new BaseGameOptionContainer();
        _optionContainer.platformData = _platformData;
        SRDebug.Instance.AddOptionContainer(_optionContainer);
        SRDebug.Instance.IsTriggerEnabled = true;
#endif
    }

    [OnGameEvent(SubscriberPriority.High)]
    protected virtual void OnGameServiceInit(GameServiceInitEvent e)
    {
        GetInfo().Forget();
        WaitServiceReady().Forget();
    }

    protected virtual async UniTaskVoid WaitServiceReady()
    {
        await UniTask.WaitUntil(() => IsServiceReady);
        new GameServiceReadyEvent().Publish(this);
    }

    protected virtual void OnDestroy()
    {
        GameServerHandler.Instance.OnOpen -= OnWebSocketOpen;
        GameServerHandler.Instance.OnSendFail -= OnSendFail;
        GameServerHandler.Instance.RemoveResponseAction(JIONROOM_CMD);
        GameServerHandler.Instance.RemoveResponseAction(GET_BALANCE_CMD);


        GameEventHub.Unbind(this);
        _gameInfoSO.Clear();
    }

    private void OnWebSocketOpen(object sender, EventArgs e)
    {
        LogUtils.Log($"[BaseGameService] OnWebSocketOpen");

        if (!_isInitialize)
        {
            Initialize();
            _isInitialize = true;
        }

        _isLogin = true;
        SendDeviceInfo();
        SendGetBalanceRequest(SetBalance, _platformData.CurrencyEnum);
        if (isHaveRoomList)
        {
            GameServerHandler.Instance.Send("rooms_get", new JObject());
        }

        if (isAutoJionRoom)
        {
            GameServerHandler.Instance.Send(JIONROOM_CMD, new JObject());
        }

        WebSocketOpen();
    }

    private void SendDeviceInfo()
    {
        DeviceInfo deviceInfo = DeviceInfoRetriever.GetDeviceInfo();
        var deviceId = deviceInfo.deviceId;
        JObject cmdData = new() { ["DeviceId"] = deviceId, };
        GameServerHandler.Instance.Send(DEVINFO_CMD, cmdData);
    }

    protected virtual void WebSocketOpen()
    {
    }

    protected virtual void OnSendFail(object sender, string cmd)
    {
        LogUtils.LogError($"Send {cmd} fail.");
        ShowError(CommonDefine.DialogKey_ErrorNetwork);
    }

    private void OnConnectTimeout(object sender, EventArgs e)
    {
        LogUtils.LogError($"Connect timeout");
        ShowError(CommonDefine.DialogKey_ErrorDisconnect);
    }


    protected virtual void Initialize()
    {
    }

    // 檢查是否有重新連線
    private void CheckReconnect()
    {
        // 如果是重新連線，則重新取得遊戲資訊
        if (GameServerHandler.Instance.IsReconnected)
        {
            _gameInfoSO.SetGameInfo(_gameInfoData);
            SetGameInfo();
        }
    }


    public virtual void OnJionRoom(ResInfo responseInfo, JToken responseData)
    {
        LogUtils.Log($"[BaseGameService] 加入房间");
        _isLogin = responseInfo.code == 200;
        if (!_isLogin)
        {
            ShowErrorCode(responseInfo.msg);
            return;
        }

        CheckReconnect();
    }

    //取得錢包
    public void SendGetBalanceRequest(Action<ulong> callback = null, ECurrency currency = ECurrency.PHP)
    {
        _isGetBalance = false;
        // GetBalanceCmd cmdData = new() { Currency = currency.ToString() };
        GameServerHandler.Instance.Send(GET_BALANCE_CMD, new JObject());

        _OnGetBalanceCallback = callback == null ? SetBalance : callback;
    }


    private void OnGetBalanceResponse(ResInfo responseInfo, JToken responseData)
    {
        var data = responseData.ToObject<JObject>();
        if (responseInfo.code != 200)
        {
            ShowErrorCode(responseInfo.msg);
            return;
        }

        if (data["user_id"] != null)
        {
            _platformData.AccountId = (int)data["user_id"];
        }

        if (data["currency"] != null)
        {
            _platformData.SetCurrencyEnum(UtilityConstants.TryParseCurrency((string)data["currency"]));
        }

        _OnGetBalanceCallback?.Invoke((ulong)data["balance"]);
    }


    public virtual void SendSpin(string gameId, double totalBet, BuyType buyType = BuyType.BUY_NONE,
        JObject extraData = null, Action<JToken> callback = null)
    {
        double newBalance = _playerBetData.Balance - totalBet;
        _playerBetData.Balance = newBalance < 0 ? 0 : newBalance;
        new GameChangeBalanceEvent().Publish(this);

        bool isAuto = _gameStateData.IsAuto;
        string autoSpinId = isAuto ? _lastAutoSpinId : "Null";
        int turbo = (int)_gameStateData.TurboType;
        JObject cmdData = new() { ["bet"] = ServiceUtils.ToServerBalance(totalBet), };

        if (extraData != null)
        {
            foreach (var item in extraData)
            {
                cmdData[item.Key] = item.Value;
            }
        }

        GameServerHandler.Instance.AddResponseAction<JToken>("spin", OnSpinRes);
        GameServerHandler.Instance.Send("spin", cmdData);
        OnSpinCallback = callback;
    }

    protected virtual void OnSpinRes(ResInfo responseInfo, JToken data)
    {
        if (responseInfo.code == 9004)
        {
            ShowTipsCode();
            return;
        }

        GameServerHandler.Instance.RemoveResponseAction(responseInfo.cmd);

        if (responseInfo.code != 200)
        {
            LogUtils.LogError(
                $"Send command fail.(cmd={responseInfo.cmd}, code={responseInfo.code}, msg={responseInfo.msg})");


            switch (responseInfo.msg)
            {
                case "":
                    ShowError(CommonDefine.DialogKey_ErrorUnkown);
                    break;
                case "Player insufficient balance":
                    ShowError(CommonDefine.DialogKey_ErrorBalance);
                    break;
                case "insufficient balance":
                    ShowError(CommonDefine.DialogKey_ErrorBalance);
                    break;

                default:
                    ShowErrorCode(responseInfo.msg);
                    break;
            }

            return;
        }


        data["Balance"] = responseInfo.balance;
        // data["TxnId"] = responseInfo.txnid;
        OnSpinCallback?.Invoke(data);
    }


    public void SendAutoSpin(int totalSpin, int stopRatio, double stopLessBalance, double stopMoreBalance,
        bool stopFreeGame, Action<string> callback = null)
    {
        _lastAutoSpinId = UniqueIDGenerator.GenerateUniqueID();
        AutoSpin cmdData = new()
        {
            AutoSpinId = _lastAutoSpinId,
            TotalSpin = totalSpin,
            StopRatio = stopRatio,
            StopLessBalance = stopLessBalance,
            StopMoreBalance = stopMoreBalance,
            StopFreeGame = stopFreeGame,
        };
        _OnAutoSettingCallback = callback;
    }


    public async UniTaskVoid GetInfo()
    {
        var gameToken = URLParameter.GetURLParameter("t");
        var lang = URLParameter.GetURLParameter("lang") ?? URLParameter.GetURLParameter("l") ?? "en";
        if (string.IsNullOrEmpty(gameToken))
        {
            gameToken = Token;
        }

        GameServerHandler.Instance.Connect($"{webSocketUri}?t={gameToken}&lang={lang}");
        await UniTask.WaitUntil(() => IsReady);

        _gameInfoSO.SetGameInfo(_gameInfoData);

        SetGameInfo();

        Initialization();
    }

    private void SetBalance(ulong balance)
    {
        _isGetBalance = true;
        _playerBetData.Balance = ServiceUtils.ToClientBalance(balance);
        new GameChangeBalanceEvent().Publish(this);
    }

    private void SetGameInfo()
    {
        _isGetInfo = true;
        GetSystemUrl();
        SetBetData();
    }

    private void SetRecordUrl()
    {
        _gameInfoSO.SetRecordUrl(_recordUrl);
        new SetRecordUrlEvent(_recordUrl).Publish(this);
    }

    private void GetSystemUrl()
    {
        GetSystemUrlByHttp().Forget();
    }

    private async UniTaskVoid GetSystemUrlByHttp()
    {
        var token = URLParameter.GetURLParameter("t");
        if (string.IsNullOrEmpty(token))
        {
            token = Token;
        }

        if (string.IsNullOrEmpty(token))
        {
            // 开发环境下 token 可能是空，不强制要求，走兜底流程
            if (IsDevEnvironment())
            {
                LogUtils.LogWarning(
                    "[BaseGameService] GetSystemUrl skipped: token is empty (dev environment), use fallback urls.");
                ApplyDevFallbackSystemUrls();
                if (_isRefreshingSystemUrl)
                {
                    _isWaitingRecordUrlResponse = false;
                    _isWaitingHomeUrlResponse = false;
                    TryCompleteRefreshSystemUrl(true);
                }

                return;
            }

            LogUtils.LogError("[BaseGameService] GetSystemUrl failed: token is empty.");
            if (_isRefreshingSystemUrl)
            {
                _isWaitingRecordUrlResponse = false;
                _isWaitingHomeUrlResponse = false;
                TryCompleteRefreshSystemUrl(false);
            }

            return;
        }

        string deviceId = DeviceInfoRetriever.GetDeviceInfo()?.deviceId;
        if (string.IsNullOrEmpty(deviceId))
        {
            if (IsDevEnvironment())
            {
                LogUtils.LogWarning(
                    "[BaseGameService] GetSystemUrl skipped: deviceId is empty (dev environment), use fallback urls.");
                ApplyDevFallbackSystemUrls();
                if (_isRefreshingSystemUrl)
                {
                    _isWaitingRecordUrlResponse = false;
                    _isWaitingHomeUrlResponse = false;
                    TryCompleteRefreshSystemUrl(true);
                }

                return;
            }

            LogUtils.LogError("[BaseGameService] GetSystemUrl failed: deviceId is empty.");
            if (_isRefreshingSystemUrl)
            {
                _isWaitingRecordUrlResponse = false;
                _isWaitingHomeUrlResponse = false;
                TryCompleteRefreshSystemUrl(false);
            }

            return;
        }

        var recordTask = RequestSystemUrlAsync(GET_RECORD_CMD, token, deviceId);
        var homeTask = RequestSystemUrlAsync(GET_HOMEURL_CMD, token, deviceId);
        var (recordUrl, homeUrl) = await UniTask.WhenAll(recordTask, homeTask);

        bool success = true;
        bool devEnv = IsDevEnvironment();

        if (string.IsNullOrEmpty(recordUrl) && devEnv)
        {
            recordUrl = GetDevFallbackRecordUrl();
            LogUtils.LogWarning($"[BaseGameService] GetSystemUrl record url empty in dev, use fallback: {recordUrl}");
        }

        if (!string.IsNullOrEmpty(recordUrl))
        {
            _recordUrl = recordUrl;
            SetRecordUrl();
        }
        else
        {
            success = false;
            LogUtils.LogError("[BaseGameService] GetSystemUrl failed: record url is empty.");
        }

        if (string.IsNullOrEmpty(homeUrl) && devEnv)
        {
            homeUrl = GetDevFallbackHomeUrl();
            LogUtils.LogWarning($"[BaseGameService] GetSystemUrl home url empty in dev, use fallback: {homeUrl}");
        }

        if (!string.IsNullOrEmpty(homeUrl))
        {
            _platformData.SetHomeUrl(homeUrl);
            new SetHomeUrlEvent().Publish(this);
        }
        else
        {
            success = false;
            LogUtils.LogError("[BaseGameService] GetSystemUrl failed: home url is empty.");
        }

        if (_isRefreshingSystemUrl)
        {
            _isWaitingRecordUrlResponse = false;
            _isWaitingHomeUrlResponse = false;
            TryCompleteRefreshSystemUrl(success);
        }
    }

    private async UniTask<string> RequestSystemUrlAsync(string type, string token, string deviceId)
    {
        var baseUrl = "";
#if DEV_BUILD
        return "";
#elif UAT_BUILD || RELEASE_BUILD
        if (IsRuntimeDevMode())
            return "";
        baseUrl = GetSystemUrlApi();
#else
        LogUtils.LogError("Environment not defined.");
        return null;
#endif
        var requestUrl =
            $"{baseUrl}?device={UnityWebRequest.EscapeURL(deviceId)}&type={UnityWebRequest.EscapeURL(type)}";
        using var request = UnityWebRequest.Get(requestUrl);
        request.SetRequestHeader("Authorization", token);

        await request.SendWebRequest();

        bool devEnv = IsDevEnvironment();

        if (request.result != UnityWebRequest.Result.Success)
        {
            var msg =
                $"[BaseGameService] RequestSystemUrl failed: type={type}, error={request.error}, response={request.downloadHandler?.text}";
            if (devEnv) LogUtils.LogWarning(msg);
            else LogUtils.LogError(msg);
            return null;
        }

        var responseText = request.downloadHandler?.text;
        if (string.IsNullOrEmpty(responseText))
        {
            var msg = $"[BaseGameService] RequestSystemUrl failed: type={type}, empty response.";
            if (devEnv) LogUtils.LogWarning(msg);
            else LogUtils.LogError(msg);
            return null;
        }

        try
        {
            var root = JObject.Parse(responseText);
            var code = root["code"]?.Value<int>() ?? -1;
            if (code != 200)
            {
                var msg =
                    $"[BaseGameService] RequestSystemUrl business failed: type={type}, code={code}, msg={root["msg"]?.Value<string>()}";
                if (devEnv) LogUtils.LogWarning(msg);
                else LogUtils.LogError(msg);
                return null;
            }

            var data = root["data"];
            var uri = data?["link"]?.Value<string>();
            if (string.IsNullOrEmpty(uri) && data != null && data.Type == JTokenType.String)
            {
                uri = data.Value<string>();
            }

            return uri;
        }
        catch (Exception e)
        {
            var msg = $"[BaseGameService] RequestSystemUrl parse failed: type={type}, error={e.Message}";
            if (devEnv) LogUtils.LogWarning(msg);
            else LogUtils.LogError(msg);
            return null;
        }
    }

    private static bool IsDevEnvironment()
    {
#if UNITY_EDITOR || DEV_BUILD
        return true;
#else
        return IsRuntimeDevMode();
#endif
    }

    private void ApplyDevFallbackSystemUrls()
    {
        _recordUrl = GetDevFallbackRecordUrl();
        SetRecordUrl();

        _platformData.SetHomeUrl(GetDevFallbackHomeUrl());
        new SetHomeUrlEvent().Publish(this);
    }

    private static string GetDevFallbackRecordUrl()
    {
        return "about:blank";
    }

    private static string GetDevFallbackHomeUrl()
    {
        return "about:blank";
    }

    private void OnGetRecordURLResponse(ResInfo responseInfo, JToken responseData)
    {
        var data = responseData.ToObject<JObject>();
        _recordUrl = (string)data["uri"];
        SetRecordUrl();

        if (_isRefreshingSystemUrl)
        {
            _isWaitingRecordUrlResponse = false;
            TryCompleteRefreshSystemUrl(true);
        }
    }

    private void OnGetHomeURLResponse(ResInfo responseInfo, JToken responseData)
    {
        var data = responseData.ToObject<JObject>();
        var homeUrl = (string)data["uri"];
        _platformData.SetHomeUrl(homeUrl);
        new SetHomeUrlEvent().Publish(this);

        if (_isRefreshingSystemUrl)
        {
            _isWaitingHomeUrlResponse = false;
            TryCompleteRefreshSystemUrl(true);
        }
    }

    private async UniTaskVoid RefreshSystemUrlWithCallback(RefreshSystemUrlRequestEvent e)
    {
        if (!GameServerHandler.Instance.IsConnected)
        {
            e?.OnCompleted?.Invoke(false);
            return;
        }

        if (_isRefreshingSystemUrl)
        {
            _onRefreshSystemUrlCompleted += e?.OnCompleted;
            return;
        }

        _isRefreshingSystemUrl = true;
        _isWaitingRecordUrlResponse = true;
        _isWaitingHomeUrlResponse = true;
        _onRefreshSystemUrlCompleted = e?.OnCompleted;

        GetSystemUrl();

        await UniTask.Delay(3000);
        if (_isRefreshingSystemUrl)
        {
            TryCompleteRefreshSystemUrl(false);
        }
    }

    private void TryCompleteRefreshSystemUrl(bool success)
    {
        if (_isWaitingRecordUrlResponse || _isWaitingHomeUrlResponse)
        {
            if (success)
            {
                return;
            }
        }

        _isRefreshingSystemUrl = false;
        _isWaitingRecordUrlResponse = false;
        _isWaitingHomeUrlResponse = false;

        var completedCallback = _onRefreshSystemUrlCompleted;
        _onRefreshSystemUrlCompleted = null;
        completedCallback?.Invoke(success);
    }

    protected void OnSystemLogResponse(ResInfo responseInfo, JToken responseData)
    {
        switch (responseInfo.code)
        {
            case 5014:
                ShowError(CommonDefine.DialogKey_ErrorLogin);
                break;
            case 5000:
                ShowError(CommonDefine.DialogKey_ErrorDisconnect);
                break;
        }
    }

    public virtual void SetBetData()
    {
        if (_playerBetData.Bet == 0)
            _playerBetData.Bet = _gameInfoSO.GetDefaultBet();
        new GameChangeBetEvent().Publish(this);
    }


    public virtual void ShowTipsCode()
    {
        DialogMediator.ShowDialog(
            CommonDefine.DialogTableName, CommonDefine.DialogKey_SystemTitle,
            CommonDefine.DialogTableName, CommonDefine.DialogKey_ErrorCode9004,
            new ActionButton("OK", () => { }), true
        );
    }

    public void ShowErrorCode(string msg)
    {
        DialogMediator.ShowDialog(
            CommonDefine.DialogKey_SystemTitle_zhtw,
            msg,
            new ActionButton("OK", () => { new ErrorLogEvent().Publish(this); }), false
        );
    }

    public void ShowError(string key)
    {
        DialogMediator.ShowDialog(
            CommonDefine.DialogTableName, CommonDefine.DialogKey_SystemTitle,
            CommonDefine.DialogTableName, key,
            new ActionButton("OK", () => { new ErrorLogEvent().Publish(this); }), false
        );
    }

    #region Event Listener

    [OnGameEvent]
    protected void OnAutoSpinStartEvent(AutoSpinStartEvent e)
    {
        SendAutoSpin(
            e.TotalSpins,
            e.StopRatio,
            e.StopLessBalance,
            e.StopMoreBalance,
            e.IsStopFreeGame,
            e.Callback
        );
    }

    [OnGameEvent(SubscriberPriority.High)]
    protected void OnRefreshSystemUrlRequestEvent(RefreshSystemUrlRequestEvent e)
    {
        RefreshSystemUrlWithCallback(e).Forget();
    }

    #endregion


    private void Initialization()
    {
        _platformData.SetId(1);

        _isGetInfo = true;
        SetBetData();

        // 发布相关事件

        new SetPlatformIdEvent().Publish(this);
        new GameChangeBalanceEvent().Publish(this);
        new GameChangeBetEvent().Publish(this);
        new GameServiceReadyEvent().Publish(this);
    }


    private void OnGlobalBroadcastResponse(ResInfo responseInfo, JToken responseData)
    {
        if (responseInfo.code != 200 || responseData == null)
            return;
        var data = responseData as JObject ?? responseData.ToObject<JObject>();
        if (data == null)
            return;

        int? type = data.Value<int?>("type");
        string content = data.Value<string>("content");
        if (type == null || string.IsNullOrEmpty(content))
            return;

        new BroadcastMessageEvent((int)type, content).Publish(this);
    }
}
