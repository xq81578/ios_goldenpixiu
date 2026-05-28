// Copyright (c) 2025 Vuplex Inc. All rights reserved.
//
// Licensed under the Vuplex Commercial Software Library License, you may
// not use this file except in compliance with the License. You may obtain
// a copy of the License at
//
//     https://vuplex.com/commercial-library-license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using Vuplex.WebView.Internal;

namespace Vuplex.WebView {

    /// <summary>
    /// WebGLWebView is the IWebView implementation used by 2D WebView for WebGL.
    /// It also includes additional APIs for WebGL-specific functionality.
    /// </summary>
    public class WebGLWebView : BaseWebView,
                                IWebView,
                                IWithNative2DMode {

        /// <summary>
        /// Gets the unique `id` attribute of the webview's &lt;iframe&gt; element.
        /// </summary>
        /// <example>
        /// <code>
        /// await canvasWebViewPrefab.WaitUntilInitialized();
        /// #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///     var webGLWebView = canvasWebViewPrefab.WebView as WebGLWebView;
        ///     Debug.Log("IFrame ID: " + webGLWebView.IFrameElementID);
        /// #endif
        /// </code>
        /// </example>
        public string IFrameElementID { get; private set; }

        /// <see cref="IWithNative2DMode"/>
        public bool Native2DModeEnabled { get => _native2DModeEnabled; }

        public WebPluginType PluginType { get; } = WebPluginType.WebGL;

        /// <see cref="IWithNative2DMode"/>
        public Rect Rect { get => _rect; }

        /// <see cref="IWithNative2DMode"/>
        public bool Visible { get => _visible; }

        /// <seealso cref="IWithNative2DMode"/>
        public void BringToFront() {

            _assertValidState();
            _assertNative2DModeEnabled();
            WebView_bringToFront(_nativeWebViewPtr);
        }

        /// <summary>
        /// Indicates whether 2D WebView can access the content in the
        /// webview's iframe. If the iframe's content can't be accessed,
        /// then most of the IWebView APIs become disabled. For more
        /// information, please see [this article](https://support.vuplex.com/articles/webgl-limitations#cross-origin-limitation).
        /// </summary>
        /// <example>
        /// <code>
        /// await canvasWebViewPrefab.WaitUntilInitialized();
        /// #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///     var webGLWebView = canvasWebViewPrefab.WebView as WebGLWebView;
        ///     if (webGLWebView.CanAccessIFrameContent()) {
        ///         Debug.Log("The iframe content can be accessed 👍");
        ///     }
        /// #endif
        /// </code>
        /// </example>
        public bool CanAccessIFrameContent() {

            _assertValidState();
            return WebView_canAccessIFrameContent(_nativeWebViewPtr);
        }

        public override Task<bool> CanGoBack() => _canGoBackOrForward("CanGoBack");

        public override Task<bool> CanGoForward() => _canGoBackOrForward("CanGoForward");

        public override Task<byte[]> CaptureScreenshot() {

            WebGLWarnings.LogCaptureScreenshotError();
            return Task.FromResult(new byte[0]);
        }

        public override void Click(int xInPixels, int yInPixels, bool preventStealingFocus = false) {

            if (_verifyIFrameCanBeAccessed("Click")) {
                base.Click(xInPixels, yInPixels, preventStealingFocus);
            }
        }

        public override void Copy() => WebGLWarnings.LogCopyError();

        public override void Cut() => WebGLWarnings.LogCutError();

        public override void ExecuteJavaScript(string javaScript, Action<string> callback) {

            _assertValidState();
            if (_verifyIFrameCanBeAccessed("ExecuteJavaScript")) {
                var result = WebView_executeJavaScriptSync(_nativeWebViewPtr, javaScript);
                callback?.Invoke(result);
            }
        }

        /// <summary>
        /// Executes the given JavaScript locally in the Unity app's window and returns the result.
        /// </summary>
        /// <example>
        /// <code>
        /// #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///     // Gets the Unity app's web page title.
        ///     var title = WebGLWebView.ExecuteJavaScriptLocally("document.title");
        ///     Debug.Log("Title: " + title);
        /// #endif
        /// </code>
        /// </example>
        public static string ExecuteJavaScriptLocally(string javaScript) => WebView_executeJavaScriptLocally(javaScript);

        /// <summary>
        /// Focuses the Unity game view, which enables the Unity application to detect keyboard input.
        /// </summary>
        /// <remarks>
        /// When the user clicks on a webview, their web browser shifts input focus from the Unity game view
        /// to the webview. While the webview is focused, the Unity application is unable to detect keyboard input
        /// (i.e. keyboard APIs like Input.GetKeyDown() don't work). By default, the Unity game view isn't refocused until the user
        /// clicks on it, at which point the Unity application is once again able to detect keyboard input.
        /// However, the application can optionally use this method to programmatically refocus the Unity game view
        /// at any time without requiring the user to actually click on it.
        /// </remarks>
        /// <remarks>
        /// It is not recommended to call this method on each frame in an Update() loop because that may prevent
        /// the webview from being able to handle mouse and keyboard input correctly. If you wish for
        /// the Unity game view to be focused automatically, please use <see cref="SetFocusUnityOnHover"/> instead.
        /// </remarks>
        /// <example>
        /// <code>
        /// #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///     WebGLWebView.FocusUnity();
        /// #endif
        /// </code>
        /// </example>
        /// <seealso cref="SetFocusUnityOnHover"/>
        public static void FocusUnity() => WebView_focusUnity();

        public override Task<byte[]> GetRawTextureData() {

            WebGLWarnings.LogGetRawTextureDataError();
            return Task.FromResult(new byte[0]);
        }

        public override void GoBack() {

            if (_verifyIFrameCanBeAccessed("GoBack")) {
                base.GoBack();
            }
        }

        public override void GoForward() {

            if (_verifyIFrameCanBeAccessed("GoForward")) {
                base.GoForward();
            }
        }

        public Task Init(int width, int height) {

            var message = "2D WebView for WebGL only supports 2D, so Native 2D Mode must be enabled." + WebGLWarnings.GetArticleLinkText(false);
            WebViewLogger.LogError(message);
            throw new NotImplementedException(message);
        }

        /// <see cref="IWithNative2DMode"/>
        public async Task InitInNative2DMode(Rect rect) {

            await _initInNative2DModeBase(rect);
            // Set IFrameElementID *after* base.Init() because it sets gameObject.name.
            IFrameElementID = gameObject.name;
            // Prior to Unity 2019.3, Unity's UI used a pixel density of
            // 1 instead of using window.devicePixelRatio.
            #if UNITY_2019_3_OR_NEWER
                var ignoreDevicePixelRatio = false;
            #else
                var ignoreDevicePixelRatio = true;
            #endif
            _nativeWebViewPtr = WebView_newInNative2DMode(
                gameObject.name,
                (int)rect.x,
                (int)rect.y,
                (int)rect.width,
                (int)rect.height,
                ignoreDevicePixelRatio
            );
            if (_nativeWebViewPtr == IntPtr.Zero) {
                throw new TrialExpiredException("Your trial of 2D WebView for WebGL has expired. Please purchase a license to continue using it.");
            }
        }

        public static WebGLWebView Instantiate() => new GameObject().AddComponent<WebGLWebView>();

        public override void LoadHtml(string html) {

            WebGLWarnings.LogLoadHtmlWarning();
            base.LoadHtml(html);
        }

        public override void LoadUrl(string url, Dictionary<string, string> additionalHttpHeaders) {

            WebViewLogger.LogWarning("LoadUrl(url, headers) was called, but 2D WebView for WebGL is unable to send additional headers when loading a URL due to browser limitations. So, the URL will be loaded without additional headers using LoadUrl(url) instead.");
            LoadUrl(url);
        }

        public override void Paste() => WebGLWarnings.LogPasteError();

        public override void PostMessage(string message) {

            _assertValidState();
            WebView_postMessage(_nativeWebViewPtr, message);
        }

        public override void Scroll(int scrollDeltaXInPixels, int scrollDeltaYInPixels) {

            if (_verifyIFrameCanBeAccessed("Scroll")) {
                base.Scroll(scrollDeltaXInPixels, scrollDeltaYInPixels);
            }
        }

        public override void Scroll(Vector2 normalizedScrollDelta, Vector2 normalizedPoint) {

            if (_verifyIFrameCanBeAccessed("Scroll")) {
                base.Scroll(normalizedScrollDelta, normalizedPoint);
            }
        }

        public override void SendKey(string key) {

            if (_verifyIFrameCanBeAccessed("SendKey")) {
                base.SendKey(key);
            }
        }

        public static void SetCameraAndMicrophoneEnabled(bool enabled) => WebView_setCameraAndMicrophoneEnabled(enabled);

        /// <summary>
        /// Overrides the value of `devicePixelRatio` that 2D WebView uses to determine the correct
        /// size and coordinates of webviews.
        /// </summary>
        /// <summary>
        /// Starting in Unity 2019.3, Unity scales the UI by default based on the browser's window.devicePixelRatio
        /// value. However, it's possible for an application to override the `devicePixelRatio` value
        /// by passing a value for `config.devicePixelRatio` to Unity's `createUnityInstance()` JavaScript function. In some
        /// versions of Unity, the default index.html template sets `config.devicePixelRatio = 1` on mobile.
        /// In order for 2D WebView to size and position webviews correctly, it must determine the value of `devicePixelRatio`
        /// to use. Since there's no API to get a reference to the Unity instance that the application created with `createUnityInstance()`,
        /// 2D WebView tries to detect if `config.devicePixelRatio` was passed to `createUnityInstance()` by checking for the presence of a
        /// global `config` JavaScript variable. If there is a global variable named `config` with a `devicePixelRatio` property, then 2D WebView
        /// uses that value, otherwise it defaults to using `window.devicePixelRatio`. This approach works for Unity's default
        /// index.html templates, but it may not work if the application uses a custom HTML template or changes the name of the `config`
        /// variable in the default template. In those cases, the application can use this method to pass the overridden value of `devicePixelRatio` to
        /// 2D WebView.
        /// </summary>
        /// <example>
        /// <code>
        /// void Awake() {
        ///     #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///         WebGLWebView.SetDevicePixelRatio(1);
        ///     #endif
        /// }
        /// </code>
        /// </example>
        public static void SetDevicePixelRatio(float devicePixelRatio) {

            #if UNITY_2019_3_OR_NEWER
                WebView_setDevicePixelRatio(devicePixelRatio);
            #else
                WebViewLogger.LogWarning("The call to WebGLWebView.SetDevicePixelRatio() will be ignored because a version of Unity older than 2019.3 is being used, which always uses a devicePixelRatio of 1.");
            #endif
        }

        /// <summary>
        /// Sets whether 2D WebView automatically refocuses the Unity game view when the user's
        /// mouse leaves the webview and hovers over the Unity game view. The default is `false`.
        /// </summary>
        /// <remarks>
        /// When the user clicks on a webview, their web browser shifts input focus from the Unity game view
        /// to the webview. While the webview is focused, the Unity application is unable to detect keyboard input
        /// (i.e. keyboard APIs like Input.GetKeyDown() don't work). By default, the Unity game view isn't refocused until the user
        /// clicks on it, at which point the Unity application is once again able to detect keyboard input.
        /// However, the application can optionally use this method to make it so that the Unity game view is automatically
        /// refocused when the user's mouse leaves the webview and hovers over the Unity game view
        /// (i.e. without requiring them to actually click on the Unity game view).
        /// </remarks>
        /// <example>
        /// <code>
        /// void Awake() {
        ///     #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///         WebGLWebView.SetFocusUnityOnHover(true);
        ///     #endif
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="FocusUnity"/>
        public static void SetFocusUnityOnHover(bool enabled) => WebView_setFocusUnityOnHover(enabled);

        /// <summary>
        /// Sets whether web pages can use the JavaScript Fullscreen API to make an HTML element occupy the device's
        /// entire screen. The default is `true`, meaning that the JavaScript Fullscreen API is enabled by default.
        /// </summary>
        /// <example>
        /// <code>
        /// void Awake() {
        ///     #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///         WebGLWebView.SetFullscreenEnabled(false);
        ///     #endif
        /// }
        /// </code>
        /// </example>
        public static void SetFullscreenEnabled(bool enabled) => WebView_setFullscreenEnabled(enabled);

        /// <summary>
        /// Sets whether the JavaScript Geolocation API is enabled for webviews. The default is `false`.
        /// </summary>
        /// <example>
        /// <code>
        /// void Awake() {
        ///     #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///         WebGLWebView.SetGeolocationEnabled(true);
        ///     #endif
        /// }
        /// </code>
        /// </example>
        public static void SetGeolocationEnabled(bool enabled) => WebView_setGeolocationEnabled(enabled);

        public void SetNativeZoomEnabled(bool enabled) {

            WebViewLogger.LogWarning("2D WebView for WebGL doesn't support native pinch-to-zoom gestures, so the call to IWithNative2DMode.SetNativeZoomEnabled() will be ignored.");
        }

        /// <see cref="IWithNative2DMode"/>
        public void SetRect(Rect rect) {

            _assertValidState();
            _assertNative2DModeEnabled();
            _rect = rect;
            WebView_setRect(_nativeWebViewPtr, (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        }

        /// <summary>
        /// Explicitly sets the HTML element that 2D WebView should use as the Unity app container.
        /// 2D WebView automatically detects the Unity app container element if its ID is set to one of the default values of
        /// "unityContainer" or "unity-container". However, if your app uses a custom WebGL template that
        /// uses a different ID for the container element, you must call this method to set the container element ID.
        /// 2D WebView for WebGL works by adding &lt;iframe&gt; elements to the app container, so it's unable to function correctly
        /// if it's unable to find the Unity app container element. For more details, see <see href="https://support.vuplex.com/articles/webgl-limitations#templates">this article</see>.
        /// </summary>
        /// <example>
        /// <code>
        /// void Awake() {
        ///     #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///         WebGLWebView.SetUnityContainerElementID("your-custom-id");
        ///     #endif
        /// }
        /// </code>
        /// </example>
        public static void SetUnityContainerElementID(string containerID) => WebView_setUnityContainerElementID(containerID);

        /// <summary>
        /// Sets whether screen sharing is enabled for webviews. The default is `false`.
        /// </summary>
        /// <example>
        /// <code>
        /// void Awake() {
        ///     #if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR
        ///         WebGLWebView.SetScreenSharingEnabled(true);
        ///     #endif
        /// }
        /// </code>
        /// </example>
        public static void SetScreenSharingEnabled(bool enabled) => WebView_setScreenSharingEnabled(enabled);

        /// <see cref="IWithNative2DMode"/>
        public void SetVisible(bool visible) {

            _assertValidState();
            _assertNative2DModeEnabled();
            _visible = visible;
            WebView_setVisible(_nativeWebViewPtr, visible);
        }

        public override void ZoomIn() => WebGLWarnings.LogZoomInError();

        public override void ZoomOut() => WebGLWarnings.LogZoomOutError();

    #region Non-public members
        readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

        Task<bool> _canGoBackOrForward(string methodName) {

            _assertValidState();
            if (_verifyIFrameCanBeAccessed(methodName)) {
                WebGLWarnings.LogCanGoBackOrForwardWarning();
                return Task.FromResult(WebView_canGoBackOrForward(_nativeWebViewPtr));
            }
            return Task.FromResult(false);
        }

        bool _verifyIFrameCanBeAccessed(string methodName) {

            if (CanAccessIFrameContent()) {
                return true;
            }
            // Log an error instead of throwing an exception because
            // exceptions are disabled by default for WebGL.
            WebGLWarnings.LogIFrameContentAccessWarning(methodName);
            return false;
        }

        [DllImport(_dllName)]
        static extern void WebView_bringToFront(IntPtr webViewPtr);

        [DllImport(_dllName)]
        static extern bool WebView_canAccessIFrameContent(IntPtr webViewPtr);

        [DllImport(_dllName)]
        static extern bool WebView_canGoBackOrForward(IntPtr webViewPtr);

        [DllImport(_dllName)]
        static extern string WebView_executeJavaScriptSync(IntPtr webViewPtr, string javaScript);

        [DllImport(_dllName)]
        static extern string WebView_executeJavaScriptLocally(string javaScript);

        [DllImport(_dllName)]
        static extern string WebView_focusUnity();

        [DllImport(_dllName)]
        static extern IntPtr WebView_newInNative2DMode(string gameObjectName, int x, int y, int width, int height, bool ignoreDevicePixelRatio);

        [DllImport (_dllName)]
        static extern void WebView_postMessage(IntPtr webViewPtr, string message);

        [DllImport(_dllName)]
        static extern void WebView_setCameraAndMicrophoneEnabled(bool enabled);

        [DllImport (_dllName)]
        static extern void WebView_setDevicePixelRatio(float devicePixelRatio);

        [DllImport (_dllName)]
        static extern void WebView_setFocusUnityOnHover(bool enabled);

        [DllImport (_dllName)]
        static extern void WebView_setFullscreenEnabled(bool enabled);

        [DllImport (_dllName)]
        static extern void WebView_setGeolocationEnabled(bool enabled);

        [DllImport (_dllName)]
        static extern void WebView_setRect(IntPtr webViewPtr, int x, int y, int width, int height);

        [DllImport (_dllName)]
        static extern void WebView_setScreenSharingEnabled(bool enabled);

        [DllImport (_dllName)]
        static extern void WebView_setUnityContainerElementID(string containerId);

        [DllImport (_dllName)]
        static extern void WebView_setVisible(IntPtr webViewPtr, bool visible);
    #endregion

        // Added in v3.15, deprecated in v4.10.
        [Obsolete("WebGLWebView.SetUnityContainerElementId() is now deprecated because it has been renamed to SetUnityContainerElementID(). Please switch to SetUnityContainerElementID().")]
        public static void SetUnityContainerElementId(string containerID) => SetUnityContainerElementID(containerID);
    }
}
#else
namespace Vuplex.WebView {
    [System.Obsolete("To use the WebGLWebView class, you must use the directive `#if UNITY_WEBGL && !UNITY_EDITOR` like described here: https://support.vuplex.com/articles/how-to-call-platform-specific-apis#webgl . Note: WebGLWebView isn't actually obsolete. This compiler error just reports it's obsolete because 3D WebView generated the error with System.ObsoleteAttribute.", true)]
    public class WebGLWebView {}
}
#endif
