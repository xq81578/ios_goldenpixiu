using CriminalMakers.GameEventHub.Utilities;

namespace CriminalMakers.GameEventHub
{
    [SystemEvent] [ExcludeSubclassSelector]
    public class OnObjectBoundToEventSystem : GameEvent
    {
        public object BoundObject;
        public bool isStatic = false;

        public OnObjectBoundToEventSystem(object boundObject, bool isStatic)
        {
            BoundObject = boundObject;
            this.isStatic = isStatic;
        }
        
        public override string ToString()
        {
            return $"{(isStatic ? "Static" : "Dynamic")} <b>{BoundObject.GetType().Name}</b> bound";
        }
    }
}