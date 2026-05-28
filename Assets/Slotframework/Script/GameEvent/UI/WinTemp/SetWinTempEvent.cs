using CriminalMakers.GameEventHub;

public class SetWinTempEvent : GameEvent
{
    public double Add { get; private set; }

    public SetWinTempEvent(double add)
    {
        Add = add;
    }
}