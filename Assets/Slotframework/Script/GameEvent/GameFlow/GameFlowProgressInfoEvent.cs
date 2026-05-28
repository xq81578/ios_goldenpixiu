using CriminalMakers.GameEventHub;

/// <summary>
/// GameFlowManager 處理進度事件
/// </summary>
public class GameFlowProgressInfoEvent : GameEvent
{
    public string ProgressInfo { get; private set; }

    public GameFlowProgressInfoEvent SetProgressInfo(string progressInfo)
    {
        ProgressInfo = progressInfo;
        return this;
    }
}