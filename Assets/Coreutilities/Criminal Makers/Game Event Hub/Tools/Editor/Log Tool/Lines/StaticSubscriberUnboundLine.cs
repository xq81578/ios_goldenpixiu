using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class StaticSubscriberUnboundLine : BaseListViewLine<GameEvent>
    {
        public override VisualElement BindLogLine(VisualElement ve, GameEvent item)
        {
            OnObjectUnboundFromEventSystem onObjectUnbound = (OnObjectUnboundFromEventSystem)item;

            // Icons
            ve.Q<Image>("dynamic_subscriber").style.display = DisplayStyle.None;
            ve.Q<Image>("static_subscriber").style.display = DisplayStyle.Flex;
            ve.Q<Image>("event_raised").style.display = DisplayStyle.None;

            // Main Label
            ve.Q<Label>("main-log-text").text = onObjectUnbound.ToString();

            // Subscribers count
            ve.Q<Label>("subscribers-count").style.display = DisplayStyle.None;

            // Buttons
            var ping = ve.Q<Button>("ping");
            ping.style.display = DisplayStyle.Flex;
            ping.clicked -= null;
            ping.clicked += () => { GameEventsHelper.PingGameobject(onObjectUnbound.unboundObject); };
            ve.Q<Button>("open-data").style.display = DisplayStyle.None;
            ve.Q<Button>("open-actors").style.display = DisplayStyle.None;

            return ve;
        }
        
        protected override ContextualMenuManipulator InternalAppendRightClickMenu(VisualElement ve, GameEvent gameEvent, int index)
        {
            var unbound = (OnObjectUnboundFromEventSystem)gameEvent;
            return new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Timestamp: " + gameEvent._timestamp.ToString("HH:mm:ss"),
                    _ => { }, DropdownMenuAction.AlwaysDisabled);
                evt.menu.AppendSeparator();
                
                evt.menu.AppendAction("Edit subscriber script",
                    _ => { GameEventsHelper.OpenScript(unbound.unboundObject.GetType()); },
                    DropdownMenuAction.AlwaysEnabled);
                
                evt.menu.AppendAction("Force bind",
                    _ => { GameEventHub.Bind(unbound.unboundObject); },
                    DropdownMenuAction.AlwaysEnabled);
            });
        }

        public override bool IsLineApplicable(GameEvent item)
        {
            if (item is OnObjectUnboundFromEventSystem onObjectUnbound)
            {
                return onObjectUnbound.isStatic;
            }

            return false;
        }
    }
}