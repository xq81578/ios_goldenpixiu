using CriminalMakers.GameEventHub.Utilities;

namespace CriminalMakers.GameEventHub
{
    [SystemEvent] [ExcludeSubclassSelector]
    public class OnEventSystemStarted: GameEvent
    {
    }   
}