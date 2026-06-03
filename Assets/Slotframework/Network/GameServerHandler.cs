using System;
using System.Collections.Generic;
using System.Text;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.WebSockets;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Protobuf.Gateway;
using Sirenix.OdinInspector;
using UnityEngine;

public class GameServerHandler : Singleton<GameServerHandler>
{
    // [SerializeField, InlineEditor]
    // private ServerSettingScriptableObject _serverSetting;
    [SerializeField] private bool _isFakeMode = false;
    [SerializeField] private FakeData[] _fakeData;

    public bool IsConnected => _webSocket != null && _webSocket.IsOpen;

    public bool IsReconnected
    {
        get { return _reconnected; }
    }

    public event EventHandler OnOpen;
    public event EventHandler<string> OnSendFail;
    public event EventHandler OnConnectTimeout;

    #region Connect Setting

    private WebSocket _webSocket; // 遊戲WebSocket物件
    private string _webSocketUri; // WebSocket 路徑
    private int _reconnectMaxCount = 5;
    private int _reconnectUnitTime = 2;

    #endregion

    private bool _reconnected = false;
    private bool _reconnecting = false;
    private bool _blockReconnect = false; // 收到 20002/5014 时置为 true
    private int _reconnectCount = 0;
    private Dictionary<string, Action<ResponseInfo, object>> _responseActions = new();
    private Dictionary<string, Action<ResInfo, object>> _resActions = new();
    private int _responseCode = 0;

    // 暫存已發送的id
    private List<string> _pendingCommandIds = new();

    protected override void Awake()
    {
        base.Awake();
    }

    private void OnDestroy()
    {
        DisConnect();
    }

    public void Connect(string webSocketUri = null)
    {
        if (BaseGameService.LocalOnlyMode)
        {
            LogUtils.Log("[GameServerHandler] LocalOnlyMode enabled, skip websocket connect.");
            return;
        }

        if (webSocketUri != null)
        {
            _webSocketUri = webSocketUri;
        }

        LogUtils.Log("Connect to " + _webSocketUri + ".");
        _webSocket = new WebSocket(new Uri(_webSocketUri));
        _webSocket.OnOpen += OnWebSocketOpen;
        _webSocket.OnClosed += OnWebSocketClosed;
        _webSocket.OnMessage += OnWebSocketMessage;
        _webSocket.OnBinary += OnWebSocketBinary;

        _webSocket.Open();
    }

    public void DisConnect()
    {
        if (_webSocket != null)
        {
            _webSocket.OnOpen = null;
            _webSocket.OnClosed = null;
            _webSocket.OnMessage = null;
            _webSocket.Close();
            _webSocket = null;
            _responseActions.Clear();
        }
    }

    public async void Reconnect()
    {
        LogUtils.Log("WebSocket Reconnect. " + _reconnectCount);
        try
        {
            _webSocket = null;
            _reconnected = true;
            _reconnecting = true;
            await UniTask.Delay(_reconnectCount * _reconnectUnitTime * 1000);

            Connect();
            LoadingCircleMediator.ShowLoadingCircle();
            _reconnectCount++;
        }

        finally
        {
            LogUtils.Log("WebSocket Reconnect End.");
            _reconnecting = false;
        }
    }

    public void AddResponseAction<T>(string cmd, Action<ResInfo, T> action)
    {
        if (!_resActions.ContainsKey(cmd))
        {
            _resActions[cmd] = (info, obj) => action(info, (T)obj);
        }
        else
        {
            LogUtils.LogWarning($"Command {cmd} already has an action assigned.");
        }
    }
    // public void AddResponseAction<T>(string cmd, Action<ResponseInfo, T> action)
    // {
    //     if (!_responseActions.ContainsKey(cmd))
    //     {
    //         _responseActions[cmd] = (info, obj) => action(info, (T)obj);
    //     }
    //     else
    //     {
    //         LogUtils.LogWarning($"Command {cmd} already has an action assigned.");
    //     }
    // }

    public void RemoveResponseAction(string cmd)
    {
        if (_responseActions.ContainsKey(cmd))
        {
            _responseActions.Remove(cmd);
        }

        if (_resActions.ContainsKey(cmd))
        {
            _resActions.Remove(cmd);
        }
    }

    public void Send(string cmd, IMessage data)
    {
        string requestId = Guid.NewGuid().ToString();
        LogUtils.Log($"SendCommend: cmd: {cmd}, requestId: {requestId},data: {data}");

        PacketCmd packetCmd = new()
        {
            Cmd = cmd,
            RequestId = requestId,
            Data = data.ToByteString()
        };

        // 暫存送出的指令
        _pendingCommandIds.Add(packetCmd.RequestId);
        byte[] sendByte = packetCmd.ToByteArray();

        if (EncryptionHelper.CheckKey())
        {
            sendByte = EncryptionHelper.AESEncrypt(sendByte);
            if (sendByte == null)
            {
                DialogMediator.ShowDialog(
                    CommonDefine.DialogTableName, CommonDefine.DialogKey_SystemTitle,
                    CommonDefine.DialogTableName, "Error_Unknown",
                    new ActionButton("OK", () => { new ErrorLogEvent().Publish(this); }), false
                );

                return;
            }

            if (!SendCommand(sendByte))
            {
                OnSendFail?.Invoke(this, cmd);
            }

            return;
        }

        if (!SendCommand(sendByte))
        {
            OnSendFail?.Invoke(this, cmd);
        }
    }

    public void Send(string cmd, byte[] data)
    {
        string requestId = Guid.NewGuid().ToString();
        LogUtils.Log($"SendCommend: cmd: {cmd}, requestId: {requestId},data: {data}");

        PacketCmd packetCmd = new()
        {
            Cmd = cmd,
            RequestId = requestId,
            Data = ByteString.CopyFrom(data)
        };
        byte[] sendByte = packetCmd.ToByteArray();

        if (!SendCommand(sendByte))
        {
            OnSendFail?.Invoke(this, cmd);
        }
    }

    private bool SendCommand(string sendJson)
    {
        if (!IsConnected)
        {
            _responseCode = -1;
            LoadingCircleMediator.HideLoadingCircle();
            return false;
        }


        _webSocket.Send(sendJson);
        return true;
    }

    private bool SendCommand(byte[] sendByte)
    {
        if (!IsConnected)
        {
            _responseCode = -1;
            LoadingCircleMediator.HideLoadingCircle();
            return false;
        }

        LoadingCircleMediator.ShowLoadingCircle(3f);
        _webSocket.Send(sendByte);
        return true;
    }

    private void ReceivedResponse(byte[] message)
    {
        LoadingCircleMediator.HideLoadingCircle();
        ResponseInfo responseInfo = null;
        try
        {
            responseInfo = ResponseInfo.Parser.ParseFrom(message);
        }
        catch (Exception ex)
        {
            _reconnecting = false;
            _responseCode = -1;
            LogUtils.LogError("ResponseInfo Parse Error: " + ex);
            DialogMediator.ShowDialog(
                CommonDefine.DialogTableName, CommonDefine.DialogKey_SystemTitle,
                CommonDefine.DialogTableName, "Error_Network",
                new ActionButton("OK", () => { new ErrorLogEvent().Publish(this); }), false
            );
            DisConnect();
            return;
        }

        _pendingCommandIds.RemoveAll(x => x == responseInfo.RequestId);
        _responseCode = responseInfo.Code;
        byte[] res = responseInfo.Data.ToByteArray();
        string resultString = responseInfo.ToString();
        if (_isFakeMode)
        {
            foreach (var fake in _fakeData)
            {
                if (fake.cmd == responseInfo.Cmd)
                {
                    responseInfo.Data = fake.Data;
                    break;
                }
            }
        }

        LogUtils.Log(
            $"Response: Cmd: {responseInfo.Cmd}, Code: {responseInfo.Code}, Message: {responseInfo.Message}, RequestId: {responseInfo.RequestId}");
        if (_responseActions.TryGetValue(responseInfo.Cmd, out var action))
        {
            var genericAction = action;
            if (genericAction != null)
            {
                if (responseInfo.Cmd == "GetAESKey")
                {
                    if (res.Length != 0)
                    {
                        // 由RSA公鑰加密過的AES金鑰
                        byte[] data = EncryptionHelper.Decrypt(res);
                        if (data == null)
                        {
                            DialogMediator.ShowDialog(
                                CommonDefine.DialogTableName, CommonDefine.DialogKey_SystemTitle,
                                CommonDefine.DialogTableName, "Error_Unknown",
                                new ActionButton("OK", () => { new ErrorLogEvent().Publish(this); }), false
                            );
                            return;
                        }

                        EncryptionHelper.Save(data);
                    }

                    genericAction(responseInfo, null);
                }
                else
                {
                    genericAction(responseInfo, responseInfo.Data);
                }
            }
        }
    }

    private void OnWebSocketOpen(WebSocket webSocket)
    {
        LogUtils.Log("WebSocket Open!");
        _reconnecting = false;
        _reconnectCount = 0;
        LoadingCircleMediator.HideLoadingCircle();
        OnOpen?.Invoke(this, EventArgs.Empty);
    }

    private void OnWebSocketClosed(WebSocket webSocket, WebSocketStatusCodes code, string message)
    {
        LogUtils.Log($"WebSocket closed! Code: {code} Message: {message}");
        if (_blockReconnect)
        {
            LoadingCircleMediator.HideLoadingCircle();
            return;
        }

        if (_reconnecting) return;

        // 未收到 Server 錯誤訊息
        // 未有尚未收到回應的 Command
        // 且重連次數未達上限
        // 就進行重連流程

        var _pendingCommand = _pendingCommandIds.Count == 0 ||
                          (_pendingCommandIds.Count > 0 && !_pendingCommandIds.Contains("spin"));

        if (_responseCode == 0 && _pendingCommand && (_reconnectCount < _reconnectMaxCount))
        {
            EncryptionHelper.Save(null);
            Reconnect();
            return;
        }

        LoadingCircleMediator.HideLoadingCircle();

        DialogMediator.ShowDialog(
            CommonDefine.DialogTableName, CommonDefine.DialogKey_SystemTitle,
            CommonDefine.DialogTableName, "Error_Network",
            new ActionButton("OK", () => { new ErrorLogEvent().Publish(this); }), false
        );
    }

    private void OnWebSocketMessage(WebSocket webSocket, string message)
    {
        LogUtils.Log("WebSocket recivied [" + message + "]!");
        ReceivedResponse(message);
    }

    private void OnWebSocketBinary(WebSocket webSocket, BufferSegment bs)
    {
        LogUtils.Log("WebSocket recivied!");

        // 取得本次有效的資料
        byte[] receivedData = new byte[bs.Count];
        Array.Copy(bs.Data, bs.Offset, receivedData, 0, bs.Count);

        if (EncryptionHelper.CheckKey())
        {
            receivedData = EncryptionHelper.AESDecrypt(receivedData);
            if (receivedData == null)
            {
                LogUtils.LogError("Data is Empty");
                return;
            }
        }
        else
        {
            LogUtils.LogWarning("Key is Null");
        }

        ReceivedResponse(receivedData);
    }


    public void Send(string cmd, JObject data)
    {
        PacketCmdDebug packetCmd = new()
        {
            cmd = cmd,
            data = data
        };
        string sendJson = JsonConvert.SerializeObject(packetCmd);
        LogUtils.Log($"SendCommend: cmd: {cmd} + , data: {sendJson}");
        _pendingCommandIds.Add(cmd);
        if (!SendCommand(sendJson) && !_blockReconnect)
        {
            OnSendFail?.Invoke(this, cmd);
        }
    }


    private void ReceivedResponse(string message)
    {
        ResInfo responseInfo = JsonConvert.DeserializeObject<ResInfo>(message);
        if (responseInfo.cmd == "20002" && (responseInfo.code == 5014 || responseInfo.code == 5000))
        {
            // 标记不要再重连
            _blockReconnect = true;
        }

        JToken resultString = responseInfo.data;
        if (_isFakeMode)
        {
            foreach (var fake in _fakeData)
            {
                if (fake.cmd == responseInfo.cmd)
                {
                    resultString = JsonConvert.DeserializeObject<JObject>(fake.DataFile.text);
                    break;
                }
            }
        }

        _pendingCommandIds.RemoveAll(x => x == responseInfo.cmd);
        LogUtils.Log("resultString: " + resultString);
        LogUtils.Log("responseInfo.cmd===" + responseInfo.cmd);
        if (_resActions.TryGetValue(responseInfo.cmd, out var action))
        {
            var genericAction = action;
            if (genericAction != null)
            {
                // var result = JsonConvert.DeserializeObject(resultString, genericAction.Method.GetParameters()[1].ParameterType);
                genericAction(responseInfo, resultString);
            }
        }
    }
}

[Serializable]
public class FakeData
{
    public string cmd;
    [SerializeField] public TextAsset DataFile;
    public byte[] ByteArrayData => HexStringToByteArray(DataFile.text);
    public ByteString Data => ByteString.CopyFrom(ByteArrayData);

    // Helper method to convert hex string to byte array
    private static byte[] HexStringToByteArray(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return Array.Empty<byte>();
        int length = hex.Length;
        byte[] bytes = new byte[length / 2];
        for (int i = 0; i < length; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }
}
