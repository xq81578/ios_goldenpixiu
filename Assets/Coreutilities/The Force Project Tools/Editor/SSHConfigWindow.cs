using UnityEditor;
using UnityEngine;

namespace CoreUtilities.The_Force_Project_Tools.Editor
{
    public class SSHConfigWindow : EditorWindow
    {
        private SSHUploadConfig _config;

        [MenuItem("Tools/上传配置")]
        public static void ShowWindow()
        {
            var window = GetWindow<SSHConfigWindow>("SSH上传配置");
            window.minSize = new Vector2(500, 400);
            window._config = SSHUploader.LoadConfig();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SSH服务器配置", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 服务器信息
            _config.ServerHost = EditorGUILayout.TextField("服务器地址", _config.ServerHost);
            _config.ServerPort = EditorGUILayout.IntField("SSH端口", _config.ServerPort);
            _config.Username = EditorGUILayout.TextField("用户名", _config.Username);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("认证方式", EditorStyles.boldLabel);

            // 认证方式选择
            _config.UseKeyAuth = EditorGUILayout.Toggle("使用密钥认证", _config.UseKeyAuth);

            if (_config.UseKeyAuth)
            {
                EditorGUILayout.BeginHorizontal();
                _config.PrivateKeyPath = EditorGUILayout.TextField("私钥路径", _config.PrivateKeyPath);
                if (GUILayout.Button("浏览", GUILayout.Width(60)))
                {
                    string path = EditorUtility.OpenFilePanel("选择SSH私钥", "", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _config.PrivateKeyPath = path;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                _config.Password = EditorGUILayout.PasswordField("密码", _config.Password);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("宝塔配置", EditorStyles.boldLabel);
            _config.BtPanelUrl = EditorGUILayout.TextField("宝塔面板地址", _config.BtPanelUrl);
            _config.BtApiKey = EditorGUILayout.PasswordField("宝塔 API Key", _config.BtApiKey);

            EditorGUILayout.HelpBox(
                "用于通过宝塔面板 API 上传构建压缩包。\n" +
                "示例: https://127.0.0.1:21070\n" +
                "若未填写宝塔 API Key，则会回退到 SCP 上传。",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("路径配置", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("服务器基础路径", "/www/wwwroot");

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"配置说明:\n" +
                $"• 服务器基础路径固定为 /www/wwwroot，无需手动配置\n" +
                $"• Nginx软链接路径: 需要更新的软链接位置\n" +
                $"• 示例: 上传到 /www/wwwroot/MechaGirl/Version/build001\n" +
                $"• BundleSource 会上传到 /www/wwwroot/Assets/MechaGirl",
                MessageType.Info);

            EditorGUILayout.Space();

            // 保存按钮
            if (GUILayout.Button("保存配置", GUILayout.Height(30)))
            {
                SSHUploader.SaveConfig(_config);
                EditorUtility.DisplayDialog("成功", "SSH配置已保存！", "确定");
                Debug.Log("[SSHConfigWindow] SSH配置已保存");
            }

            EditorGUILayout.Space();

            // 测试连接按钮
            if (GUILayout.Button("测试SSH连接", GUILayout.Height(30)))
            {
                TestSSHConnection();
            }
        }

        private async void TestSSHConnection()
        {
            EditorUtility.DisplayProgressBar("测试连接", "正在连接SSH服务器...", 0.5f);
            
            try
            {
                bool success = await SSHUploader.ExecuteSSHCommandAsync("echo 'SSH连接测试成功'", 
                    (msg) => Debug.Log($"[SSH测试] {msg}"));
                
                EditorUtility.ClearProgressBar();
                
                if (success)
                {
                    EditorUtility.DisplayDialog("测试成功", "SSH连接正常！", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("测试失败", "无法连接到SSH服务器，请检查配置。", "确定");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("测试异常", $"测试时发生错误: {e.Message}", "确定");
            }
        }
    }
}