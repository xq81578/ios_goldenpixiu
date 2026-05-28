using CriminalMakers.GameEventHub;

/// <summary>
/// Set Record URL Event
/// </summary>
public class SetRecordUrlEvent : GameEvent
{
    public string RecordUrl { get; set; }

    public SetRecordUrlEvent()
    {

    }
    
    public SetRecordUrlEvent(string recordUrl)
    {
        RecordUrl = recordUrl;
    }   
}