using System;
using CriminalMakers.GameEventHub;

public class AutoSpinStartEvent : GameEvent
{
    public int TotalSpins { get; set; }
    public int StopRatio { get; set; }
    public double StopLessBalance { get; set; }
    public double StopMoreBalance { get; set; }
    public bool IsStopFreeGame { get; set; }
    public Action<string> Callback { get; set; } = null;
}
