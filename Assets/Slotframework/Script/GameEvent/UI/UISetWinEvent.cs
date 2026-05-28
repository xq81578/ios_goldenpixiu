using CriminalMakers.GameEventHub;

/// <summary>
/// UI Set Win 事件
/// </summary>
public class UISetWinEvent : GameEvent
{
    public double SetWin { get; set; }

    public UISetWinEvent(double setWin)
    {
        SetWin = setWin;
    }
}
