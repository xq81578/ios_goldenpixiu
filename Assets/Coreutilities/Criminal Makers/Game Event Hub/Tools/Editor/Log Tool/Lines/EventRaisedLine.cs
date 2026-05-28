using System.Collections.Generic;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class EventRaisedLine : BaseListViewLine<GameEvent>
    {
        public override VisualElement BindLogLine(VisualElement ve, GameEvent item)
        {
            OnEventRaised onEventRaised = (OnEventRaised)item;

            // Icons
            ve.Q<Image>("dynamic_subscriber").style.display = DisplayStyle.None;
            ve.Q<Image>("static_subscriber").style.display = DisplayStyle.None;
            ve.Q<Image>("event_raised").style.display = DisplayStyle.Flex;

            // Main Label
            var messageParts = new List<string>();


            if (GameEventsHelper.EmmitterIsTesterTool(onEventRaised.Emitter.OriginalObject))
            {
                messageParts.Add("<b>Tester Tool</b>");
            }

            if (string.IsNullOrEmpty(onEventRaised.EventRaised._channel) == false)
            {
                // messageParts.Add($"<color=#00ffff>Ch: {onEventRaised.EventRaised._channel}</color>");
            }

            if (onEventRaised.EventRaised._filters != null && onEventRaised.EventRaised._filters.Count > 0)
            {
                messageParts.Add($"<color=#ffa500>Filters: {onEventRaised.EventRaised._filters.Count}</color>");
            }

            if (onEventRaised.EventRaised._nonCancellable)
            {
                messageParts.Add("<color=#800000>Non-Cancellable</color>");
            }

            if (onEventRaised.EventRaised._cancelled)
            {
                messageParts.Add("<color=#ff0000>Cancelled</color>");
            }

            if (onEventRaised.EventRaised._shared)
            {
                messageParts.Add("<color=#00ff00>Shared</color>");
            }

            var extraMessage = messageParts.Count > 0
                ? $"<i>({string.Join(", ", messageParts)})</i>"
                : string.Empty;

            ve.Q<Label>("main-log-text").text = "[" + onEventRaised.EventRaised._channel + "]: " + onEventRaised.ToString() + " " + extraMessage;
            ve.Q<Label>("main-log-text").enableRichText = true;

            // Subscribers count
            ve.Q<Label>("subscribers-count").style.display = DisplayStyle.Flex;
            ve.Q<Label>("subscribers-count").text = FormatSubscriberCallMessage(onEventRaised);

            // Buttons
            ve.Q<Button>("ping").style.display = DisplayStyle.None;

            var openDataBtn = ve.Q<Button>("open-data");
            openDataBtn.style.display = DisplayStyle.Flex;
            openDataBtn.clickable = null;
            openDataBtn.clicked += () => { EventDetailTool.ShowWindow(onEventRaised.EventRaised); };

            var openActorsBtn = ve.Q<Button>("open-actors");
            openActorsBtn.style.display = DisplayStyle.Flex;
            openActorsBtn.clickable = null;
            openActorsBtn.clicked += () =>
            {
                EventActorsTool.ShowWindow(onEventRaised.Emitter, onEventRaised.Subscribers,
                    onEventRaised.EventRaised.GetType().Name);
            };

            return ve;
        }

        protected override ContextualMenuManipulator InternalAppendRightClickMenu(VisualElement ve, GameEvent gameEvent,
            int index)
        {
            var onEventRaised = (OnEventRaised)gameEvent;

            return new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Timestamp: " + gameEvent._timestamp.ToString("HH:mm:ss"),
                    _ => { }, DropdownMenuAction.AlwaysDisabled);
                evt.menu.AppendSeparator();

                evt.menu.AppendAction("Quick repeat",
                    _ => { onEventRaised.EventRaised.CopyEvent().Publish(this); },
                    DropdownMenuAction.AlwaysEnabled);

                evt.menu.AppendAction("Open in tester tool",
                    _ => { TesterTool.ShowWindow(onEventRaised.EventRaised); },
                    DropdownMenuAction.AlwaysEnabled);

                evt.menu.AppendSeparator();

                evt.menu.AppendAction("Edit emitter script",
                    _ => { GameEventsHelper.OpenScript(onEventRaised.Emitter.OriginalObject.GetType()); },
                    DropdownMenuAction.AlwaysEnabled);
                evt.menu.AppendAction("Edit event script",
                    _ => { GameEventsHelper.OpenScript(onEventRaised.EventRaised.GetType()); },
                    DropdownMenuAction.AlwaysEnabled);
                
                evt.menu.AppendSeparator();

                evt.menu.AppendAction("Save event as Scriptable Object",
                    _ => { GameEventsHelper.SaveGameEventAsScriptableObject(onEventRaised.EventRaised); },
                    DropdownMenuAction.AlwaysEnabled);
            });
        }

        private string FormatSubscriberCallMessage(OnEventRaised eventRaised)
        {
            return eventRaised.SubscribersCalledString;
        }

        public override bool IsLineApplicable(GameEvent item)
        {
            return item is OnEventRaised;
        }
    }
}