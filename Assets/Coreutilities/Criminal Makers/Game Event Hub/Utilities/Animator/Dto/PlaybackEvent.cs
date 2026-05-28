using System;

namespace CriminalMakers.GameEventHub.Utilities
{
    [Serializable]
    public class PlaybackEvent
    {
        public float playbackTime;

        public GameEventSO associatedEvent;

        [NonSerialized] public int lastTriggeredLoop = -1;
    }
}