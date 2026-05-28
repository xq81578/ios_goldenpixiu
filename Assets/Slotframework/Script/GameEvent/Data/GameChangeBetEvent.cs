using CriminalMakers.GameEventHub;

/// <summary>
/// 遊戲變更投注事件
/// </summary>
public class GameChangeBetEvent : GameEvent
{
    public double Bet { get; set; }

    public GameChangeBetEvent()
    {
    }
   
    public GameChangeBetEvent(double bet)
    {
        Bet = bet;
    }
}