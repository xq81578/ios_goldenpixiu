using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace CriminalMakers.GameEventHub.Utilities
{
    public class GameEventSO : ScriptableObject
    {
        [SerializeReference, SubclassSelector] public GameEvent GameEvent;

        [FormerlySerializedAs("id")] public string channel;
        public Object simulateEmitter;
        public bool shared;
        public bool nonCancellable;

        [SerializeReference, SubclassSelector]
        public List<ISubscriberFilter> Filters;


        public void Initialize(GameEvent gameEvent)
        {
            if (gameEvent == null)
            {
                return;
            }
            GameEvent = gameEvent.CopyEvent();
        }

        public void FeedEventProperties()
        {
            if (GameEvent == null)
            {
                return;
            }

            GameEvent.SetChannel(channel);

            if (GameEventsHelper.IsObjectUnityNull(simulateEmitter))
            {
                GameEvent.SetEmitter(this);
            }
            else
            {
                GameEvent.SetEmitter(simulateEmitter);
            }

            if (shared)
            {
                GameEvent.Shared();
            }
            else
            {
                GameEvent.Unique();
            }

            if (nonCancellable)
            {
                GameEvent.NonCancellable();
            }
            else
            {
                GameEvent.Cancellable();
            }

            GameEvent.WithFilters(Filters);
        }

        public void Publish(object emitter)
        {
            FeedEventProperties();
            GameEvent.Publish(emitter);
        }
        
        public void Publish()
        {
            FeedEventProperties();
            GameEvent.Publish(this);
        }

        public override string ToString()
        {
            return name;
        }
    }
}