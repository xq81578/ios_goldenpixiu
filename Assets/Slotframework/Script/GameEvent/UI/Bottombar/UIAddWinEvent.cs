using CriminalMakers.GameEventHub;

/// <summary>
/// UI Add Win 事件
/// </summary>
public class UIAddWinEvent : GameEvent
{
    public double AddWin { get; set; }
    public UIAddWinEvent(double addWin)
    {
        AddWin = addWin;
    }
}
