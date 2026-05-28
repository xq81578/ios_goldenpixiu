using System;
using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using UnityEngine;
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

    private Action _onCloseEvent;

#if UNITY_WEBGL
    [DllImport("__Internal")]
    private static extern void UpdateIFrame();
#endif

    protected void Start()
    {
        Init();
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

    public void ShowWebView(string url, bool showHeaderBar = true, Action closeCallback = null)
    {
        RectTransform rectTransform = _canvasWebView.GetComponent<RectTransform>();

        if (showHeaderBar)
        {
            rectTransform.offsetMax = new Vector2(0, -50);
        }
        else
        {
            rectTransform.offsetMax = new Vector2(0, 0);
        }

        rectTransform = _canvasWebView.GetComponent<RectTransform>();
        OpenIFrameWebView(url, closeCallback);

        _headerBar.SetActive(showHeaderBar);
    }

    public async void OpenIFrameWebView(string url, Action closeEvent)
    {
        _onCloseEvent = closeEvent;
        _mask.SetActive(true);
        _canvasWebView.gameObject.SetActive(true);
        await _canvasWebView.WaitUntilInitialized();
        _canvasWebView.WebView.LoadUrl(url);
    }

    public void CloseIFrameWebView()
    {
        _onCloseEvent?.Invoke();
        _mask.SetActive(false);
        _canvasWebView.gameObject.SetActive(false);
        _canvasWebView.WebView.LoadHtml("<html><head></head><body></body></html>");
    }

    public void CloseWebView()
    {
        CloseIFrameWebView();
        _headerBar.SetActive(false);
    }




    #region  測試用
    [Button]
    public void TestOpenWebView()
    {
        ShowWebView("https://tw.yahoo.com/", true, CloseWebView);
    }
    #endregion
}
