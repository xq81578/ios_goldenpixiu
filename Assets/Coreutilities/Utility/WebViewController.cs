using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vuplex.WebView;

public class WebViewController : Singleton<WebViewController>
{
    /// <summary>
    /// WebView的背景Mask
    /// </summary>
    public GameObject Mask => _mask;

    [SerializeField]
    private GameObject _mask;
    [SerializeField]
    private GameObject _headerBar;
    [SerializeField]
    private CanvasWebViewPrefab _canvasWebView;

    [SerializeField]
    private int _loadRetryCount = 3;
    [SerializeField]
    private int _loadRetryDelayMs = 2000;

    [SerializeField]
    private bool _disableWebViewOverscroll = true;

    [SerializeField]
    private bool _showLoadingProgress = true;
    [SerializeField]
    private float _reloadAfterFocusDelaySeconds = 1.2f;
    [SerializeField]
    private float _reloadAfterFocusFallbackDelaySeconds = 3.5f;
    [SerializeField]
    private float _headerBarHeight = 60f;
    [SerializeField]
    private float _headerCloseButtonSize = 144f;
    [SerializeField]
    private float _headerCloseButtonRightInset = 88f;
    [SerializeField]
    private float _headerCloseIconFontSize = 88f;

    private Action _onCloseEvent;
    private Action _onPageLoadedEvent;
    private string _currentUrl;
    private int _currentLoadAttempt;
    private bool _loadFailedHandlerAttached;
    private bool _loadProgressHandlerAttached;
    private bool _isLoadingWebPage;
    private bool _lastLoadFailed;
    private bool _reloadAfterFocusScheduled;
    private GameObject _loadingOverlay;
    private Image _loadingProgressFill;
    private float _displayedLoadProgress;
    private float _currentHeaderReservedHeight;
    private bool _headerCloseAreaConfigured;
    private Button _headerCloseAreaButton;

    private const string HeaderCloseAreaName = "HeaderCloseArea";

#if UNITY_WEBGL
    [DllImport("__Internal")]
    private static extern void UpdateIFrame();
#endif

    protected override void Awake()
    {
        base.Awake();
        ConfigureMobileBrowserMode();
    }

    protected void Start()
    {
        Init();
    }

    private void ConfigureMobileBrowserMode()
    {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
        Web.SetUserAgent(true);

        if (_canvasWebView != null)
        {
            _canvasWebView.Native2DModeEnabled = true;
            _canvasWebView.PixelDensity = 1;
            _canvasWebView.Resolution = 1;
        }
#endif
    }

    private async void InitIFrameWebView()
    {
        // IFrame需要設定攝影機權限，所以打開一個空網頁設定WebCamera權限到IFrame中
        try
        {
#if UNITY_WEBGL
            _canvasWebView.gameObject.SetActive(true);
            await _canvasWebView.WaitUntilInitialized();
            _canvasWebView.WebView.LoadHtml("<html><head></head><body></body></html>");
            UpdateIFrame();
#endif
        }
        catch (Exception e)
        {
            LogUtils.LogWarning($"IFrameWebView Init Error: {e.Message}");
        }
        finally
        {
            _canvasWebView.gameObject.SetActive(false);
        }
    }

    private void Init()
    {
        InitIFrameWebView();
    }

    public void ShowWebView(string url)
    {
        bool showHeaderBar = false;
#if UNITY_EDITOR || UNITY_STANDALONE
        showHeaderBar = true;
#endif

        ShowWebView(url, showHeaderBar, null);
    }

    public void ShowWebViewWithClose(string url)
    {
        ShowWebView(url, true, null);
    }

    public void ShowWebView(string url, Action closeCallback)
    {
        ShowWebView(url, true, closeCallback);
    }

    public void ShowWebView(string url, bool showHeaderBar)
    {
        ShowWebView(url, showHeaderBar, null);
    }

    public void ShowWebView(string url, bool showHeaderBar = true, Action closeCallback = null, Action pageLoadedCallback = null)
    {
        ApplyHeaderLayout(showHeaderBar);
        OpenIFrameWebView(url, closeCallback, pageLoadedCallback);

        _headerBar.SetActive(showHeaderBar);
    }

    private void ApplyHeaderLayout(bool showHeaderBar)
    {
        float safeTop = GetSafeAreaTopInset();
        float reservedHeight = showHeaderBar ? safeTop + _headerBarHeight : 0f;
        _currentHeaderReservedHeight = reservedHeight;

        RectTransform webViewRect = _canvasWebView.GetComponent<RectTransform>();
        webViewRect.offsetMax = new Vector2(0, -reservedHeight);

        if (_headerBar != null)
        {
            RectTransform headerRect = _headerBar.GetComponent<RectTransform>();
            if (headerRect != null)
            {
                headerRect.anchorMin = new Vector2(0, 1);
                headerRect.anchorMax = new Vector2(1, 1);
                headerRect.pivot = new Vector2(0.5f, 1f);
                headerRect.sizeDelta = new Vector2(0, reservedHeight);
                headerRect.anchoredPosition = Vector2.zero;
            }

            Button closeButton = FindHeaderVisualCloseButton();
            if (closeButton != null)
            {
                RectTransform closeButtonRect = closeButton.GetComponent<RectTransform>();
                if (closeButtonRect != null)
                {
                    closeButtonRect.anchorMin = new Vector2(1, 1);
                    closeButtonRect.anchorMax = new Vector2(1, 1);
                    closeButtonRect.pivot = new Vector2(0.5f, 0.5f);
                    closeButtonRect.sizeDelta = new Vector2(_headerCloseButtonSize, _headerCloseButtonSize);
                    closeButtonRect.anchoredPosition = new Vector2(
                        -_headerCloseButtonRightInset,
                        -(safeTop + _headerBarHeight * 0.5f));
                }

                ConfigureHeaderCloseButtonVisual(closeButton);
            }

            ConfigureHeaderCloseArea();
        }

        if (_loadingOverlay != null)
        {
            RectTransform overlayRect = _loadingOverlay.GetComponent<RectTransform>();
            overlayRect.offsetMax = new Vector2(0f, -_currentHeaderReservedHeight);
        }
    }

    private static float GetSafeAreaTopInset()
    {
        float topInset = Screen.height - Screen.safeArea.yMax;
        return Mathf.Max(0f, topInset);
    }

    private void ConfigureHeaderCloseArea()
    {
        if (_headerCloseAreaConfigured || _headerBar == null)
            return;

        Button legacyHeaderButton = _headerBar.GetComponent<Button>();
        if (legacyHeaderButton != null)
            Destroy(legacyHeaderButton);

        GameObject closeAreaObject = new GameObject(HeaderCloseAreaName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeAreaObject.layer = _headerBar.layer;
        closeAreaObject.transform.SetParent(_headerBar.transform, false);
        closeAreaObject.transform.SetAsLastSibling();

        RectTransform closeAreaRect = closeAreaObject.GetComponent<RectTransform>();
        closeAreaRect.anchorMin = Vector2.zero;
        closeAreaRect.anchorMax = Vector2.one;
        closeAreaRect.offsetMin = Vector2.zero;
        closeAreaRect.offsetMax = Vector2.zero;

        Image closeAreaImage = closeAreaObject.GetComponent<Image>();
        closeAreaImage.color = new Color(1f, 1f, 1f, 0f);
        closeAreaImage.raycastTarget = true;

        _headerCloseAreaButton = closeAreaObject.GetComponent<Button>();
        _headerCloseAreaButton.transition = Selectable.Transition.None;
        _headerCloseAreaButton.targetGraphic = closeAreaImage;
        _headerCloseAreaButton.onClick.AddListener(CloseWebView);
        _headerCloseAreaConfigured = true;
    }

    private Button FindHeaderVisualCloseButton()
    {
        if (_headerBar == null)
            return null;

        foreach (Button button in _headerBar.GetComponentsInChildren<Button>(true))
        {
            if (button == null
                || button == _headerCloseAreaButton
                || button.gameObject == _headerBar
                || button.gameObject.name == HeaderCloseAreaName)
            {
                continue;
            }

            return button;
        }

        return null;
    }

    private void ConfigureHeaderCloseButtonVisual(Button closeButton)
    {
        if (closeButton == null)
            return;

        closeButton.transition = Selectable.Transition.None;

        Image buttonImage = closeButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = Color.clear;
            buttonImage.raycastTarget = false;
        }

        TMP_Text closeText = closeButton.GetComponentInChildren<TMP_Text>(true);
        if (closeText == null)
            return;

        closeText.text = "X";
        closeText.fontSize = _headerCloseIconFontSize;
        closeText.color = Color.white;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.raycastTarget = false;

        RectTransform textRect = closeText.GetComponent<RectTransform>();
        if (textRect == null)
            return;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    public async void OpenIFrameWebView(string url, Action closeEvent, Action pageLoadedEvent = null)
    {
        _onCloseEvent = closeEvent;
        _onPageLoadedEvent = pageLoadedEvent;
        _currentUrl = url;
        _currentLoadAttempt = 0;
        _mask.SetActive(true);
        ShowLoadingOverlay(0.04f);
        _canvasWebView.gameObject.SetActive(true);
        await _canvasWebView.WaitUntilInitialized();

        AttachLoadFailedHandler();
        AttachLoadProgressHandler();
        ConfigurePageLoadScripts();

#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
        if (_canvasWebView.WebView is IWithSettableUserAgent webViewWithUserAgent)
        {
            webViewWithUserAgent.SetUserAgent(true);
        }
#endif

        LoadCurrentUrl();
    }

    public void CloseIFrameWebView()
    {
        _currentUrl = null;
        _onPageLoadedEvent = null;
        DetachLoadFailedHandler();
        DetachLoadProgressHandler();
        HideWebViewUi();

        if (TryLoadEmptyHtml())
        {
            // cleared in-page content while webview is still alive
        }

        var closeCallback = _onCloseEvent;
        _onCloseEvent = null;
        closeCallback?.Invoke();
    }

    public void CloseWebView()
    {
        CloseIFrameWebView();
        _headerBar.SetActive(false);
    }

    /// <summary>
    /// Call before leaving a scene that used WebView (e.g. bootstrap).
    /// Vuplex KeyboardManager uses DontDestroyOnLoad and must be destroyed explicitly.
    /// </summary>
    public void ReleaseBeforeSceneTransition()
    {
        _onCloseEvent = null;
        _onPageLoadedEvent = null;
        _currentUrl = null;
        DetachLoadFailedHandler();
        DetachLoadProgressHandler();
        HideWebViewUi();
        _headerBar.SetActive(false);

        if (_canvasWebView != null
            && _canvasWebView.WebView != null
            && !_canvasWebView.WebView.IsDisposed)
        {
            _canvasWebView.WebView.Dispose();
        }

        DestroyVuplexKeyboardManager();
    }

    private void HideWebViewUi()
    {
        SetLoadingOverlayVisible(false);

        if (_mask != null)
            _mask.SetActive(false);

        if (_canvasWebView != null)
            _canvasWebView.gameObject.SetActive(false);
    }

    private bool TryLoadEmptyHtml()
    {
        if (_canvasWebView == null || _canvasWebView.WebView == null || _canvasWebView.WebView.IsDisposed)
            return false;

        _canvasWebView.WebView.LoadHtml("<html><head></head><body></body></html>");
        return true;
    }

    private void AttachLoadFailedHandler()
    {
        if (_loadFailedHandlerAttached || _canvasWebView?.WebView == null)
            return;

        _canvasWebView.WebView.LoadFailed += OnWebViewLoadFailed;
        _loadFailedHandlerAttached = true;
    }

    private void DetachLoadFailedHandler()
    {
        if (!_loadFailedHandlerAttached || _canvasWebView?.WebView == null)
            return;

        _canvasWebView.WebView.LoadFailed -= OnWebViewLoadFailed;
        _loadFailedHandlerAttached = false;
    }

    private void AttachLoadProgressHandler()
    {
        if (_loadProgressHandlerAttached || _canvasWebView?.WebView == null)
            return;

        _canvasWebView.WebView.LoadProgressChanged += OnWebViewLoadProgressChanged;
        _loadProgressHandlerAttached = true;
    }

    private void DetachLoadProgressHandler()
    {
        if (!_loadProgressHandlerAttached || _canvasWebView?.WebView == null)
            return;

        _canvasWebView.WebView.LoadProgressChanged -= OnWebViewLoadProgressChanged;
        _loadProgressHandlerAttached = false;
    }

    private void ConfigurePageLoadScripts()
    {
        if (!_disableWebViewOverscroll || _canvasWebView?.WebView == null)
            return;

        if (!_canvasWebView.WebView.PageLoadScripts.Contains(DisableOverscrollScript))
            _canvasWebView.WebView.PageLoadScripts.Add(DisableOverscrollScript);
    }

    private void LoadCurrentUrl()
    {
        if (_canvasWebView == null
            || _canvasWebView.WebView == null
            || _canvasWebView.WebView.IsDisposed
            || string.IsNullOrEmpty(_currentUrl))
        {
            return;
        }

        LogUtils.Log($"[WebViewController] Loading web url: {_currentUrl}, attempt={_currentLoadAttempt + 1}");
        _isLoadingWebPage = true;
        _lastLoadFailed = false;
        _displayedLoadProgress = 0f;
        ShowLoadingOverlay(0.08f);
        _canvasWebView.WebView.LoadUrl(_currentUrl);
    }

    private void OnWebViewLoadProgressChanged(object sender, ProgressChangedEventArgs eventArgs)
    {
        if (eventArgs == null)
            return;

        switch (eventArgs.Type)
        {
            case ProgressChangeType.Started:
                _isLoadingWebPage = true;
                _lastLoadFailed = false;
                _displayedLoadProgress = 0f;
                ShowLoadingOverlay(0.08f);
                break;
            case ProgressChangeType.Updated:
                UpdateLoadingProgress(Mathf.Clamp(eventArgs.Progress, 0.08f, 0.92f));
                break;
            case ProgressChangeType.Finished:
                _isLoadingWebPage = false;
                _lastLoadFailed = false;
                CompleteLoadingOverlay();
                InvokePageLoadedOnce();
                break;
            case ProgressChangeType.Failed:
                _isLoadingWebPage = false;
                _lastLoadFailed = true;
                UpdateLoadingProgress(0.12f);
                break;
        }
    }

    private async void OnWebViewLoadFailed(object sender, LoadFailedEventArgs eventArgs)
    {
        if (eventArgs == null || eventArgs.Url != _currentUrl)
            return;

        _isLoadingWebPage = false;
        _lastLoadFailed = true;

        if (_currentLoadAttempt < _loadRetryCount)
        {
            _currentLoadAttempt++;
            LogUtils.LogWarning(
                $"[WebViewController] Web load failed ({eventArgs.NativeErrorCode}), retry {_currentLoadAttempt}/{_loadRetryCount}: {eventArgs.Url}");
            await UniTask.Delay(_loadRetryDelayMs);
            LoadCurrentUrl();
            return;
        }

        LogUtils.LogWarning(
            $"[WebViewController] Web load failed after retries ({eventArgs.NativeErrorCode}): {eventArgs.Url}");
        if (_headerBar != null)
            _headerBar.SetActive(true);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus
            || string.IsNullOrEmpty(_currentUrl)
            || _canvasWebView == null
            || !_canvasWebView.gameObject.activeInHierarchy)
        {
            return;
        }

        if (_isLoadingWebPage || _lastLoadFailed)
            ScheduleReloadAfterFocus().Forget();
    }

    private async UniTaskVoid ScheduleReloadAfterFocus()
    {
        if (_reloadAfterFocusScheduled)
            return;

        _reloadAfterFocusScheduled = true;
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_reloadAfterFocusDelaySeconds));
            if (ShouldReloadAfterFocus())
            {
                LogUtils.Log("[WebViewController] Reloading web url after app focus returned.");
                LoadCurrentUrl();
            }

            await UniTask.Delay(TimeSpan.FromSeconds(_reloadAfterFocusFallbackDelaySeconds));
            if (ShouldReloadAfterFocus())
            {
                LogUtils.Log("[WebViewController] Reloading web url after app focus fallback delay.");
                LoadCurrentUrl();
            }
        }
        finally
        {
            _reloadAfterFocusScheduled = false;
        }
    }

    private bool ShouldReloadAfterFocus()
    {
        return !string.IsNullOrEmpty(_currentUrl)
               && _canvasWebView != null
               && _canvasWebView.gameObject.activeInHierarchy
               && (_isLoadingWebPage || _lastLoadFailed);
    }

    private void InvokePageLoadedOnce()
    {
        var pageLoadedCallback = _onPageLoadedEvent;
        _onPageLoadedEvent = null;
        pageLoadedCallback?.Invoke();
    }

    private void ShowLoadingOverlay(float progress)
    {
        if (!_showLoadingProgress)
            return;

        EnsureLoadingOverlay();
        SetLoadingOverlayVisible(true);
        UpdateLoadingProgress(progress);
    }

    private void CompleteLoadingOverlay()
    {
        UpdateLoadingProgress(1f);
        HideLoadingOverlaySoon().Forget();
    }

    private async UniTaskVoid HideLoadingOverlaySoon()
    {
        await UniTask.Delay(250);
        if (!_isLoadingWebPage)
            SetLoadingOverlayVisible(false);
    }

    private void UpdateLoadingProgress(float progress)
    {
        _displayedLoadProgress = Mathf.Clamp01(Mathf.Max(_displayedLoadProgress, progress));

        if (_loadingProgressFill != null)
            _loadingProgressFill.fillAmount = _displayedLoadProgress;
    }

    private void SetLoadingOverlayVisible(bool visible)
    {
        if (_loadingOverlay != null)
            _loadingOverlay.SetActive(visible);

        if (!visible)
            _displayedLoadProgress = 0f;
    }

    private void EnsureLoadingOverlay()
    {
        if (_loadingOverlay != null)
            return;

        Transform parent = _mask != null ? _mask.transform.parent : transform;

        _loadingOverlay = new GameObject("WebViewLoadingOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _loadingOverlay.layer = gameObject.layer;
        _loadingOverlay.transform.SetParent(parent, false);
        _loadingOverlay.transform.SetAsLastSibling();

        RectTransform overlayRect = _loadingOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = new Vector2(0f, -_currentHeaderReservedHeight);

        Image background = _loadingOverlay.GetComponent<Image>();
        background.color = Color.white;
        background.raycastTarget = true;

        GameObject trackObject = new GameObject("ProgressTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackObject.layer = _loadingOverlay.layer;
        trackObject.transform.SetParent(_loadingOverlay.transform, false);

        RectTransform trackRect = trackObject.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.16f, 0.5f);
        trackRect.anchorMax = new Vector2(0.84f, 0.5f);
        trackRect.sizeDelta = new Vector2(0f, 6f);
        trackRect.anchoredPosition = Vector2.zero;

        Image trackImage = trackObject.GetComponent<Image>();
        trackImage.color = new Color(0.86f, 0.88f, 0.9f, 1f);
        trackImage.raycastTarget = false;

        GameObject fillObject = new GameObject("ProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.layer = _loadingOverlay.layer;
        fillObject.transform.SetParent(trackObject.transform, false);

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        _loadingProgressFill = fillObject.GetComponent<Image>();
        _loadingProgressFill.color = new Color(0.12f, 0.46f, 0.95f, 1f);
        _loadingProgressFill.raycastTarget = false;
        _loadingProgressFill.type = Image.Type.Filled;
        _loadingProgressFill.fillMethod = Image.FillMethod.Horizontal;
        _loadingProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _loadingProgressFill.fillAmount = 0f;
    }

    private const string DisableOverscrollScript = @"
(() => {
    if (window.__unityDisableOverscrollInstalled) {
        return;
    }
    window.__unityDisableOverscrollInstalled = true;

    const style = document.createElement('style');
    style.textContent = `
        html, body {
            overscroll-behavior: none !important;
        }
    `;
    document.head.appendChild(style);

    let startY = 0;
    document.addEventListener('touchstart', event => {
        if (event.touches.length === 1) {
            startY = event.touches[0].clientY;
        }
    }, { passive: true });

    const canScrollElement = (element, deltaY) => {
        if (!element) {
            return false;
        }

        const style = window.getComputedStyle(element);
        const overflowY = style.overflowY;
        const isRootScroller = element === document.scrollingElement
            || element === document.documentElement
            || element === document.body;
        const isScrollable = (isRootScroller || overflowY === 'auto' || overflowY === 'scroll' || overflowY === 'overlay')
            && element.scrollHeight > element.clientHeight + 1;

        if (!isScrollable) {
            return false;
        }

        if (deltaY > 0) {
            return element.scrollTop > 0;
        }

        if (deltaY < 0) {
            return element.scrollTop + element.clientHeight < element.scrollHeight - 1;
        }

        return false;
    };

    const canAnyParentScroll = (element, deltaY) => {
        while (element && element !== document.body && element !== document.documentElement) {
            if (canScrollElement(element, deltaY)) {
                return true;
            }
            element = element.parentElement;
        }

        return canScrollElement(document.scrollingElement || document.documentElement, deltaY);
    };

    document.addEventListener('touchmove', event => {
        if (event.touches.length !== 1) {
            return;
        }

        const currentY = event.touches[0].clientY;
        const deltaY = currentY - startY;

        if (!canAnyParentScroll(event.target, deltaY)) {
            event.preventDefault();
        }
    }, { passive: false });
})();
";

    private static void DestroyVuplexKeyboardManager()
    {
        var keyboardManager = GameObject.Find("WebView Keyboard Manager");
        if (keyboardManager != null)
        {
            Destroy(keyboardManager);
        }
    }




    #region  測試用
    [Button]
    public void TestOpenWebView()
    {
        ShowWebView("https://tw.yahoo.com/", true, CloseWebView);
    }
    #endregion
}
