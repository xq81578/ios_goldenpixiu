using CriminalMakers.GameEventHub;

/// <summary>
/// Info 開啟事件
/// </summary>
public class InfoUIActiveEvent : GameEvent
{
    public bool IsActive { get; private set; }

    public InfoUIActiveEvent(bool isActive)
    {
        IsActive = isActive;
    }
}
