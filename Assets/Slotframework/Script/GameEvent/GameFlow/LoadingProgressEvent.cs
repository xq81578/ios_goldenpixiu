using CriminalMakers.GameEventHub;

/// <summary>
/// 進度通知事件
/// </summary>
public class LoadingProgressEvent : GameEvent
{
    public float Progress { get; private set; }

    public LoadingProgressEvent SetProgress(float progress)
    {
        Progress = progress;
        return this;
    }
}