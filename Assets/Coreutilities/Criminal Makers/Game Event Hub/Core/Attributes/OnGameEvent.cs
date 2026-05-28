using System;

namespace CriminalMakers.GameEventHub
{
    [AttributeUsage(AttributeTargets.Method)]
    public class OnGameEvent: Attribute
    {
        public Type EventID { get; set; }
        public SubscriberPriority Priority { get; set; } = SubscriberPriority.High;

        /// <summary>
        /// If you use this parameterless constructor, your first parameter must be the event type.
        /// </summary>
        public OnGameEvent()
        {
        }

        /// <summary>
        /// If you use this constructor, your first parameter must be the event type.
        /// </summary>
        /// <param name="priority">The priority of the subscriber.</param>
        public OnGameEvent(SubscriberPriority priority)
        {
            Priority = priority;
        }
        
        public OnGameEvent(Type eventID)
        {
            if (eventID == null || !typeof(GameEvent).IsAssignableFrom(eventID))
            {
                throw new ArgumentException("The type must inherit from GameEvent.", nameof(eventID));
            }
            EventID = eventID;
        }
        
        public OnGameEvent(Type eventID, SubscriberPriority priority)
        {
            if (eventID == null || !typeof(GameEvent).IsAssignableFrom(eventID))
            {
                throw new ArgumentException("The type must inherit from GameEvent.", nameof(eventID));
            }
            EventID = eventID;
            Priority = priority;
        }
    }
}