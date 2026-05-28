using System;
using CriminalMakers.GameEventHub;

/// <summary>
/// Request system URLs refresh and notify completion.
/// </summary>
public class RefreshSystemUrlRequestEvent : GameEvent
{
    public Action<bool> OnCompleted { get; }

    public RefreshSystemUrlRequestEvent(Action<bool> onCompleted)
    {
        OnCompleted = onCompleted;
    }
}
