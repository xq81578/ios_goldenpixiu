using System.Collections.Generic;
using CriminalMakers.GameEventHub.Utilities;

namespace CriminalMakers.GameEventHub
{
    [SystemEvent] [ExcludeSubclassSelector]
    public class OnEventRaised : GameEvent
    {
        public EventActorData Emitter { get; }
        public GameEvent EventRaised { get; }
        public List<EventActorData> Subscribers { get; }
        public int SubscribersCalledCount { get; private set; }
        public int TotalSubscribersCount { get; private set; }

        public string SubscribersCalledString { get; private set; }

        public OnEventRaised()
        {
        }

        public OnEventRaised(EventActorData emitter, GameEvent eventRaised, List<EventActorData> subscribers)
        {
            Emitter = emitter;
            EventRaised = eventRaised;
            Subscribers = subscribers;

            FormatSubscribersCount();
            FormatSubscriberCallMessage();
        }

        private void FormatSubscribersCount()
        {
            SubscribersCalledCount = Subscribers?.Count ?? 0;
            TotalSubscribersCount =
                GameEventHub.GameEventHubRegistry.ContainsKey(
                    GameEventsHelper.GetEventBindingKey(EventRaised.GetType()))
                    ? GameEventHub.GameEventHubRegistry[GameEventsHelper.GetEventBindingKey(EventRaised.GetType())]
                        .Count
                    : 0;
        }

        private void FormatSubscriberCallMessage()
        {
            if (SubscribersCalledCount == 0 && TotalSubscribersCount == 0)
            {
                SubscribersCalledString = "No subscribers";
            }
            else if (SubscribersCalledCount != TotalSubscribersCount)
            {
                SubscribersCalledString = $"{SubscribersCalledCount}/{TotalSubscribersCount} subscriber(s) called";
            }
            else
            {
                SubscribersCalledString = $"{SubscribersCalledCount} subscribers called";
            }
        }


        public override string ToString()
        {
            return $"<b>{EventRaised.GetType().Name}</b> raised";
        }
    }
}