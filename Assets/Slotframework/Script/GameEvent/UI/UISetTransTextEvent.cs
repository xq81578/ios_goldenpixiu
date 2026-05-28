using CriminalMakers.GameEventHub;

/// <summary>
/// UI Set Trans Text 事件
/// </summary>
public class UISetTransTextEvent : GameEvent
{
    public string TransText { get; private set; } = "";

    public UISetTransTextEvent() { }

    public UISetTransTextEvent(string transText)
    {
        TransText = transText;
    }
}
