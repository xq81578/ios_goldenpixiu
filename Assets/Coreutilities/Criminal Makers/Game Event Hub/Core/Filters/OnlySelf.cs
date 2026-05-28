using System.Collections.Generic;
using UnityEngine;

namespace CriminalMakers.GameEventHub
{
    public class OnlySelf: ISubscriberFilter
    {
        public bool includeChildren = false;
        public int includeParentDepth = 0;

        public OnlySelf()
        {
        }
        
        public OnlySelf(bool includeChildren)
        {
            this.includeChildren = includeChildren;
        }
        
        public OnlySelf(int includeParentDepth)
        {
            this.includeParentDepth = includeParentDepth;
        }
        
        public OnlySelf(bool includeChildren, int includeParentDepth)
        {
            this.includeChildren = includeChildren;
            this.includeParentDepth = includeParentDepth;
        }

        public List<AbstractAttributeBound<OnGameEvent>.BindingInfo> Filter(GameEvent originalEvent, List<AbstractAttributeBound<OnGameEvent>.BindingInfo> bindings)
        {
            var emitterGameObject = SubscriberFilterHelper.ExtractGameObject(originalEvent._emitter);
    
            // Emitter is not part of a GameObject. Only emit to self.
            if (emitterGameObject == null)
            {
                bindings.RemoveAll(bind => originalEvent._emitter != bind.Subscriber);
            }
            else
            {
                bindings.RemoveAll(bind =>
                {
                    // Get the GameObject for the Subscriber
                    var gameObject = SubscriberFilterHelper.ExtractGameObject(bind.Subscriber);

                    if (gameObject == null)
                        return true; // No valid GameObject, remove binding

                    // Retain children if includeChildren is enabled
                    if (includeChildren && IsChildOf(gameObject, emitterGameObject))
                        return false;

                    // Retain parents within the allowed depth
                    if (includeParentDepth > 0 && IsWithinParentDepth(gameObject, emitterGameObject, includeParentDepth))
                        return false;

                    // Default to allowing only the original emitter
                    return gameObject != emitterGameObject;
                });
            }

            return bindings;
        }

        public override string ToString()
        {
            return "Only self";
        }

        private static bool IsChildOf(GameObject child, GameObject parent)
        {
            if (child == null || parent == null)
                return false;

            return child.transform.IsChildOf(parent.transform);
        }
        
        private static bool IsWithinParentDepth(GameObject gameObject, GameObject emitter, int depth)
        {
            if (gameObject == null || emitter == null || depth <= 0)
                return false;

            var current = gameObject.transform.parent;
            int currentDepth = 0;

            while (current != null && currentDepth < depth)
            {
                if (current.gameObject == emitter)
                    return true;

                current = current.parent;
                currentDepth++;
            }

            return false;
        }
    }
}