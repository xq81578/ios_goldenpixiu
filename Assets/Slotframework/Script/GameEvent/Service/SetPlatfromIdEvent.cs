using CriminalMakers.GameEventHub;
using Slot.Common;

/// <summary>
/// Event data for setting the platform ID.
/// </summary>
public class SetPlatformIdEvent : GameEvent
{
    public PlatformType PlatformType { get; set; }

    public SetPlatformIdEvent()
    {
    }

    public SetPlatformIdEvent(PlatformType platformType)
    {
        PlatformType = platformType;
    }
}
