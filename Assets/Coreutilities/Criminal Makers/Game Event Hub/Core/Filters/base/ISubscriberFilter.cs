using System.Collections.Generic;

namespace CriminalMakers.GameEventHub
{
    public interface ISubscriberFilter
    {
        List<AbstractAttributeBound<OnGameEvent>.BindingInfo> Filter(GameEvent originalEvent, List<AbstractAttributeBound<OnGameEvent>.BindingInfo> bindings);
    }
}