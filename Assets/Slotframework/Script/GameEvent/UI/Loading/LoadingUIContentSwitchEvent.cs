using CriminalMakers.GameEventHub;

public class LoadingUIContentSwitchEvent : GameEvent
{
    public bool Next { get; private set; }

    public LoadingUIContentSwitchEvent SetNext(bool next)
    {
        Next = next;
        return this;
    }
}