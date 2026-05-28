using System.Collections.Generic;

namespace CriminalMakers.GameEventHub
{
    public class OnlyEssentialAndCleanup : ISubscriberFilter
    {
        public List<AbstractAttributeBound<OnGameEvent>.BindingInfo> Filter(GameEvent originalEvent,
            List<AbstractAttributeBound<OnGameEvent>.BindingInfo> bindings)
        {
            // Return an empty list, because filters are only applied for non-essential and non-cleanup subscribers
            return new List<AbstractAttributeBound<OnGameEvent>.BindingInfo>();
        }
        
        public override string ToString()
        {
            return "OnlyEssentialAndCleanup";
        }
    }
}