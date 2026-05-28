using CriminalMakers.GameEventHub;
using Slot.Common;

/// <summary>
/// Game Change Turbo 事件
/// </summary>
public class GameChangeTurboEvent : GameEvent {
    public Bottom_TurboType TurboTypeEnum { get; private set; }
    public GameChangeTurboEvent(Bottom_TurboType turboTypeEnum)
    {
        TurboTypeEnum = turboTypeEnum;
    }
}
