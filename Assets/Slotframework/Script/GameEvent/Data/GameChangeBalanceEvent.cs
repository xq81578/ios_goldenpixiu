using CriminalMakers.GameEventHub;

/// <summary>
/// 遊戲變更餘額事件
/// </summary>
public class GameChangeBalanceEvent : GameEvent
{
    public bool ShowNetWin { get; private set; }

    public GameChangeBalanceEvent()
    {
        
    }

    public GameChangeBalanceEvent(bool showNetWin)
    {
        ShowNetWin = showNetWin;
    }
}