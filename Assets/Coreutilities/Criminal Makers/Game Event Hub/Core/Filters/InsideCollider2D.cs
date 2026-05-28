using System.Collections.Generic;
using UnityEngine;

namespace CriminalMakers.GameEventHub
{
    public class InsideCollider2D: ISubscriberFilter
    {
        private Collider2D _collider;

        public InsideCollider2D(Collider2D collider)
        {
            _collider = collider;
        }

        public List<AbstractAttributeBound<OnGameEvent>.BindingInfo> Filter(GameEvent originalEvent, List<AbstractAttributeBound<OnGameEvent>.BindingInfo> bindings)
        {
            bindings.RemoveAll(bind =>
            {
                GameObject subscriberGameObject = SubscriberFilterHelper.ExtractGameObject(bind.Subscriber);
                return subscriberGameObject == null || !_collider.bounds.Contains(subscriberGameObject.transform.position);
            });
            return bindings;
        }

        public override string ToString()
        {
            return "InsideCollider2D";
        }
    }
}