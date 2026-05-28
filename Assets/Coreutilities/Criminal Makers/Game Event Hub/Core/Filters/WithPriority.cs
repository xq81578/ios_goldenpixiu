using System.Collections.Generic;

namespace CriminalMakers.GameEventHub
{
    public class WithPriority : ISubscriberFilter
    {
        private SubscriberPriority _priority;

        public WithPriority(SubscriberPriority priority)
        {
            _priority = priority;
        }


        public List<AbstractAttributeBound<OnGameEvent>.BindingInfo> Filter(GameEvent originalEvent,
            List<AbstractAttributeBound<OnGameEvent>.BindingInfo> bindings)
        {
            bindings.RemoveAll(bind => GameEventsHelper.GetBindingPriority(bind) != _priority);

            return bindings;
        }

        public override string ToString()
        {
            return "Only with priority: " + _priority;
        }
    }
}