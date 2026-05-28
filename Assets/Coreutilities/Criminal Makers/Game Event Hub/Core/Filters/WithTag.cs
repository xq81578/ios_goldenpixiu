using System.Collections.Generic;

namespace CriminalMakers.GameEventHub
{
    public class WithTag : ISubscriberFilter
    {
        private string _tag;

        public WithTag(string tag)
        {
            _tag = tag;
        }

        public List<AbstractAttributeBound<OnGameEvent>.BindingInfo> Filter(GameEvent originalEvent,
            List<AbstractAttributeBound<OnGameEvent>.BindingInfo> bindings)
        {
            bindings.RemoveAll(bind =>
            {
                var gameObject = SubscriberFilterHelper.ExtractGameObject(bind.Subscriber);

                return gameObject == null || gameObject.CompareTag(_tag) == false;
            });

            return bindings;
        }
        
        public override string ToString()
        {
            return $"Only with tag {_tag}";
        }
    }
}