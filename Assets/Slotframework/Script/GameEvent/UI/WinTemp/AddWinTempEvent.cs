using CriminalMakers.GameEventHub;

public class AddWinTempEvent : GameEvent
{
    public double Add { get; private set; }

    public AddWinTempEvent(double add)
    {
        Add = add;
    }
}
