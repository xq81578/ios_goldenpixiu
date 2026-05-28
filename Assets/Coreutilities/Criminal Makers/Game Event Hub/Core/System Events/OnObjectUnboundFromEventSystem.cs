using CriminalMakers.GameEventHub.Utilities;

namespace CriminalMakers.GameEventHub
{
    [SystemEvent] [ExcludeSubclassSelector]
    public class OnObjectUnboundFromEventSystem: GameEvent
    {
        public object unboundObject;
        public bool isStatic = false;

        public OnObjectUnboundFromEventSystem(object unboundObject, bool isStatic)
        {
            this.unboundObject = unboundObject;
            this.isStatic = isStatic;
        }
        
        public override string ToString()
        {
            return $"{(isStatic ? "Static" : "Dynamic")} <b>{unboundObject.GetType().Name}</b> unbound";
        }
    }
}