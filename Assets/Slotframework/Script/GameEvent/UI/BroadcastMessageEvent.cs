using CriminalMakers.GameEventHub;

/// <summary>
/// 服务器广播消息事件。
/// 用法：
/// new BroadcastMessageEvent(BroadcastMessageType.EpicWin, "Congrats Alice won 10000").Publish(this);
/// </summary>
public enum BroadcastMessageType
{
    MaintenanceNotice = 1,
    RiskNotice = 2,
    JackpotBroadcast = 3,
    EpicWin = 4,
    SuperWin = 5,
    EventNotice = 6,
    PaymentNotice = 7,
    NewGameNotice = 8,
    WinBroadcast = 9,
    SystemNotice = 10,
}

public class BroadcastMessageEvent : GameEvent
{
    public int Type { get; private set; } = (int)BroadcastMessageType.SystemNotice;
    public string Content { get; private set; } = string.Empty;

    public BroadcastMessageEvent() { }

    public BroadcastMessageEvent(int type, string content)
    {
        Set(type, content);
    }

    public BroadcastMessageEvent(BroadcastMessageType type, string content)
    {
        Set(type, content);
    }

    public BroadcastMessageEvent Set(int type, string content)
    {
        Type = type;
        Content = content ?? string.Empty;
        return this;
    }

    public BroadcastMessageEvent Set(BroadcastMessageType type, string content)
    {
        return Set((int)type, content);
    }
}
