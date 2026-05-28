using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace CoreUtilities.The_Force_Project_Tools.Editor
{
    [Serializable]
    public class SSHUploadConfig
    {
        public string ServerHost = "91.208.104.206";
        public int ServerPort = 22;
        public string Username = "root";
        public string Password = "qDWYth7BzP1d";
        public string PrivateKeyPath = ""; // SSH私钥路径（可选，如果使用密钥认证）
        public string RemoteBasePath = "/www/wwwroot"; // 服务器基础路径
        public bool UseKeyAuth = true; // 是否使用密钥认证
        public string BtPanelUrl = "https://91.208.104.206:22493/"; // 宝塔面板地址，如 https://127.0.0.1:21070
        public string BtApiKey = "V3EAGyXzWXfMHSZhfJU5otwj8G1laMrf"; // 宝塔 API Key
    }

    public static class SSHUploader
    {
        private const string FixedRemoteBasePath = "/www/wwwroot";
        /// <summary>EditorPrefs 键：SSH 建立连接阶段超时（秒），对应 ssh/scp 的 ConnectTimeout。跨境/高延迟可设 180～300。</summary>
        private const string PrefSshConnectTimeoutSeconds = "SSH_ConnectTimeoutSeconds";
        /// <summary>SCP 整体上传超时（秒），0 表示不限制。大文件可设 7200。</summary>
        private const string PrefSshScpTimeoutSeconds = "SSH_ScpTimeoutSeconds";
        private static SSHUploadConfig _config;
        private static int _cachedSshConnectTimeoutSeconds = 120;
        private static int _cachedSshScpTimeoutSeconds = 0;
        private static bool _timeoutPrefsLoaded = false;

        private static void RefreshTimeoutPrefsCache()
        {
            int connect = EditorPrefs.GetInt(PrefSshConnectTimeoutSeconds, 120);
            int scp = EditorPrefs.GetInt(PrefSshScpTimeoutSeconds, 0);
            _cachedSshConnectTimeoutSeconds = UnityEngine.Mathf.Clamp(connect, 10, 600);
            _cachedSshScpTimeoutSeconds = UnityEngine.Mathf.Clamp(scp, 0, 86400);
            _timeoutPrefsLoaded = true;
        }

        /// <summary>默认 120s；过短易出现 “Timeout, server … not responding”。可在 Unity 中设置 EditorPrefs：SSH_ConnectTimeoutSeconds（建议 10～600）。</summary>
        private static int GetSshConnectTimeoutSeconds()
        {
            // 注意：不要在后台线程访问 EditorPrefs。若缓存尚未初始化，使用默认值。
            return _timeoutPrefsLoaded ? _cachedSshConnectTimeoutSeconds : 120;
        }

        /// <summary>
        /// ssh/scp 的 ConnectTimeout（仅影响 TCP+握手建立阶段）。
        /// 可选环境变量 UNITY_CI_SSH_CONNECT_TIMEOUT（秒，10～600）覆盖，便于流水线调参。
        /// 注意：曾将 CI 强行压在 20s，跨境/晚高峰易误报超时，且无法缩短大文件 scp 传输时间。
        /// </summary>
        private static int GetEffectiveSshConnectTimeoutSeconds()
        {
            string env = Environment.GetEnvironmentVariable("UNITY_CI_SSH_CONNECT_TIMEOUT");
            if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out int envSec))
            {
                return UnityEngine.Mathf.Clamp(envSec, 10, 600);
            }

            return GetSshConnectTimeoutSeconds();
        }

        /// <summary>
        /// 0 = 不限制（仅非 CI 或已显式 EditorPrefs 设为 0 且非 CI 默认逻辑时）。
        /// CI 且未设 SSH_ScpTimeoutSeconds 时，用环境变量 UNITY_CI_SCP_TIMEOUT_SECONDS（默认 7200）防止 scp 永不返回拖死整 job。
        /// </summary>
        private static int GetSshScpTimeoutSeconds()
        {
            int pref = _timeoutPrefsLoaded ? _cachedSshScpTimeoutSeconds : 0;
            if (pref > 0)
            {
                return pref;
            }

            if (!IsRunningInCi())
            {
                return 0;
            }

            string env = Environment.GetEnvironmentVariable("UNITY_CI_SCP_TIMEOUT_SECONDS");
            if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out int sec) && sec > 0)
            {
                return UnityEngine.Mathf.Clamp(sec, 60, 86400);
            }

            return 7200;
        }

        /// <summary>单次 ssh 执行结果（供重试逻辑判断）。</summary>
        private struct SshExecutionResult
        {
            public bool Success;
            public int ExitCode;
            public string StandardOutput;
            public string StandardError;
        }

        private static bool IsRunningInCi()
        {
            string ci = Environment.GetEnvironmentVariable("CI");
            string gitlabCi = Environment.GetEnvironmentVariable("GITLAB_CI");
            return string.Equals(ci, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(gitlabCi, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 供 <see cref="BuildScript"/> 在 <c>Task.Run</c> 上传前于主线程调用。
        /// <see cref="EditorPrefs"/> 与 Addressables 取项目名必须在主线程；上传异步在子线程跑时避免首次访问卡死/异常。
        /// </summary>
        public static void PrewarmEditorStateForCliWorkerUpload()
        {
            LoadConfig();
            _cliPrewarmedProjectName = GetProjectName();
            // 必须在主线程计算：内含 Application.isBatchMode；上传在 ThreadPool 上执行时不可再调用。
            _cliPrewarmedShouldThreadPoolHop = ShouldUseThreadPoolForCliUploadBlockingWait();
        }

        private static string _cliPrewarmedProjectName;

        /// <summary>由 <see cref="PrewarmEditorStateForCliWorkerUpload"/> 在主线程写入，供子线程上传路径读取。</summary>
        private static bool? _cliPrewarmedShouldThreadPoolHop;

        /// <summary>
        /// BuildScript.TryCliPostUploadSync 在主线程 <c>GetAwaiter().GetResult()</c> 会阻塞；
        /// 若上传链路里 <c>await</c> 默认回到 Unity 主线程，会与 <c>GetResult</c> 死锁（日志停在 ssh 诊断后无下文）。
        /// Jenkins 等可能未使用 <c>-batchmode</c>，仅靠 <see cref="Application.isBatchMode"/> 会漏判。
        /// </summary>
        public static bool ShouldUseThreadPoolForCliUploadBlockingWait()
        {
            if (Application.isBatchMode)
            {
                return true;
            }

            if (IsRunningInCi())
            {
                return true;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JENKINS_URL")) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JENKINS_HOME")))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_NUMBER")) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_ID")))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 与 <see cref="ShouldUseThreadPoolForCliUploadBlockingWait"/> 相同语义，但优先使用 <see cref="PrewarmEditorStateForCliWorkerUpload"/> 在主线程写入的快照，
        /// 供 S3/SSH 在 <c>ConfigureAwait(false)</c> 或线程池延续上调用，避免访问 <see cref="Application.isBatchMode"/>。
        /// </summary>
        public static bool GetShouldUseThreadPoolHopPreferPrewarmed()
        {
            return _cliPrewarmedShouldThreadPoolHop ?? ShouldUseThreadPoolForCliUploadBlockingWait();
        }

        private static void LogSshDiag(string stage, SSHUploadConfig config, string sshCommand, string remoteCommand)
        {
            bool isCi = IsRunningInCi();
            if (!isCi)
            {
                return;
            }

            string keyPath = config.PrivateKeyPath ?? string.Empty;
            bool keyPathProvided = !string.IsNullOrWhiteSpace(keyPath);
            bool keyFileExists = keyPathProvided && File.Exists(keyPath);

            Debug.Log(
                $"[SSHUploader][CI-DIAG] stage={stage}, host={config.ServerHost}, port={config.ServerPort}, user={config.Username}, useKeyAuth={config.UseKeyAuth}");
            Debug.Log(
                $"[SSHUploader][CI-DIAG] keyPath={keyPath}, keyPathProvided={keyPathProvided}, keyFileExists={keyFileExists}");
            Debug.Log($"[SSHUploader][CI-DIAG] remoteCommand={remoteCommand}");
            Debug.Log($"[SSHUploader][CI-DIAG] sshArguments={sshCommand}");
        }

        /// <summary>ssh / scp 共用的 OpenSSH 客户端选项（连接与保活）。</summary>
        private static void AppendOpenSshClientOptions(List<string> args)
        {
            args.Add("-o");
            args.Add("StrictHostKeyChecking=no");
            args.Add("-o");
            args.Add("UserKnownHostsFile=/dev/null");
            args.Add("-o");
            args.Add("BatchMode=yes");
            args.Add("-o");
            args.Add($"ConnectTimeout={GetEffectiveSshConnectTimeoutSeconds()}");
            args.Add("-o");
            args.Add("ConnectionAttempts=1");
            args.Add("-o");
            args.Add("ServerAliveInterval=15");
            args.Add("-o");
            args.Add("ServerAliveCountMax=6");
        }

        /// <summary>
        /// CI / batchmode 下用环境变量覆盖 SSH 私钥路径（不依赖本机 EditorPrefs）。
        /// <list type="bullet">
        /// <item><description><c>UNITY_CI_SSH_PRIVATE_KEY_PATH</c>：私钥文件绝对路径。与 <c>AWS_SECRET_ACCESS_KEY</c> 相同在 GitLab「项目/群组 → 设置 → CI/CD → 变量」配置；推荐变量类型选 File、变量名用本 key，Runner 会注入临时路径。存在则强制 <see cref="SSHUploadConfig.UseKeyAuth"/> = true。</description></item>
        /// <item><description><c>UNITY_CI_SSH_USE_KEY_AUTH</c>：可选 <c>true</c>/<c>false</c>/<c>1</c>/<c>0</c>，在已加载 EditorPrefs 之后进一步覆盖是否使用密钥（例如仅有路径时需关闭可显式写 false）。</description></item>
        /// </list>
        /// 仅在 <see cref="IsRunningInCi"/> 或 <c>Application.isBatchMode</c> 时生效，避免本地编辑器误读环境变量。
        /// </summary>
        private static void ApplyEnvironmentSshCredentialOverrides(SSHUploadConfig config)
        {
            if (!(IsRunningInCi() || Application.isBatchMode))
            {
                return;
            }

            string pathEnv = Environment.GetEnvironmentVariable("UNITY_CI_SSH_PRIVATE_KEY_PATH");
            if (!string.IsNullOrWhiteSpace(pathEnv))
            {
                string p = pathEnv.Trim().Trim('"', '\'');
                if (File.Exists(p))
                {
                    config.PrivateKeyPath = p;
                    config.UseKeyAuth = true;
                }
                else
                {
                    Debug.LogWarning($"[SSHUploader] UNITY_CI_SSH_PRIVATE_KEY_PATH is set but file not found: {p}");
                }
            }

            string useKey = Environment.GetEnvironmentVariable("UNITY_CI_SSH_USE_KEY_AUTH");
            if (string.IsNullOrWhiteSpace(useKey))
            {
                return;
            }

            if (bool.TryParse(useKey, out bool useKeyParsed))
            {
                config.UseKeyAuth = useKeyParsed;
            }
            else if (string.Equals(useKey, "1", StringComparison.Ordinal))
            {
                config.UseKeyAuth = true;
            }
            else if (string.Equals(useKey, "0", StringComparison.Ordinal))
            {
                config.UseKeyAuth = false;
            }
        }

        /// <summary>
        /// GitLab File 类 CI 变量等场景下私钥常为 world-readable，OpenSSH 会拒绝（bad permissions）。
        /// Windows 上 OpenSSH 不沿用 Unix 的 chmod 语义，此处跳过。
        /// </summary>
        private static void TryEnsureSshPrivateKeyFilePermissions(SSHUploadConfig config)
        {
            if (config == null || !config.UseKeyAuth || string.IsNullOrWhiteSpace(config.PrivateKeyPath))
            {
                return;
            }

            string path = config.PrivateKeyPath;
            if (!File.Exists(path))
            {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"600 {QuoteArgument(path)}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = new System.Diagnostics.Process { StartInfo = psi })
                {
                    p.Start();
                    if (!p.WaitForExit(10000))
                    {
                        try
                        {
                            p.Kill();
                        }
                        catch
                        {
                            // ignore
                        }

                        Debug.LogWarning("[SSHUploader] chmod 600 on private key timed out — ssh may reject the key.");
                    }
                    else if (p.ExitCode != 0)
                    {
                        Debug.LogWarning(
                            $"[SSHUploader] chmod 600 on private key failed (exit {p.ExitCode}) — ssh may reject the key.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SSHUploader] chmod 600 on private key skipped: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载配置（可以从ScriptableObject或EditorPrefs加载）
        /// </summary>
        public static SSHUploadConfig LoadConfig()
        {
            if (_config == null)
            {
                _config = new SSHUploadConfig();

                // 从EditorPrefs加载配置
                _config.ServerHost = EditorPrefs.GetString("SSH_ServerHost", "91.208.104.206");
                _config.ServerPort = EditorPrefs.GetInt("SSH_ServerPort", 22);
                _config.Username = EditorPrefs.GetString("SSH_Username", "root");
                _config.Password = EditorPrefs.GetString("SSH_Password", "qDWYth7BzP1d");
                _config.PrivateKeyPath = EditorPrefs.GetString("SSH_PrivateKeyPath", "");
                _config.RemoteBasePath = FixedRemoteBasePath;
                _config.UseKeyAuth = EditorPrefs.GetBool("SSH_UseKeyAuth", false);
                _config.BtPanelUrl = EditorPrefs.GetString("SSH_BtPanelUrl", "https://91.208.104.206:22493/");
                _config.BtApiKey = EditorPrefs.GetString("SSH_BtApiKey", "V3EAGyXzWXfMHSZhfJU5otwj8G1laMrf");
                EditorPrefs.DeleteKey("SSH_RemoteBasePath");
                RefreshTimeoutPrefsCache();
                ApplyEnvironmentSshCredentialOverrides(_config);
            }

            return _config;
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public static void SaveConfig(SSHUploadConfig config)
        {
            config.RemoteBasePath = FixedRemoteBasePath;
            _config = config;
            EditorPrefs.SetString("SSH_ServerHost", config.ServerHost);
            EditorPrefs.SetInt("SSH_ServerPort", config.ServerPort);
            EditorPrefs.SetString("SSH_Username", config.Username);
            EditorPrefs.SetString("SSH_Password", config.Password);
            EditorPrefs.SetString("SSH_PrivateKeyPath", config.PrivateKeyPath);
            EditorPrefs.SetBool("SSH_UseKeyAuth", config.UseKeyAuth);
            EditorPrefs.SetString("SSH_BtPanelUrl", config.BtPanelUrl);
            EditorPrefs.SetString("SSH_BtApiKey", config.BtApiKey);
            EditorPrefs.DeleteKey("SSH_RemoteBasePath");
            RefreshTimeoutPrefsCache();
        }

        /// <summary>
        /// 上传文件夹到Linux服务器
        /// </summary>
        public static async Task<bool> UploadDirectoryAsync(string localPath, string remoteSubPath,
            Action<string> onProgress = null)
        {
            var config = LoadConfig();

            if (!Directory.Exists(localPath))
            {
                Debug.LogError($"[SSHUploader] 本地路径不存在: {localPath}");
                return false;
            }

            try
            {
                onProgress?.Invoke("开始上传...");

                // 构建远程路径
                string remotePath = $"{config.RemoteBasePath.TrimEnd('/')}/{remoteSubPath.TrimStart('/')}";

                Debug.Log($"[SSHUploader] 上传配置:\n" +
                          $"  服务器: {config.Username}@{config.ServerHost}:{config.ServerPort}\n" +
                          $"  本地路径: {localPath}\n" +
                          $"  远程路径: {remotePath}");

                // 使用 scp 命令上传（需要系统安装了 scp）
                bool success = await UploadWithSCP(localPath, remotePath, config, onProgress).ConfigureAwait(false);

                if (success)
                {
                    onProgress?.Invoke("上传完成！");
                    Debug.Log($"[SSHUploader] ✅ 上传成功: {localPath} -> {remotePath}");
                    return true;
                }
                else
                {
                    Debug.LogError("[SSHUploader] ❌ 上传失败");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SSHUploader] 上传异常: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 使用 SCP 命令上传
        /// </summary>
        private static async Task<bool> UploadWithSCP(string localPath, string remotePath, SSHUploadConfig config,
            Action<string> onProgress)
        {
            try
            {
                TryEnsureSshPrivateKeyFilePermissions(config);
                // 构建 scp 命令
                string scpCommand = BuildSCPCommand(localPath, remotePath, config);

                onProgress?.Invoke("执行 SCP 上传命令...");
                Debug.Log($"[SSHUploader] 执行命令: {scpCommand}");

                // 执行命令
                // Jenkins/GitLab Runner 等 CI：Unity -batchmode 的 STDIN 是一根「打开但空闲」的管道；
                // 若 scp 继承父进程 STDIN，scp 在某些路径上会阻塞读 stdin，表现为「进程启动后完全不动」。
                // 必须 RedirectStandardInput=true，Start() 之后立刻关闭，给子进程一个已关闭的 stdin。
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "scp",
                        Arguments = scpCommand,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                try { process.StandardInput.Close(); } catch { /* 关不上就忽略，不要影响主流程 */ }

                // 与 ssh 相同：并行读 stdout/stderr，避免缓冲区塞满死锁
                Task<string> outTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errTask = process.StandardError.ReadToEndAsync();
                Task exitTask = Task.Run(() =>
                {
                    try
                    {
                        process.WaitForExit();
                    }
                    catch
                    {
                        // Kill 后可能异常，忽略
                    }
                });

                int scpTimeout = GetSshScpTimeoutSeconds();
                if (scpTimeout > 0)
                {
                    Task delayTask = Task.Delay(TimeSpan.FromSeconds(scpTimeout));
                    Task finished = await Task.WhenAny(exitTask, delayTask).ConfigureAwait(false);
                    if (finished == delayTask && !process.HasExited)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // ignore
                        }

                        Debug.LogError($"[SSHUploader] SCP 超时（{scpTimeout}s）已终止");
                        onProgress?.Invoke($"SCP 超时({scpTimeout}s)，可在 EditorPrefs 增大 SSH_ScpTimeoutSeconds 或检查网络");
                        return false;
                    }
                }
                else
                {
                    await exitTask.ConfigureAwait(false);
                }

                string output = await outTask.ConfigureAwait(false);
                string error = await errTask.ConfigureAwait(false);

                if (process.ExitCode == 0)
                {
                    Debug.Log($"[SSHUploader] SCP 上传成功");
                    if (!string.IsNullOrEmpty(output))
                        Debug.Log($"[SSHUploader] 输出: {output}");
                    return true;
                }
                else
                {
                    Debug.LogError($"[SSHUploader] SCP 上传失败，退出码: {process.ExitCode}");
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogError($"[SSHUploader] 错误: {error}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SSHUploader] SCP 执行异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 构建 SCP 命令
        /// </summary>
        private static string BuildSCPCommand(string localPath, string remotePath, SSHUploadConfig config)
        {
            // scp -C -P port -i keyfile localpath user@host:remotepath
            List<string> args = new List<string>();

            if (Directory.Exists(localPath))
            {
                args.Add("-r");
            }

            // args.Add("-C"); // 启用压缩
            args.Add("-P");
            args.Add(config.ServerPort.ToString()); // 端口

            if (config.UseKeyAuth && !string.IsNullOrEmpty(config.PrivateKeyPath))
            {
                args.Add("-i");
                args.Add(QuoteArgument(config.PrivateKeyPath)); // 私钥路径
            }

            AppendOpenSshClientOptions(args);

            // 构建远程路径
            string remoteTarget = $"{config.Username}@{config.ServerHost}:{remotePath}";

            args.Add(QuoteArgument(localPath));
            args.Add(remoteTarget);

            return string.Join(" ", args);
        }

        /// <summary>
        /// 将参数包一层双引号，避免 Windows 下路径中的空格导致 scp/ssh 参数拆分。
        /// </summary>
        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        /// <summary>
        /// 获取宝塔面板地址；若未单独配置，则回退为服务器地址 + 默认宝塔端口 21070。
        /// </summary>
        private static string GetBtPanelUrl(SSHUploadConfig config)
        {
            if (!string.IsNullOrEmpty(config.BtPanelUrl))
            {
                return config.BtPanelUrl.TrimEnd('/');
            }

            return $"https://{config.ServerHost}:21070";
        }

        /// <summary>
        /// 计算小写 MD5，用于宝塔 request_token。
        /// </summary>
        private static string GetMd5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                foreach (byte hashByte in hashBytes)
                {
                    sb.Append(hashByte.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// 获取宝塔 API 认证字段。
        /// </summary>
        private static Dictionary<string, string> GetBtAuthFields(SSHUploadConfig config)
        {
            string requestTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string requestToken = GetMd5Hash(requestTime + GetMd5Hash(config.BtApiKey));

            return new Dictionary<string, string>
            {
                { "request_time", requestTime },
                { "request_token", requestToken }
            };
        }

        private static byte[] GetMultipartFieldBytes(string boundary, string fieldName, string value)
        {
            string field =
                $"--{boundary}\r\n" +
                $"Content-Disposition: form-data; name=\"{fieldName}\"\r\n\r\n" +
                $"{value}\r\n";
            return Encoding.UTF8.GetBytes(field);
        }

        private static byte[] GetMultipartFileHeaderBytes(string boundary, string fieldName, string fileName)
        {
            string header =
                $"--{boundary}\r\n" +
                $"Content-Disposition: form-data; name=\"{fieldName}\"; filename=\"{fileName}\"\r\n" +
                "Content-Type: application/octet-stream\r\n\r\n";
            return Encoding.UTF8.GetBytes(header);
        }

        /// <summary>
        /// 通过宝塔面板 API 上传文件。速度通常优于本机 scp。
        /// </summary>
        private static async Task<bool> UploadWithBaoTaApiAsync(string localFilePath, string remoteFilePath,
            SSHUploadConfig config, Action<string> onProgress)
        {
            if (string.IsNullOrEmpty(config.BtApiKey))
            {
                Debug.LogWarning("[SSHUploader] 未配置宝塔 API Key，回退到 SCP 上传。");
                return await UploadWithSCP(localFilePath, remoteFilePath, config, onProgress).ConfigureAwait(false);
            }

            string btPanelUrl = GetBtPanelUrl(config);
            string uploadUrl = btPanelUrl + "/files?action=upload";
            string remoteDirectory = Path.GetDirectoryName(remoteFilePath)?.Replace("\\", "/") ?? "/";
            string fileName = Path.GetFileName(localFilePath);
            string remoteFullPath = remoteFilePath.Replace("\\", "/");
            long fileSize = new FileInfo(localFilePath).Length;
            string boundary = "----SSHUploader" + DateTime.UtcNow.Ticks.ToString("x");

            onProgress?.Invoke("执行宝塔 API 上传...");
            Debug.Log($"[SSHUploader] 宝塔上传: {localFilePath} -> {remoteFullPath}");

            var fields = GetBtAuthFields(config);
            fields["f_path"] = remoteDirectory;
            fields["f_name"] = remoteFullPath;
            fields["f_size"] = fileSize.ToString();
            fields["f_start"] = "0";

            var fieldBytesList = fields
                .Select(x => GetMultipartFieldBytes(boundary, x.Key, x.Value))
                .ToList();
            byte[] fileHeaderBytes = GetMultipartFileHeaderBytes(boundary, "blob", fileName);
            byte[] endBoundaryBytes = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");

            long contentLength = fieldBytesList.Sum(x => (long)x.Length) +
                                 fileHeaderBytes.Length +
                                 fileSize +
                                 endBoundaryBytes.Length;

            RemoteCertificateValidationCallback previousCertCallback = ServicePointManager.ServerCertificateValidationCallback;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.Expect100Continue = false;

            var request = (HttpWebRequest)WebRequest.Create(uploadUrl);
            request.Method = "POST";
            request.ContentType = $"multipart/form-data; boundary={boundary}";
            request.ContentLength = contentLength;
            request.Timeout = 30 * 60 * 1000;
            request.ReadWriteTimeout = 30 * 60 * 1000;
            request.KeepAlive = true;
            request.AllowWriteStreamBuffering = false;
            request.SendChunked = false;

            try
            {
                try
                {
                    using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                    {
                        foreach (byte[] fieldBytes in fieldBytesList)
                        {
                            await requestStream.WriteAsync(fieldBytes, 0, fieldBytes.Length).ConfigureAwait(false);
                        }

                        await requestStream.WriteAsync(fileHeaderBytes, 0, fileHeaderBytes.Length).ConfigureAwait(false);

                        using (var fileStream = File.OpenRead(localFilePath))
                        {
                            byte[] buffer = new byte[1024 * 64];
                            long uploaded = 0;
                            int read;

                            while ((read = await fileStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                            {
                                await requestStream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                                uploaded += read;

                                if (fileSize > 0)
                                {
                                    int progress = (int)(uploaded * 100 / fileSize);
                                    onProgress?.Invoke($"执行宝塔 API 上传... {progress}%");
                                }
                            }
                        }

                        await requestStream.WriteAsync(endBoundaryBytes, 0, endBoundaryBytes.Length).ConfigureAwait(false);
                    }

                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    using (var responseStream = response.GetResponseStream())
                    using (var reader = new StreamReader(responseStream ?? Stream.Null))
                    {
                        string result = await reader.ReadToEndAsync().ConfigureAwait(false);

                        if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
                        {
                            Debug.LogError($"[SSHUploader] 宝塔上传失败，状态码: {(int)response.StatusCode}, 响应: {result}");
                            return false;
                        }

                        if (result.IndexOf("\"status\":false", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            result.IndexOf("\"status\": false", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.LogError($"[SSHUploader] 宝塔上传返回失败: {result}");
                            return false;
                        }

                        Debug.Log($"[SSHUploader] 宝塔上传成功: {result}");
                        return true;
                    }
                }
                catch (WebException e)
                {
                    string responseText = string.Empty;
                    if (e.Response != null)
                    {
                        using (var responseStream = e.Response.GetResponseStream())
                        using (var reader = new StreamReader(responseStream ?? Stream.Null))
                        {
                            responseText = await reader.ReadToEndAsync().ConfigureAwait(false);
                        }
                    }

                    Debug.LogError($"[SSHUploader] 宝塔上传异常: {e.Message}\n{responseText}");
                    return false;
                }
            }
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback = previousCertCallback;
            }
        }

        /// <summary>
        /// 递归复制目录内容，不包含源目录本身。
        /// </summary>
        private static void CopyDirectoryContents(string sourcePath, string targetPath)
        {
            Directory.CreateDirectory(targetPath);

            foreach (string directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourcePath, directory);
                Directory.CreateDirectory(Path.Combine(targetPath, relativePath));
            }

            foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourcePath, file);
                string destinationPath = Path.Combine(targetPath, relativePath);
                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(file, destinationPath, true);
            }
        }

        /// <summary>
        /// 将单个目录组装成 zip，zip 根目录名为 rootFolderName。
        /// </summary>
        private static string CreateDirectoryArchive(
            string sourcePath,
            string rootFolderName,
            string progressMessage,
            Action<string> onProgress = null)
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"S003_Upload_{Guid.NewGuid():N}");
            string packageRoot = Path.Combine(tempRoot, rootFolderName);
            string archivePath = Path.Combine(Path.GetTempPath(), $"{rootFolderName}_{Guid.NewGuid():N}.zip");

            try
            {
                onProgress?.Invoke(progressMessage);
                Directory.CreateDirectory(packageRoot);

                CopyDirectoryContents(sourcePath, packageRoot);

                onProgress?.Invoke($"压缩 {rootFolderName}...");
                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }

                ZipFile.CreateFromDirectory(tempRoot, archivePath, System.IO.Compression.CompressionLevel.Fastest, false);
                Debug.Log($"[SSHUploader] 已生成上传压缩包: {archivePath}");
                return archivePath;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, true);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SSHUploader] 清理临时打包目录失败: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 上传单个文件到远端。
        /// </summary>
        private static async Task<bool> UploadFileAsync(string localFilePath, string remoteFilePath,
            Action<string> onProgress = null)
        {
            var config = LoadConfig();

            if (!File.Exists(localFilePath))
            {
                Debug.LogError($"[SSHUploader] 本地文件不存在: {localFilePath}");
                return false;
            }

            onProgress?.Invoke("上传压缩包...");
            return await UploadWithBaoTaApiAsync(localFilePath, remoteFilePath, config, onProgress).ConfigureAwait(false);
        }

        /// <summary>
        /// 通过 SSH 执行命令。timeoutSeconds>0 时超时会杀进程并返回 false，避免卡死无反馈。
        /// </summary>
        /// <param name="timeoutSeconds">超时秒数；0 表示不限制（解压等大步骤可给 600）</param>
        public static async Task<bool> ExecuteSSHCommandAsync(string command, Action<string> onProgress = null,
            int timeoutSeconds = 0)
        {
            SshExecutionResult r = await ExecuteSSHCommandCoreAsync(command, onProgress, timeoutSeconds).ConfigureAwait(false);
            return r.Success;
        }

        /// <summary>与 <see cref="ExecuteSSHCommandAsync"/> 相同，但返回退出码与输出，供重试逻辑使用。</summary>
        private static async Task<SshExecutionResult> ExecuteSSHCommandCoreAsync(
            string command,
            Action<string> onProgress,
            int timeoutSeconds)
        {
            var config = LoadConfig();
            TryEnsureSshPrivateKeyFilePermissions(config);

            try
            {
                string shortCmd = command.Length > 120 ? command.Substring(0, 117) + "..." : command;
                onProgress?.Invoke($"SSH: {shortCmd}");
                Debug.Log($"[SSHUploader] 执行SSH命令: {command}");

                string sshCommand = BuildSSHCommand(command, config);
                LogSshDiag("ExecuteSSHCommandCoreAsync", config, sshCommand, command);

                // Jenkins/GitLab Runner 在 Windows 上跑 Unity -batchmode 时，Unity 的 STDIN 是一根来自 CI
                // 的「打开但空闲」的管道。ssh 带远端命令运行时会持续把本地 stdin 转发给远端，
                // 本地 stdin 永不 EOF → ssh 不退出 → WaitForExit 永不返回 → 整个 CI job 卡死。
                // 这里同时在 .NET 侧 RedirectStandardInput + Close()，以及在 ssh 命令行加 -n，
                // 双保险保证子进程拿到的是「已关闭的 stdin」。
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ssh",
                        Arguments = sshCommand,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                try { process.StandardInput.Close(); } catch { /* 关不上就忽略，不要影响主流程 */ }

                // 同时读 stdout/stderr，避免 ssh 只写 stderr 时先 ReadToEnd stdout 导致死锁
                Task<string> outTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errTask = process.StandardError.ReadToEndAsync();
                Task exitTask = Task.Run(() =>
                {
                    try
                    {
                        process.WaitForExit();
                    }
                    catch
                    {
                        // 进程被 Kill 后 WaitForExit 可能抛异常，忽略
                    }
                });

                if (timeoutSeconds > 0)
                {
                    Task delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                    Task finished = await Task.WhenAny(exitTask, delayTask).ConfigureAwait(false);
                    if (finished == delayTask && !process.HasExited)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // ignore
                        }

                        Debug.LogError($"[SSHUploader] SSH 超时（{timeoutSeconds}s）已终止: {shortCmd}");
                        onProgress?.Invoke($"SSH 超时({timeoutSeconds}s)，请检查网络/密钥/sudo");
                        return new SshExecutionResult
                        {
                            Success = false,
                            ExitCode = -1,
                            StandardOutput = string.Empty,
                            // 与 OpenSSH “timed out” 类文案一致，便于重试逻辑识别为瞬态
                            StandardError = "timed out (local SSH watchdog)"
                        };
                    }
                }
                else
                {
                    await exitTask.ConfigureAwait(false);
                }

                string output = await outTask.ConfigureAwait(false);
                string error = await errTask.ConfigureAwait(false);

                if (process.ExitCode == 0)
                {
                    Debug.Log($"[SSHUploader] SSH命令执行成功");
                    if (!string.IsNullOrEmpty(output))
                        Debug.Log($"[SSHUploader] 输出: {output}");
                    return new SshExecutionResult
                    {
                        Success = true,
                        ExitCode = 0,
                        StandardOutput = output,
                        StandardError = error
                    };
                }

                Debug.LogError($"[SSHUploader] SSH命令执行失败，退出码: {process.ExitCode}");
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"[SSHUploader] 错误: {error}");
                return new SshExecutionResult
                {
                    Success = false,
                    ExitCode = process.ExitCode,
                    StandardOutput = output,
                    StandardError = error
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[SSHUploader] SSH执行异常: {e.Message}\n{e.StackTrace}");
                return new SshExecutionResult
                {
                    Success = false,
                    ExitCode = -1,
                    StandardOutput = string.Empty,
                    StandardError = e.Message
                };
            }
        }

        /// <summary>
        /// 构建 SSH 命令
        /// </summary>
        private static string BuildSSHCommand(string remoteCommand, SSHUploadConfig config)
        {
            List<string> args = new List<string>();

            // -n：把本地 stdin 重定向到 /dev/null（Windows 对应 NUL），防止 ssh 把 CI runner 的空闲 STDIN
            // 一直转发给远端命令；这是 Jenkins + OpenSSH 经典卡死场景的根源之一。
            // 注意：若以后需要远端进程读取本地 stdin，请去掉此参数并改用 RedirectStandardInput 喂数据。
            args.Add("-n");

            args.Add("-p");
            args.Add(config.ServerPort.ToString()); // 端口

            if (config.UseKeyAuth && !string.IsNullOrEmpty(config.PrivateKeyPath))
            {
                args.Add("-i");
                args.Add(QuoteArgument(config.PrivateKeyPath)); // 私钥路径
            }

            AppendOpenSshClientOptions(args);

            string target = $"{config.Username}@{config.ServerHost}";
            args.Add(target);

            // 转义命令中的特殊字符
            string escapedCommand = remoteCommand.Replace("\"", "\\\"");
            args.Add($"\"{escapedCommand}\"");

            return string.Join(" ", args);
        }

        private static bool IsLikelyTransientSshFailure(string stderr)
        {
            if (string.IsNullOrEmpty(stderr)) return false;
            // 常见可重试：网络抖动/连接超时/连接被中断/路由临时不可达，以及偶发 I/O / 资源占用
            return
                // 连接/路由相关
                stderr.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Connection timed out", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("not responding", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Connection reset", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("No route to host", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Broken pipe", StringComparison.OrdinalIgnoreCase) ||
                // 偶发 I/O / 资源问题（unzip/sudo 过程中也可能出现）
                stderr.Contains("Input/output error", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("I/O error", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Resource temporarily unavailable", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Device or resource busy", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>认证/权限/配置错误等，重试通常无效（仅匹配 ssh 客户端侧常见文案，避免与远端脚本报错混淆）。</summary>
        private static bool IsLikelyPermanentSshFailure(string stderr)
        {
            if (string.IsNullOrEmpty(stderr)) return false;
            if (stderr.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)) return true;
            if (stderr.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase)) return true;
            if (stderr.Contains("Could not resolve hostname", StringComparison.OrdinalIgnoreCase)) return true;
            if (stderr.Contains("Identity file", StringComparison.OrdinalIgnoreCase) &&
                stderr.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)) return true;
            if (stderr.Contains("unexpected EOF while looking for matching", StringComparison.OrdinalIgnoreCase))
                return true;
            if (stderr.Contains("syntax error: unexpected end of file", StringComparison.OrdinalIgnoreCase))
                return true;
            return stderr.Contains("Load key", StringComparison.OrdinalIgnoreCase) &&
                   stderr.Contains("bad permissions", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 是否应对本次失败退避重试：瞬态网络问题，或 ssh 客户端层失败（常见 exit 255）且非明确永久错误。
        /// </summary>
        private static bool ShouldRetrySshAfterFailure(SshExecutionResult r)
        {
            if (r.Success) return false;
            string err = r.StandardError ?? string.Empty;
            if (IsLikelyPermanentSshFailure(err)) return false;
            if (IsLikelyTransientSshFailure(err)) return true;
            // OpenSSH：连接/握手阶段失败多为 255；远端命令失败时一般为远端退出码而非 255
            if (r.ExitCode == 255) return true;
            return false;
        }

        /// <summary>
        /// 执行 SSH 命令（带重试与退避）。仅对瞬态网络问题或 ssh 层 255 等可恢复失败重试；认证/密钥错误不重试。
        /// </summary>
        private static async Task<bool> ExecuteSSHCommandWithRetryAsync(
            string remoteCommand,
            Action<string> onProgress = null,
            int timeoutSeconds = 0,
            int maxAttempts = 10,
            int initialBackoffSeconds = 5)
        {
            int attempt = 0;
            int backoff = Math.Max(1, initialBackoffSeconds);
            while (true)
            {
                attempt++;
                onProgress?.Invoke($"SSH 尝试执行（{attempt}/{maxAttempts}），单次超时={timeoutSeconds}s");

                SshExecutionResult r = await ExecuteSSHCommandCoreAsync(remoteCommand, onProgress, timeoutSeconds)
                    .ConfigureAwait(false);
                if (r.Success) return true;

                if (!ShouldRetrySshAfterFailure(r))
                {
                    onProgress?.Invoke("SSH 失败（非网络瞬态问题），不再自动重试。请检查密钥、账号权限与远端路径。");
                    return false;
                }

                if (attempt >= maxAttempts)
                {
                    onProgress?.Invoke($"SSH 失败已达最大重试次数({maxAttempts})，请检查服务器 22 端口连通性/防火墙/sshd 负载");
                    return false;
                }

                onProgress?.Invoke($"SSH 执行失败，{backoff}s 后重试（{attempt}/{maxAttempts}）...");
                await Task.Delay(TimeSpan.FromSeconds(backoff)).ConfigureAwait(false);
                backoff = Math.Min(backoff * 2, 60);
            }
        }

        private static string BuildRemoteDeployCommand(
            string remoteVersionBasePath,
            string remoteAssetsProjectPath,
            string remoteVersionPath,
            string remoteBundleSourcePath,
            string remoteVersionArchivePath,
            string remoteBundleArchivePath,
            string symlinkPath,
            string projectName)
        {
            // 合并成一次 SSH，减少握手次数；远端以 echo '<base64>' | base64 -d | bash 执行（避免多层引号）
            // 说明：
            // - set -e: 任一步失败直接失败，避免“部分成功”
            // - unzip 使用 -o -q
            // - sudo ln -sfn: 若 sudo 需要交互，会因 BatchMode 卡死；此处由调用者保证 sudo 免密或改用有权限用户
            // 脚本经 base64 传入，避免「bash -lc」+ QuoteArgument + BuildSSHCommand 的 \" 在 Windows/OpenSSH 上叠多层引号，导致远端 bash: unexpected EOF while looking for matching `"'
            string inner =
                "set -e; " +
                $"rm -rf \"{remoteVersionPath}\"; "+
                $"rm -rf \"{remoteBundleSourcePath}\"; " +
                $"unzip -o -q \"{remoteVersionArchivePath}\" -d \"{remoteVersionBasePath}\"; " +
                $"unzip -o -q \"{remoteBundleArchivePath}\" -d \"{remoteAssetsProjectPath}\"; " +
                $"rm -f \"{remoteVersionArchivePath}\" \"{remoteBundleArchivePath}\"; " +
                $"sudo ln -sfn \"{remoteVersionPath}\" \"{symlinkPath}\"; " +
                $"echo \"deploy_ok:{projectName}\"";

            string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(inner));
            return $"echo {QuoteArgument(b64)} | base64 -d | bash";
        }

        /// <summary>
        /// 获取项目名称（从Addressables配置）
        /// </summary>
        public static string GetProjectName()
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null && settings.profileSettings != null)
                {
                    string activeProfileId = settings.activeProfileId;
                    string projectName = settings.profileSettings.GetValueByName(activeProfileId, "ProjectName");
                    if (!string.IsNullOrEmpty(projectName))
                    {
                        return projectName;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SSHUploader] 获取ProjectName失败: {e.Message}");
            }

            // 如果获取失败，使用Application.productName作为fallback
            return Application.productName;
        }

        /// <summary>
        /// 上传构建版本到Linux服务器（包含BuildOutput和BundleSource）
        /// </summary>
        public static async Task<bool> UploadBuildVersionAsync(
            string buildOutputPath, // BuildOutput/2603071454_1_0_12_d
            string bundleSourcePath, // BundleSource_DEV
            string versionFolderName, // 2603071454_1_0_12_d
            BuildScript.BuildTypeEnum buildType,
            Action<string> onProgress = null)
        {
            var config = LoadConfig();
            string projectName = !string.IsNullOrEmpty(_cliPrewarmedProjectName)
                ? _cliPrewarmedProjectName
                : GetProjectName();

            try
            {
                string remoteBasePath = GetRemoteBasePathByBuildType(buildType);

                string bundleSourceFolderName = GetBundleSourceFolderName(buildType);
                string localBundleSourcePath = string.IsNullOrEmpty(bundleSourcePath)
                    ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, bundleSourceFolderName)
                    : bundleSourcePath;

                if (!Directory.Exists(localBundleSourcePath))
                {
                    Debug.LogError($"[SSHUploader] BundleSource文件夹不存在: {localBundleSourcePath}");
                    return false;
                }

                // 使用动态的 remoteBasePath
                string remoteVersionBasePath = $"{remoteBasePath.TrimEnd('/')}/{projectName}/Version";
                string remoteVersionPath = $"{remoteVersionBasePath}/{versionFolderName}";
                string remoteVersionArchivePath = $"{remoteVersionBasePath}/{versionFolderName}.zip";

                string remoteAssetsProjectPath = $"{config.RemoteBasePath}/Assets/{projectName}";   // 同步修改 Assets 路径
                string remoteBundleSourcePath = $"{remoteAssetsProjectPath}/{bundleSourceFolderName}";
                string remoteBundleArchivePath = $"{remoteAssetsProjectPath}/{bundleSourceFolderName}.zip";

                string symlinkPath = $"{remoteBasePath}/{projectName}/Current";   // 软链接也指向对应环境的根目录

                Debug.Log($"[SSHUploader] 开始上传构建版本到服务器...");
                Debug.Log($"[SSHUploader] 项目名称: {projectName}");
                Debug.Log($"[SSHUploader] 版本文件夹: {versionFolderName}");
                Debug.Log($"[SSHUploader] BuildOutput远程路径: {remoteVersionPath}");
                Debug.Log($"[SSHUploader] BundleSource远程路径: {remoteBundleSourcePath}");

                onProgress?.Invoke("准备远端目录...");
                bool isCi = IsRunningInCi();
                // CI 下过短的单次 SSH 超时易导致 mkdir 误失败；仍保持较少重试以免拖长总时间
                int prepareTimeoutSeconds = isCi ? 120 : 1200;
                int prepareMaxAttempts = isCi ? 3 : 10;
                int prepareInitialBackoffSeconds = isCi ? 5 : 10;
                if (isCi)
                {
                    Debug.Log(
                        $"[SSHUploader][CI-DIAG] Prepare remote dir: timeout={prepareTimeoutSeconds}s, maxAttempts={prepareMaxAttempts}, initialBackoff={prepareInitialBackoffSeconds}s");
                }

                // CLI：BuildScript 用 GetAwaiter().GetResult() 阻塞主线程；Jenkins 可能未 -batchmode，须显式检测。
                // Task.Delay(0) 在部分 Unity 版本上仍可能把延续派回主线程；用空 Task.Run 强制线程池更稳。
                // 内层 ssh/scp 的 await 全部 ConfigureAwait(false)，避免漏网之鱼。
                // 是否 hop 线程池须在主线程判定（见 Prewarm）；此处可能在 QueueUserWorkItem 的工作线程上执行。
                if (GetShouldUseThreadPoolHopPreferPrewarmed())
                {
                    await Task.Run(() => { }).ConfigureAwait(false);
                }

                bool prepareSuccess = await ExecuteSSHCommandWithRetryAsync(
                    $"mkdir -p \"{remoteVersionBasePath}\" \"{remoteAssetsProjectPath}\"",
                    onProgress,
                    timeoutSeconds: prepareTimeoutSeconds,
                    maxAttempts: prepareMaxAttempts,
                    initialBackoffSeconds: prepareInitialBackoffSeconds).ConfigureAwait(false);
                if (!prepareSuccess)
                {
                    Debug.LogError("[SSHUploader] 远端目录准备失败");
                    return false;
                }

                string versionArchivePath = string.Empty;
                string bundleArchivePath = string.Empty;
                try
                {
                    versionArchivePath = CreateDirectoryArchive(
                        buildOutputPath,
                        versionFolderName,
                        "步骤1/5: 打包构建输出...",
                        onProgress);

                    bundleArchivePath = CreateDirectoryArchive(
                        localBundleSourcePath,
                        bundleSourceFolderName,
                        "步骤2/5: 打包 BundleSource...",
                        onProgress);

                    onProgress?.Invoke("步骤3/5: 上传构建输出压缩包...");
                    bool versionUploadSuccess = await UploadFileAsync(versionArchivePath, remoteVersionArchivePath, onProgress)
                        .ConfigureAwait(false);
                    if (!versionUploadSuccess)
                    {
                        Debug.LogError("[SSHUploader] 构建输出压缩包上传失败");
                        return false;
                    }

                    onProgress?.Invoke("步骤4/5: 上传 BundleSource 压缩包...");
                    bool bundleUploadSuccess = await UploadFileAsync(bundleArchivePath, remoteBundleArchivePath, onProgress)
                        .ConfigureAwait(false);
                    if (!bundleUploadSuccess)
                    {
                        Debug.LogError("[SSHUploader] BundleSource 压缩包上传失败");
                        return false;
                    }

                    onProgress?.Invoke("步骤5/5: 远端部署（清理/解压/删zip/更新软链）...");
                    string remoteDeployCommand = BuildRemoteDeployCommand(
                        remoteVersionBasePath,
                        remoteAssetsProjectPath,
                        remoteVersionPath,
                        remoteBundleSourcePath,
                        remoteVersionArchivePath,
                        remoteBundleArchivePath,
                        symlinkPath,
                        projectName);

                    bool deploySuccess = await ExecuteSSHCommandWithRetryAsync(
                        remoteDeployCommand,
                        onProgress,
                        timeoutSeconds: 3000,
                        maxAttempts: 10,
                        initialBackoffSeconds: 20).ConfigureAwait(false);
                    if (!deploySuccess)
                    {
                        Debug.LogWarning("[SSHUploader] 上传成功，但服务器解压或软链接更新失败");
                        return false;
                    }
                }
                finally
                {
                    if (!string.IsNullOrEmpty(versionArchivePath) && File.Exists(versionArchivePath))
                    {
                        File.Delete(versionArchivePath);
                    }

                    if (!string.IsNullOrEmpty(bundleArchivePath) && File.Exists(bundleArchivePath))
                    {
                        File.Delete(bundleArchivePath);
                    }
                }

                Debug.Log("[SSHUploader] ✅ 构建版本上传和软链接更新全部完成！");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SSHUploader] 上传构建版本异常: {e.Message}\n{e.StackTrace}");
                return false;
            }
            finally
            {
                _cliPrewarmedProjectName = null;
                _cliPrewarmedShouldThreadPoolHop = null;
            }
        }

        /// <summary>
        /// 根据构建类型获取BundleSource文件夹名称
        /// </summary>
        private static string GetBundleSourceFolderName(BuildScript.BuildTypeEnum buildType)
        {
            switch (buildType)
            {
                case BuildScript.BuildTypeEnum.DEV_BUILD:
                    return "BundleSource_DEV";
                case BuildScript.BuildTypeEnum.UAT_BUILD:
                    return "BundleSource_UAT";
                case BuildScript.BuildTypeEnum.RELEASE_BUILD:
                    return "BundleSource";
                default:
                    return "BundleSource_DEV";
            }
        }

        /// <summary>
        /// 更新软链接（通用方法）。遇到网络/偶发 I/O 等可恢复错误时会自动重试。
        /// </summary>
        public static async Task<bool> UpdateSymlinkAsync(
            string symlinkPath,
            string targetPath,
            Action<string> onProgress = null)
        {
            try
            {
                // 构建命令：ln -sfn 目标路径 软链接路径
                string command = $"sudo ln -sfn \"{targetPath}\" \"{symlinkPath}\"";

                onProgress?.Invoke($"更新软链接: {targetPath} -> {symlinkPath}");
                Debug.Log($"[SSHUploader] 更新软链接: {targetPath} -> {symlinkPath}");

                // 软链更新一般很快，这里给适中 timeout 与少量重试即可
                bool success = await ExecuteSSHCommandWithRetryAsync(
                    command,
                    onProgress,
                    timeoutSeconds: 300,
                    maxAttempts: 5,
                    initialBackoffSeconds: 5).ConfigureAwait(false);

                if (success)
                {
                    onProgress?.Invoke("软链接更新成功！");
                    Debug.Log($"[SSHUploader] ✅ 软链接更新成功");
                    return true;
                }
                else
                {
                    Debug.LogError("[SSHUploader] ❌ 软链接更新失败");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SSHUploader] 更新软链接异常: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }
        
        
        /// <summary>
        /// 根据构建类型返回对应的远程根路径
        /// </summary>
        public static string GetRemoteBasePathByBuildType(BuildScript.BuildTypeEnum buildType)
        {
            return buildType switch
            {
                BuildScript.BuildTypeEnum.DEV_BUILD => "/www/wwwroot/game-dev",
                BuildScript.BuildTypeEnum.UAT_BUILD => "/www/wwwroot/game-uat",
                BuildScript.BuildTypeEnum.RELEASE_BUILD => "/www/wwwroot/game",
                _ => "/www/wwwroot/game-dev"  // 默认回退到 dev
            };
        }
    }
}