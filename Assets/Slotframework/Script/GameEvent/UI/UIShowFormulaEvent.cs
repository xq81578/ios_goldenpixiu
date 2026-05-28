using CriminalMakers.GameEventHub;
using Slot.Common;

public class UIShowFormulaEvent : GameEvent
{
    public int Value { get; }
    public Bottom_MathType MathType { get; }
    public UIShowFormulaEvent(int value, Bottom_MathType mathType)
    {
        Value = value;
        MathType = mathType;
    }
}
