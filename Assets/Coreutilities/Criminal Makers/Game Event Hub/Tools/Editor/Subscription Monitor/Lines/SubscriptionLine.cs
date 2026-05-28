using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class SubscriptionLine: BaseListViewLine<string>
    {
        public override VisualElement BindLogLine(VisualElement ve, string item)
        {
            var type = Type.GetType(item);
            var eventName = GameEventsHelper.GetEventName(type);
            ve.Q<Label>("event-name").text = eventName ?? "Unknown";

            ve.Q<Label>("subscribers-label").text = $"{GameEventHub.GameEventHubRegistry[item].Count.ToString()} Subscribers";

            ve.Q<Button>("data").clickable = null;
            ve.Q<Button>("data").clicked += () =>
            {
                List<EventActorData> actors = new List<EventActorData>();
                foreach (var subscriber in GameEventHub.GameEventHubRegistry[item])
                {
                    actors.Add(new EventActorData(subscriber.Subscriber.ToString(), subscriber.Subscriber));
                }

                EventActorsTool.ShowWindow(null, actors, eventName);
            };
            
            return ve;
        }

        protected override ContextualMenuManipulator InternalAppendRightClickMenu(VisualElement ve, string data, int index)
        {
            return null;
        }

        public override bool IsLineApplicable(string item)
        {
            return true;
        }
    }
}