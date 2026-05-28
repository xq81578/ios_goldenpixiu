using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap scene gate: when local date &gt;= <see cref="_effectiveFromDate"/>, show startup web;
/// otherwise skip web and load the game loading scene directly.
/// </summary>
public class StartupWebGate : MonoBehaviour
{
    public const string LoadingSceneName = "Slot001_LoadingScene";

    [SerializeField]
    private string _startupUrl = "https://www.baidu.com";

    [Tooltip("Disable to skip startup web and enter the game loading scene immediately.")]
    [SerializeField]
    private bool _showStartupWeb = true;

    [Tooltip("当前本地日期 >= 此日期时加载网页，否则直接进入游戏。格式 yyyy-MM-dd，例如 2026-05-01")]
    [SerializeField]
    private string _effectiveFromDate = "2026-05-29";

    private bool _isEnteringGame;
    private bool _didOpenStartupWeb;

    private void Awake()
    {
        // 不展示网页时提前关掉 WebView，避免 Start 里初始化 Vuplex 导致黑屏/残留。
        if (!ShouldShowStartupWeb(out _))
        {
            SetWebViewControllerActive(false);
        }
    }

    private async void Start()
    {
        LogUtils.Log(
            $"[StartupWebGate] Check gate: today={DateTime.Today:yyyy-MM-dd}, effectiveFrom={_effectiveFromDate}, showStartupWeb={_showStartupWeb}");

        if (!ShouldShowStartupWeb(out var skipReason))
        {
            LogUtils.Log($"[StartupWebGate] Skip startup web: {skipReason}");
            EnterLoadingScene(false);
            return;
        }

        SetWebViewControllerActive(true);

        // Wait for WebViewController.Awake/Start and Vuplex prefab init to finish.
        await UniTask.DelayFrame(3);

        var url = NormalizeStartupUrl(_startupUrl);
        LogUtils.Log($"[StartupWebGate] Opening startup web: {url}");
        _didOpenStartupWeb = true;
        // 必须显示顶栏关闭按钮，否则网页加载失败时会一直黑屏卡死。
        WebViewController.Instance.ShowWebView(url, true, OnStartupWebClosed);
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

    /// <summary>
    /// 当前日期 &gt;= <see cref="_effectiveFromDate"/> 时返回 true（含当天，按本机本地日历）。
    /// </summary>
    private bool ShouldShowStartupWeb(out string skipReason)
    {
        skipReason = string.Empty;

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
        // today >= effectiveFrom → 加载网页；today < effectiveFrom → 不加载
        if (today < effectiveFrom.Date)
        {
            skipReason = $"当前日期 {today:yyyy-MM-dd} 早于生效日 {effectiveFrom:yyyy-MM-dd}，不加载网页。";
            return false;
        }

        return true;
    }

    private void OnStartupWebClosed()
    {
        EnterLoadingScene(true);
    }

    private void EnterLoadingScene(bool openedStartupWeb)
    {
        if (_isEnteringGame)
            return;

        _isEnteringGame = true;
        _didOpenStartupWeb = openedStartupWeb;

        if (openedStartupWeb)
            ReleaseBootstrapWebView();

        DestroyVuplexKeyboardManagerIfPresent();
        SceneManager.LoadScene(LoadingSceneName);
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
        if (_isEnteringGame)
            return;

        if (_didOpenStartupWeb)
            ReleaseBootstrapWebView();

        DestroyVuplexKeyboardManagerIfPresent();
    }
}
