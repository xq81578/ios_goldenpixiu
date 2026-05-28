using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class EventActorLine : BaseListViewLine<EventActorData>
    {
        public override VisualElement BindLogLine(VisualElement ve, EventActorData item)
        {
            ve.Q<Label>("actor-name").text = item.Name;

            UIToolkitHelpers.BindPingButton(item.OriginalObject, ve);

            return ve;
        }

        protected override ContextualMenuManipulator InternalAppendRightClickMenu(VisualElement ve, EventActorData data,
            int index)
        {
            return null;
        }

        public override bool IsLineApplicable(EventActorData item)
        {
            return true;
        }
    }
}