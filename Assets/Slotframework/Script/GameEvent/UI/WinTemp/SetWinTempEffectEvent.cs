using CriminalMakers.GameEventHub;
using Slot.Common.UI;

public class SetWinTempEffectEvent : GameEvent
{
    public WinTemp.EffectEnum EffectType { get; private set; }

    public SetWinTempEffectEvent(WinTemp.EffectEnum effectType)
    {
        EffectType = effectType;
    }
}
