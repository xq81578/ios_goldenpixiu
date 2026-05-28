using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public abstract class BaseListViewLine<T>
    {
        public void AppendRightClickMenu(VisualElement ve, T data, int index)
        {
            if (ve.userData != null)
            {
                ve.RemoveManipulator(ve.userData as ContextualMenuManipulator);
            }
            var newRightClickActions = InternalAppendRightClickMenu(ve, data, index);
            ve.userData = newRightClickActions;
            ve.AddManipulator(newRightClickActions);
        }
        
        public abstract VisualElement BindLogLine(VisualElement ve, T item);

        protected abstract ContextualMenuManipulator InternalAppendRightClickMenu(VisualElement ve, T data,
            int index);
        
        public abstract bool IsLineApplicable(T item);
    }
}