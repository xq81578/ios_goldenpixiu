using UnityEngine;
using Newtonsoft.Json.Linq;
using VContainer;
using Slot.Common;


public class ListenToCmd20001 : MonoBehaviour
{
    private const string CMD_20001 = "20001";

    [Inject]
    private PlayerBetData _playerBetData;
    
    void Start()
    {
        // 添加对cmd=20001的监听
        GameServerHandler.Instance.AddResponseAction<JObject>(CMD_20001, OnReceivedCmd20001);
    }
    
    void OnDestroy()
    {
        // 移除监听器，防止内存泄漏
        GameServerHandler.Instance.RemoveResponseAction(CMD_20001);
    }
    
    /// <summary>
    /// 当收到cmd=20001消息时会被调用
    /// </summary>
    /// <param name="info">响应信息</param>
    /// <param name="data">响应数据</param>
    private void OnReceivedCmd20001(ResInfo info, JObject data)
    {
        // Debug.Log($"收到cmd={CMD_20001}的消息");
        // Debug.Log($"响应码: {info.code}");
        // Debug.Log($"消息内容: {info.message}");
        // Debug.Log($"数据内容: {data?.ToString()}");
        
        // 在这里处理你的业务逻辑
        // 例如解析JObject数据并执行相应操作
        HandleCmd20001Data(data);
    }
    
    /// <summary>
    /// 处理cmd=20001的数据
    /// </summary>
    /// <param name="data">从服务器接收到的数据</param>
    private void HandleCmd20001Data(JObject data)
    {
        // 根据实际的数据结构来解析和处理数据
        // 示例:
        ulong balance = (ulong)data["balance"];
        _playerBetData.Balance = ServiceUtils.ToClientBalance(balance);
        //  new GameChangeBalanceEvent().Publish(this);

        
        // 执行相应的游戏逻辑...
    }
}