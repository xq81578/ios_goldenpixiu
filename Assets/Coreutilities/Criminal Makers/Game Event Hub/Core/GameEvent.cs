using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CriminalMakers.GameEventHub
{
    [Serializable, DefaultChannel("Default")]
    public abstract class GameEvent : ICloneable
    {
        [Obsolete("Please read _channel instead")]
        public string _id { get; private set; }
        public string _channel { get; private set; }
        
        public object _emitter { get; private set; }
        public bool _shared { get; private set; }
        public List<ISubscriberFilter> _filters { get; private set; }
        public bool _nonCancellable { get; private set; }
        public object _cancelledBy { get; private set; }
        public bool _cancelled { get; private set; }
        public bool _sealed { get; private set; }
        public int _numberOfInvocations { get; private set; }
        public float _executionTime { get; private set; }
        public DateTime _timestamp { get; private set; }


        /// <summary>
        /// Sets the ID of the game event to the specified value. This method can only be performed on an unsealed event.
        ///
        /// You can set a custom ID for the event to help you debug, or it will be generated automatically when the event is published. 
        /// </summary>
        /// <returns>The current instance of the game event with the newly generated ID.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the event is sealed
        /// </exception>
        [Obsolete("SetId is deprecated. Please use SetChannel instead")]
        public virtual GameEvent SetId(string id)
        {
            if (_sealed)
            {
                throw new InvalidOperationException("ID cannot be changed on a sealed event.");
            }

            Debug.LogWarning("SetId is deprecated. Please use SetChannel instead");
            _id = id;
            _channel = id;

            return this;
        }
        
        public virtual GameEvent SetChannel(string channel)
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Channel cannot be changed on a sealed event.");
            }

            _channel = channel;
#pragma warning disable CS0618 // Type or member is obsolete
            _id = channel;
#pragma warning restore CS0618 // Type or member is obsolete
            return this;
        }

        /// <summary>
        /// Sets the emitter of the game event to the specified object.
        /// This operation cannot be performed if the event is already sealed.
        /// </summary>
        /// <param name="emitter">The object to be set as the emitter of the game event.</param>
        /// <returns>The current instance of the game event with the updated emitter.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to set the emitter on a sealed event.</exception>
        public GameEvent SetEmitter(object emitter)
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Cannot change properties on a sealed event.");
            }

            _emitter = emitter;
            return this;
        }

        public GameEvent RegisterTimestamp()
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Cannot change properties on a sealed event.");
            }

            _timestamp = DateTime.Now;
            return this;
        }

        public GameEvent Cancellable()
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Cannot change properties on a sealed event.");
            }

            _nonCancellable = false;
            return this;
        }

        /// <summary>
        /// Marks the game event as incancellable, ensuring that it cannot be stopped once triggered.
        /// </summary>
        public GameEvent NonCancellable()
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Cannot change properties on a sealed event.");
            }

            _nonCancellable = true;
            return this;
        }

        /// <summary>
        /// Attempts to halt the propagation of the game event.
        /// </summary>
        /// <param name="canceller">
        /// The object or entity attempting to cancel the event. This can provide context to why or
        /// by whom the event was cancelled.
        /// </param>
        /// <remarks>
        /// If the event is marked as non-cancellable, a warning will be logged and propagation will continue.
        /// </remarks>
        public void StopPropagation(object canceller)
        {
            if (_nonCancellable)
            {
                Debug.LogWarning(
                    $"{canceller.ToString()} attempted to cancel a non-cancellable event. Propagation will continue.",
                    canceller as Object);
                return;
            }

            _cancelled = true;
            _cancelledBy = canceller;
        }

        /// <summary>
        /// Shared events will hold a single instance of the event, if any subscriber changes the event data subsequent subscribers will receive the updated data.
        /// </summary>
        /// <returns>The current instance of the game event marked as shared.</returns>
        public GameEvent Shared()
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Cannot change properties on a sealed event.");
            }

            _shared = true;
            return this;
        }

        /// <summary>
        /// (Default) Marks the game event as unique, ensuring its data is not shared across different contexts.
        /// </summary>
        /// <returns>The current instance of the game event configured as unique.</returns>
        public GameEvent Unique()
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Cannot change properties on a sealed event.");
            }

            _shared = false;
            return this;
        }

        public GameEvent WithFilter(ISubscriberFilter filter)
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Cannot change properties on a sealed event.");
            }

            _filters ??= new List<ISubscriberFilter>();

            _filters.Add(filter);
            return this;
        }

        public GameEvent WithFilters(List<ISubscriberFilter> filters)
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Cannot change properties on a sealed event.");
            }

            _filters = filters;
            return this;
        }

        /// <summary>
        /// Sets the number invocations this event produced.
        /// This operation will be automatically performed when the event is published.
        /// </summary>
        /// <param name="numberOfInvocations">The number of invocations caused by the event.</param>
        /// <returns>The current instance of the game event with the updated number of invocations.</returns>
        public GameEvent SetNumberOfInvocations(int numberOfInvocations)
        {
            _numberOfInvocations = numberOfInvocations;
            return this;
        }

        /// <summary>
        /// Sets the execution time for the game event.
        /// This operation will be automatically performed when the event is published.
        /// </summary>
        /// <param name="time">The execution time in seconds to be set for the game event.</param>
        /// <returns>The current instance of the game event with the updated execution time.</returns>
        public GameEvent SetExecutionTime(float time)
        {
            _executionTime = time;
            return this;
        }

        /// <summary>
        /// Seals the game event, preventing any further modifications to its configuration.
        ///
        /// You should not seal an event, it will be sealed automatically when published.
        /// </summary>
        /// <returns>The current instance of the game event marked as sealed.</returns>
        public GameEvent Seal()
        {
            _sealed = true;
            return this;
        }


        /// <summary>
        /// Publishes the current game event to the GameEventHub for handling by any registered subscribers.
        /// This method requires that the event's emitter has been set prior to calling.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the emitter is null when attempting to publish the event.
        /// </exception>
        public void Publish()
        {
            if (_emitter == null)
                throw new InvalidOperationException("Emitter cannot be null.");

            GameEventHub.Publish(_emitter, this);
        }

        /// <summary>
        /// Publishes the current game event to the GameEventHub for handling by registered subscribers.
        /// </summary>
        /// <param name="emitter">The object responsible for publishing the event, typically the instance originating the event.</param>
        public void Publish(object emitter)
        {
            GameEventHub.Publish(emitter, this);
        }

        /// <summary>
        /// Publishes the current game event after a specified delay. This method ensures the event will be handled by any registered subscribers once the delay elapses.
        /// </summary>
        /// <param name="emitter">The object emitting or initiating the event. Typically, this is the object associated with the event's context.</param>
        /// <param name="delay">The duration of the delay, specified in seconds, after which the event will be published.</param>
        public void PublishDelayed(object emitter, float delay)
        {
            GameEventHub.PublishDelayed(emitter, this, delay);
        }

        /// <summary>
        /// Publishes the game event after a specified delay. The event will be handled by any subscribed listeners after the delay.
        /// </summary>
        /// <param name="delay">The duration of the delay, in seconds, before the event is published.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the emitter is null when attempting to publish the event.
        /// </exception>
        public void PublishDelayed(float delay)
        {
            if (_emitter == null)
                throw new InvalidOperationException("Emitter cannot be null.");

            GameEventHub.PublishDelayed(_emitter, this, delay);
        }

        /// <summary>
        /// Creates a copy of the current GameEvent instance for reuse.
        /// This method creates a shallow copy of the event, resetting its sealed and metadata state
        /// to allow further modifications to the cloned instance.
        /// </summary>
        /// <returns>A new GameEvent instance that is a shallow copy of the current event with the sealed state reset.</returns>
        public GameEvent CopyEvent()
        {
            var copy = (GameEvent)Clone();
            copy._timestamp = DateTime.Now;
            copy._cancelled = false;
            copy._cancelledBy = null;
            copy._numberOfInvocations = 0;
            copy._executionTime = 0;
            copy._sealed = false;

            return copy;
        }

        /// <summary>
        /// Creates a shallow copy of the current GameEvent instance.
        /// This method allows duplication of the event's state at the time of invocation.
        ///
        /// It will be used by GameEventHub if the event is flagged as Unique.
        /// </summary>
        /// <returns>A shallow copy of the current GameEvent instance.</returns>
        public virtual object Clone()
        {
            var clonedEvent = (GameEvent)MemberwiseClone();
            clonedEvent._filters = new List<ISubscriberFilter>();
            if (_filters != null)
            {
                clonedEvent._filters.AddRange(_filters);
            }

            return clonedEvent;
        }
    }
}