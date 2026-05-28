using System.Collections.Generic;
using UnityEngine;

namespace CriminalMakers.GameEventHub
{
    public class SameSceneAsEmitter : ISubscriberFilter
    {
        public List<AbstractAttributeBound<OnGameEvent>.BindingInfo> Filter(GameEvent originalEvent,
            List<AbstractAttributeBound<OnGameEvent>.BindingInfo> bindings)
        {
            GameObject emitterGameobject = SubscriberFilterHelper.ExtractGameObject(originalEvent._emitter);

            if (emitterGameobject == null)
            {
                return bindings;
            }

            bindings.RemoveAll(bind =>
            {
                GameObject subscriberGameObject = SubscriberFilterHelper.ExtractGameObject(bind.Subscriber);
                return subscriberGameObject == null || emitterGameobject.scene != subscriberGameObject.scene;
            });
            return bindings;
        }

        public override string ToString()
        {
            return "Only in the same scene as the emitter";
        }
    }
}