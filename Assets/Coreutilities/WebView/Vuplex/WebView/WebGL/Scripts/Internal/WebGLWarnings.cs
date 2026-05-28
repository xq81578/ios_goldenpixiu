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
#if UNITY_WEBGL
using System;

namespace Vuplex.WebView.Internal {

    static class WebGLWarnings {

        public static string GetArticleLinkText(bool includeFormatting = true) {

            return $" For more info, please see this article: {(includeFormatting ? "<em>" : "")}https://support.vuplex.com/articles/webgl-limitations{(includeFormatting ? "</em>" : "")}";
        }

        public static void LogCanGoBackOrForwardWarning() {

            if (!_hasLoggedBackForwardWarning) {
                WebViewLogger.LogWarning("Just a heads-up: due to browser limitations on WebGL, CanGoBack() and CanGoForward() both return whether the webview can either go back *or* forward when running in the browser. In other words, when running in the browser, both methods return true if the webview can go either back or foward, and both return false if the webview can't go back or forward." + GetArticleLinkText());
                _hasLoggedBackForwardWarning = true;
            }
        }

        public static void LogCaptureScreenshotError() => _logNotSupportedError("CaptureScreenshot", "taking a screenshot of the web content");

        public static void LogCopyError() => _logNotSupportedError("Copy", "manipulating the clipboard");

        public static void LogCutError() => _logNotSupportedError("Cut", "manipulating the clipboard");

        public static void LogGetRawTextureDataError() => _logNotSupportedError("GetRawTextureData", "rendering web content to a texture");

        public static void LogIFrameContentAccessWarning(string methodName) {

            #if UNITY_EDITOR
                WebViewLogger.LogWarning($"{methodName}() was called but will likely be disabled when running on WebGL due to browser limitations. If the webview's URL is set to a domain that is different than the domain of the Unity app, then 2D WebView is unable to execute {methodName}() due to browser security restrictions." + GetArticleLinkText());
            #else
                WebViewLogger.LogError($"{methodName}() was called but is currently disabled due to browser limitations. The webview's URL is set to a domain that is different than the domain of the Unity app, so 2D WebView is unable to execute {methodName}() due to browser security restrictions." + GetArticleLinkText());
            #endif
        }

        public static void LogLoadHtmlWarning() {

            WebViewLogger.LogWarning("Due to browser limitations on WebGL, most IWebView methods are disabled when HTML is loaded with LoadHtml(). An alternative without this limitation is to load the HTML from StreamingAssets using LoadUrl() instead." + GetArticleLinkText());
        }

        public static void LogPasteError() => _logNotSupportedError("Paste", "accessing the clipboard");

        public static void LogZoomInError() => _logNotSupportedError("ZoomIn", "zooming in");

        public static void LogZoomOutError() => _logNotSupportedError("ZoomOut", "zooming out");

        static bool _hasLoggedBackForwardWarning;

        static void _logNotSupportedError(string methodName, string unsupportedActionDescription) {

            WebViewLogger.LogError($"2D WebView for WebGL doesn't support {unsupportedActionDescription} due to browser limitations, so the call to {methodName}() will be ignored when running in the browser." + GetArticleLinkText());
        }
    }
}
#endif
