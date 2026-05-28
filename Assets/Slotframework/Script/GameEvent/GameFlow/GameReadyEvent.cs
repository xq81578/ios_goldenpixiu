using CriminalMakers.GameEventHub;

/// <summary>
/// 遊戲準備完成事件
/// </summary>
public class GameReadyEvent : GameEvent
{
    public bool isWaitGameUIReady { get; private set; }
    public GameReadyEvent(bool _isWaitGameUIReady=false)
    {
        isWaitGameUIReady = _isWaitGameUIReady;
    }
}