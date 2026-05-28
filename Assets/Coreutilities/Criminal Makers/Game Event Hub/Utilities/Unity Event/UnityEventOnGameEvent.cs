using System;
using UnityEngine;
using UnityEngine.Events;

namespace CriminalMakers.GameEventHub.Utilities
{
    [AddComponentMenu("Criminal Makers/Game Event Hub/Utilities/Unity Event On Game Event")]
    public class UnityEventOnGameEvent : MonoBehaviour
    {
        [SerializeReference, SubclassSelector(false)]
        private GameEvent gameEvent;

        [SerializeField] private SubscriberPriority priority = SubscriberPriority.Medium;

        [SerializeField] private UnityEvent<GameEvent> onGameEvent;

        private Action unsub;


        private void OnEnable()
        {
            unsub = GameEventHub.Listen(this, gameEvent, priority, e => { onGameEvent.Invoke(e); });
        }

        private void OnDisable()
        {
            unsub();
        }
    }
}