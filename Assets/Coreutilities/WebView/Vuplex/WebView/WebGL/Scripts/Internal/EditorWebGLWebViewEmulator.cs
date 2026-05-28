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
#if UNITY_WEBGL && UNITY_EDITOR
#pragma warning disable CS0618
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Vuplex.WebView.Internal;

namespace Vuplex.WebView {

    // Hooks into the partial methods of MockWebView and StandaloneWebView
    // in order to log WebGL-related warnings while running in the editor.
    //
    // - This file can't be moved to the /Editor folder because it must be
    //   compiled into the same assembly as StandaloneWebView.cs and MockWebView.cs
    //
    // - Warnings aren't logged for Click(), Scroll(), or SendKey()
    //   because those methods aren't normally called when interacting with the WebGL webview
    //   in Native 2D Mode, but they are called for StandaloneWebView and MockWebView because
    //   they don't implement IWithNative2DMode.

// It's necessary to explicitly exclude the Linux editor because ConditionalCompilationUtility
// doesn't execute when running in headless mode (i.e. for CI/CD).
#if VUPLEX_STANDALONE && !UNITY_EDITOR_LINUX
    partial class StandaloneWebView {
#else
    partial class MockWebView {
#endif

        partial void OnCanGoBack() {

            _logIFrameContentAccessWarningIfNeeded("CanGoBack");
            WebGLWarnings.LogCanGoBackOrForwardWarning();
        }

        partial void OnCanGoForward() {

            _logIFrameContentAccessWarningIfNeeded("CanGoBack");
            WebGLWarnings.LogCanGoBackOrForwardWarning();
        }

        partial void OnCaptureScreenshot() => WebGLWarnings.LogCaptureScreenshotError();

        partial void OnCopy() => WebGLWarnings.LogCopyError();

        partial void OnCut() => WebGLWarnings.LogCutError();

        partial void OnExecuteJavaScript() => _logIFrameContentAccessWarningIfNeeded("ExecuteJavaScript");

        partial void OnGetRawTextureData() => WebGLWarnings.LogGetRawTextureDataError();

        partial void OnGoBack() => _logIFrameContentAccessWarningIfNeeded("GoBack");

        partial void OnGoForward() => _logIFrameContentAccessWarningIfNeeded("GoForward");

        partial void OnLoadHtml() {

            if (_ignoreNextLoadHtmlInvocation) {
                _ignoreNextLoadHtmlInvocation = false;
                return;
            }
            _iframeMayBeInaccessible = true;
            WebGLWarnings.LogLoadHtmlWarning();
        }

        partial void OnLoadUrl(string url) {

            if (url == null) {
                return;
            }
            var isHttpOrHttpsUrl = url.StartsWith("http://") || url.StartsWith("https://");
            // streaming-assets:// URLs will be from the same origin.
            _iframeMayBeInaccessible = isHttpOrHttpsUrl;
            if (isHttpOrHttpsUrl) {
                StartCoroutine(_checkForXFrameOptionsHeader(url));
            }
        }

        partial void OnPaste() => WebGLWarnings.LogPasteError();

        partial void OnZoomIn() => WebGLWarnings.LogZoomInError();

        partial void OnZoomOut() => WebGLWarnings.LogZoomOutError();

        bool _ignoreNextLoadHtmlInvocation;
        bool _iframeMayBeInaccessible;

        IEnumerator _checkForXFrameOptionsHeader(string url) {

            var request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();
            if (request.isNetworkError) {
                yield break;
            }
            var headers = request.GetResponseHeaders();
            if (headers == null) {
                yield break;
            }
            var headerName = "X-Frame-Options";
            string headerValue = null;
            headers.TryGetValue(headerName, out headerValue);
            if (headerValue == null) {
                headers.TryGetValue(headerName.ToLowerInvariant(), out headerValue);
            }
            if (headerValue == "DENY" || headerValue == "SAMEORIGIN") {
                var message = $"2D WebView for WebGL won't be able to load this URL ({url}) when running in the browser because the web page's server blocks it from being loaded in an <iframe> element by sending an \"X-Frame-Options: {headerValue}\" response header.";
                WebViewLogger.LogError(message + WebGLWarnings.GetArticleLinkText());
                #if VUPLEX_STANDALONE
                    _ignoreNextLoadHtmlInvocation = true;
                    var html = $"<p style='font-family: sans-serif; padding: 2em;'>{message.Replace("<", "&lt;").Replace(">", "&gt;")} For more info, please see this article: <a href='https://support.vuplex.com/articles/webgl-limitations'>https://support.vuplex.com/articles/webgl-limitations</a></p>";
                    LoadHtml(html);
                #endif
                yield break;
            }
        }

        void _logIFrameContentAccessWarningIfNeeded(string methodName) {

            if (_iframeMayBeInaccessible) {
                WebGLWarnings.LogIFrameContentAccessWarning(methodName);
            }
        }
    }
}
#endif
