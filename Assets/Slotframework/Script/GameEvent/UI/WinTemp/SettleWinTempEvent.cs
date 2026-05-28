using CriminalMakers.GameEventHub;
using Slot.Common;

public class SettleWinTempEvent : GameEvent
{
    public int NewValue { get; private set; }
    public Bottom_MathType MathType { get; private set; }

    public SettleWinTempEvent(int newValue, Bottom_MathType mathType)
    {
        NewValue = newValue;
        MathType = mathType;
    }
}
