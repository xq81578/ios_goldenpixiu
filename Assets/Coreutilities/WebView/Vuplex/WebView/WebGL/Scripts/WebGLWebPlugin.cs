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
using UnityEngine;
using Vuplex.WebView.Internal;

namespace Vuplex.WebView {

    class WebGLWebPlugin : IWebPlugin {

        public ICookieManager CookieManager { get; } = null;

        public static WebGLWebPlugin Instance {
            get {
                if (_instance == null) {
                    _instance = new WebGLWebPlugin();
                }
                return _instance;
            }
        }

        public WebPluginType Type { get; } = WebPluginType.WebGL;

        public void ClearAllData() {

            _logNotSupportedWarning("Web.ClearAllData", "clearing browser data");
        }

        // Deprecated
        public void CreateMaterial(Action<Material> callback) => callback(null);

        public IWebView CreateWebView() => WebGLWebView.Instantiate();

        public void EnableRemoteDebugging() {

            WebViewLogger.Log("Remote debugging is enabled for WebGL. For instructions, please see https://support.vuplex.com/articles/how-to-debug-web-content#webgl.");
        }

        public void SetAutoplayEnabled(bool enabled) {

            _logNotSupportedWarning("Web.SetAutoplayEnabled", "enabling autoplay");
        }

        public void SetCameraAndMicrophoneEnabled(bool enabled) => WebGLWebView.SetCameraAndMicrophoneEnabled(enabled);

        public void SetIgnoreCertificateErrors(bool ignore) {

            _logNotSupportedWarning("Web.SetIgnoreCertificateErrors", "ignoring certificate errors");
        }

        public void SetStorageEnabled(bool enabled) {

            _logNotSupportedWarning("Web.SetStorageEnabled", "disabling storage");
        }

        public void SetUserAgent(bool mobile) {

            _logNotSupportedWarning("Web.SetUserAgent", "changing the User-Agent");
        }

        public void SetUserAgent(string userAgent) {

            _logNotSupportedWarning("Web.SetUserAgent", "changing the User-Agent");
        }

        static WebGLWebPlugin _instance;

        void _logNotSupportedWarning(string methodName, string unsupportedActionDescription) {

            WebViewLogger.LogWarning($"2D WebView for WebGL doesn't support {unsupportedActionDescription} due to browser limitations, so the call to {methodName}() will be ignored.");
        }
    }
}
#endif
