using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace CriminalMakers.GameEventHub
{
    [DefaultExecutionOrder(-999)]
    [AddComponentMenu("Criminal Makers/Game Event Hub/Game Event Hub")]
    public class GameEventHub : AbstractAttributeBound<OnGameEvent>
    {
        /// <summary>
        /// Determines whether an event should be triggered automatically when the GameEventHub starts.
        /// </summary>
        /// <remarks>
        /// If TriggerEventOnStart is set to true, the GameEventHub will raise the OnEventSystemStarted event
        /// as soon as the Start method is called. This is useful to initialize or notify components that are
        /// dependent on the event system at the beginning of the game lifecycle. If set to false, the event
        /// will not be triggered on start and must be initiated manually if needed.
        /// </remarks>
        public bool TriggerEventOnStart = true;

        /// <summary>
        /// Indicates whether the GameEventHub object should persist across scene loads.
        /// </summary>
        /// <remarks>
        /// If MarkDontDestroyOnLoad is set to true, the GameEventHub instance will not be destroyed when loading a new scene.
        /// This is useful for maintaining a persistent state or listening service that must remain active throughout the
        /// entire game lifecycle. If set to false, the GameEventHub object will be destroyed upon loading a new scene, which
        /// might be desired for resetting states or if the persistent event management is scoped to certain levels of the game.
        /// </remarks>
        public bool MarkDontDestroyOnLoad = true;


        /// <summary>
        /// Ensures that only a single instance of the GameEventHub exists in the game.
        /// </summary>
        /// <remarks>
        /// When ensureSingleInstance is set to true, the system enforces that only one instance of
        /// the GameEventHub component exists in the game. If a duplicate instance is detected,
        /// the extra instances will be automatically removed during runtime so the oldest instance
        /// is preserved.
        /// </remarks>
        public bool EnsureSingleInstance = true;
        
        public int ExecutionMsLimit = 50;

        /// <summary>
        /// Indicates whether the GameEventHub instance is currently initialized.
        /// </summary>
        /// <remarks>
        /// The IsInitialized property returns true if there is an existing GameEventHub instance;
        /// otherwise, it returns false. This property helps determine if the GameEventHub is ready
        /// to handle event publishing and listening functionalities.
        /// </remarks>
        public static bool IsInitialized => Instance != null;

        /// <summary>
        /// Provides read-only access to the registry of game events and their associated bindings within the GameEventHub.
        /// </summary>
        /// <remarks>
        /// GameEventHubRegistry is a static property that returns a ReadOnlyDictionary, mapping event identifiers as strings
        /// to lists of BindingInfo. The dictionary is populated by the GameEventHub instance's BindingRegistry or an empty
        /// dictionary if no instance exists.
        /// </remarks>
        public static ReadOnlyDictionary<string, List<BindingInfo>> GameEventHubRegistry =>
            Instance != null
                ? new ReadOnlyDictionary<string, List<BindingInfo>>(Instance.BindingRegistry)
                : new ReadOnlyDictionary<string, List<BindingInfo>>(new Dictionary<string, List<BindingInfo>>());

        #region Private Fields

        private static GameEventHub _instance;
        private static bool _missingReferenceLogged;
        private DateTime _creationTime;

        public DateTime CreationTime => _creationTime;

        #endregion

        #region Singleton

        private static GameEventHub Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_6000_0_OR_NEWER
                    _instance = FindAnyObjectByType<GameEventHub>();
#else
                    _instance = FindObjectOfType<GameEventHub>();
#endif
                }

                return _instance;
            }
        }

        public static GameEventHub CreateOrRetrieveInstance()
        {
            if (Instance != null)
            {
                return _instance;
            }

            var go = new GameObject("Game Event Hub");
            _instance = go.AddComponent<GameEventHub>();
            return _instance;
        }

        private static void ValidateInstance()
        {
            if (Instance != null || _missingReferenceLogged) return;

            Debug.LogError("Game Event Hub is not in the scene. Events will not be handled.");
            _missingReferenceLogged = true;
        }

        /// <summary>
        /// Detaches the GameObject from its current parent and marks it to not be destroyed on scene load.
        /// </summary>
        private void DetachAndPreserve()
        {
            gameObject.transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Ensures there is only one instance of the GameEventHub in the scene by consolidating all existing instances.
        /// Retains the oldest instance based on its creation time and destroys all other instances.
        /// </summary>
        private static void ConsolidateGameEventHubs()
        {
            GameEventHub[] gameEventHubs;
#if UNITY_6000_0_OR_NEWER
            gameEventHubs = FindObjectsByType<GameEventHub>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            gameEventHubs = FindObjectsOfType<GameEventHub>();
#endif

            if (gameEventHubs.Length == 1) return;

            var oldestInstance = gameEventHubs.OrderBy(geh => geh._creationTime).First();

            foreach (var geh in gameEventHubs)
            {
                if (geh == oldestInstance) continue;
                Destroy(geh.gameObject);
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _creationTime = DateTime.Now;
        }

        private void Start()
        {
            if (EnsureSingleInstance)
            {
                ConsolidateGameEventHubs();
            }

            if (TriggerEventOnStart)
            {
                _RaiseEvent(this, new OnEventSystemStarted());
            }

            if (MarkDontDestroyOnLoad)
            {
                DetachAndPreserve();
            }

            // Check time of creation. If > 1 frame, log warning.
            if (Time.frameCount > 1)
            {
                Debug.LogWarning(
                    "Game Event Hub was created after the first frame. Expect some subscriptions to be missed.");
            }
        }

        #endregion

        #region API

        /// <summary>
        /// Publishes a game event to be handled by any registered subscribers.
        /// </summary>
        /// <typeparam name="TEvent">The type of the game event being published.</typeparam>
        /// <param name="emitter">The object emitting or initiating the event. (Generally "this")</param>
        /// <param name="gameEvent">The game event instance to be published.</param>
        public static void Publish<TEvent>(object emitter, TEvent gameEvent) where TEvent : GameEvent
        {
            ValidateInstance();
            Instance?._RaiseEvent(emitter, gameEvent);
        }

        /// <summary>
        /// Publishes a game event after a specified delay to be handled by any registered subscribers.
        /// </summary>
        /// <typeparam name="TEvent">The type of the game event being published.</typeparam>
        /// <param name="emitter">The object emitting or initiating the event. (Generally "this").</param>
        /// <param name="gameEvent">The game event instance to be published.</param>
        /// <param name="delayInSeconds">The duration of the delay, in seconds, after which the event will be published.</param>
        public static void PublishDelayed<TEvent>(object emitter, TEvent gameEvent, float delayInSeconds)
            where TEvent : GameEvent
        {
            ValidateInstance();
            Instance.StartCoroutine(Instance._RaiseEventDelayed(emitter, gameEvent, delayInSeconds));
        }

        /// <summary>
        /// Allows a subscriber to listen for specific game events and execute an associated action when the event occurs.
        /// 
        /// This method is ideal for dynamic subscribers needing the flexibility to subscribe and unsubscribe at any time.
        /// </summary>
        /// <typeparam name="TEvent">The type of game event to listen for.</typeparam>
        /// <param name="subscriber">The object that will respond to the game event.</param>
        /// <param name="action">The action to execute when the specified event occurs.</param>
        /// <returns>An action delegate that can be invoked to unsubscribe the subscriber from the event.</returns>
        public static Action Listen<TEvent>(object subscriber, Action<TEvent> action) where TEvent : GameEvent
        {
            ValidateInstance();
            return Instance?._Listen(subscriber, action, SubscriberPriority.Medium);
        }

        /// <summary>
        /// Registers a subscriber for a specific type of game event.
        /// </summary>
        /// <typeparam name="TEvent">The type of the game event the subscriber is interested in.</typeparam>
        /// <param name="subscriber">The object that will listen for the event.</param>
        /// <param name="action">The action to execute when the event is triggered.</param>
        /// <param name="priority">The priority level of the subscriber. (If you want to use Medium, use the other overload)</param>
        /// <returns>An action delegate that can be used to unregister the subscriber.</returns>
        public static Action Listen<TEvent>(object subscriber, Action<TEvent> action, SubscriberPriority priority)
            where TEvent : GameEvent
        {
            ValidateInstance();
            return Instance?._Listen(subscriber, action, priority);
        }

        /// <summary>
        /// Registers a subscriber to listen for a specific game event with a defined priority and handler action.
        ///
        /// This method is unadvised, as it is not type-safe and relies heavily on reflection. Are you sure you need subscription to be this dynamic?
        ///
        /// Suggestion: Instead, use Listen and pass the event type as the parameter of the action.
        /// </summary>
        /// <param name="subscriber">The object that subscribes to the game event, typically the calling class instance.</param>
        /// <param name="gameEvent">The type of game event to subscribe to.</param>
        /// <param name="priority">The priority level of the subscriber, determining the order of event handling.</param>
        /// <param name="action">The action to execute when the subscribed game event occurs.</param>
        /// <returns>A delegate that can be used to unsubscribe the listener from the event.</returns>
        public static Action Listen(object subscriber, GameEvent gameEvent, SubscriberPriority priority,
            Action<GameEvent> action)
        {
            ValidateInstance();
            return Instance?._ListenGeneric(subscriber, gameEvent, action, priority);
        }

        /// <summary>
        /// Registers a subscriber to listen for a specific game event with the specified action to execute when the event is triggered.
        ///
        /// This method is unadvised, as it is not type-safe and relies heavily on reflection. Are you sure you need subscription to be this dynamic?
        ///
        /// Suggestion: Instead, use Listen and pass the event type as the parameter of the action.
        /// </summary>
        /// <param name="subscriber">The object subscribing to the game event.</param>
        /// <param name="gameEvent">The game event to subscribe to.</param>
        /// <param name="action">The action to invoke when the subscribed game event is triggered.</param>
        /// <returns>An Action instance that can be invoked to unsubscribe from the game event.</returns>
        public static Action Listen(object subscriber, GameEvent gameEvent, Action<GameEvent> action)
        {
            ValidateInstance();
            return Instance?._ListenGeneric(subscriber, gameEvent, action, SubscriberPriority.Medium);
        }

        /// <summary>
        /// Binds a subscriber object to the game event system.
        ///
        /// Methods decorated with the [OnGameEvent] attribute will be automatically bound to the event system.
        ///
        /// Methods should have a single parameter of the same type as the event they are listening for.
        /// Or none if the subscriber is not interested in the event data.
        /// </summary>
        /// <param name="subscriber">The object to be bound to the event system.</param>
        public static void Bind(object subscriber)
        {
            ValidateInstance();
            Instance?._Bind(subscriber);
        }

        /// <summary>
        /// Unbinds a subscriber from the event system, removing any registered events associated with it.
        /// </summary>
        /// <param name="subscriber">The object to be unbound from listening to events.</param>
        public static void Unbind(object subscriber)
        {
            Instance?._Unbind(subscriber);
        }

        #endregion

        #region Event Handling

        /// <summary>
        /// Raises a game event after a specified delay to be handled by any registered subscribers.
        /// </summary>
        /// <typeparam name="TEvent">The type of the game event being raised.</typeparam>
        /// <param name="emitter">The object emitting or initiating the event.</param>
        /// <param name="gameEvent">The game event instance to be raised.</param>
        /// <param name="delayInSeconds">The delay in seconds before the event is raised.</param>
        /// <returns>An IEnumerator used to perform the delay in a coroutine.</returns>
        private IEnumerator _RaiseEventDelayed<TEvent>(object emitter, TEvent gameEvent, float delayInSeconds)
            where TEvent : GameEvent
        {
            yield return new WaitForSeconds(delayInSeconds);
            _RaiseEvent(emitter, gameEvent);
        }

        /// <summary>
        /// Raises a game event by notifying all subscribers associated with the given event type.
        /// </summary>
        /// <typeparam name="TEvent">The type of the game event being raised.</typeparam>
        /// <param name="emitter">The object that emits or initiates the event. (Typically "this")</param>
        /// <param name="ogGameEvent">The instance of the game event to be raised.</param>
        /// <exception cref="InvalidOperationException">Thrown if the event is sealed and cannot be raised.</exception>
        private void _RaiseEvent<TEvent>(object emitter, TEvent ogGameEvent) where TEvent : GameEvent
        {
            if (GameEventsHelper.IsObjectUnityNull(emitter))
            {
                Debug.LogError("Emitter cannot be null. Please provide a valid object reference.");
                return;
            }

            GameEvent gameEvent = ogGameEvent.CopyEvent();
            if (string.IsNullOrEmpty(gameEvent._channel))
            {
                // get [DefaultChannel] attribute at class level
                var defaultChannelAttribute =
                    (DefaultChannel)Attribute.GetCustomAttribute(gameEvent.GetType(), typeof(DefaultChannel));

                if (defaultChannelAttribute != null)
                {
                    gameEvent.SetChannel(defaultChannelAttribute.Channel);
                }
            }

            gameEvent.SetEmitter(emitter);
            gameEvent.RegisterTimestamp();
            gameEvent.Seal();
            _CleanBindingRegistry(gameEvent);

            List<BindingInfo> subscribers = new List<BindingInfo>();
            if (BindingRegistry.ContainsKey(GameEventsHelper.GetEventBindingKey(gameEvent)))
            {
                subscribers = BindingRegistry[GameEventsHelper.GetEventBindingKey(gameEvent)];
            }

            List<object> subscribersCalled = new List<object>();

            // Start performance tracking
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Restart();

            // Always execute essential subscribers at the beginning
            PropagationResult essentialResult =
                _PropagatePhase(gameEvent, subscribers, SubscriberPriority.Essential, false);
            subscribersCalled.AddRange(essentialResult.SubscribersInvoked);

            // Only propagate to regular subscribers if the event is not cancelled
            if (!essentialResult.PropagationStopped)
            {
                List<BindingInfo> filteredSubscribers = _ApplyEventFilters(gameEvent, subscribers);
                // Define the list of propagation phases in order
                var priorityPhaseOrder = new[]
                {
                    SubscriberPriority.High,
                    SubscriberPriority.Medium,
                    SubscriberPriority.Low
                };

                // Propagate through each phase in order
                foreach (var priority in priorityPhaseOrder)
                {
                    PropagationResult result = _PropagatePhase(gameEvent, filteredSubscribers, priority, true);
                    subscribersCalled.AddRange(result.SubscribersInvoked);

                    if (result.PropagationStopped) // Stop propagation if cancelled during any phase
                    {
                        break;
                    }
                }
            }

            // Always execute cleanup subscribers at the end
            PropagationResult cleanupResult =
                _PropagatePhase(gameEvent, subscribers, SubscriberPriority.Cleanup, false);
            subscribersCalled.AddRange(cleanupResult.SubscribersInvoked);

            // Stop performance tracking
            stopwatch.Stop();
            gameEvent.SetExecutionTime((float)stopwatch.Elapsed.TotalMilliseconds);
            gameEvent.SetNumberOfInvocations(subscribersCalled.Count);
            _CheckExecutionTime(gameEvent);

#if UNITY_EDITOR
            if (!ReferenceEquals(emitter, this))
            {
                _NotifyTools(subscribersCalled, gameEvent);
            }
#endif
        }

        /// <summary>
        /// Propagates a game event to a specific phase of subscribers,
        /// determined by the provided subscriber priority.
        /// </summary>
        /// <typeparam name="TEvent">The type of the game event being propagated.</typeparam>
        /// <param name="gameEvent">The game event instance to be propagated.</param>
        /// <param name="subscribers">A list of subscribers to be notified of the event.</param>
        /// <param name="priority">The priority of subscribers to notify during this phase.</param>
        /// <param name="breakIfCancelled">If true, stops further propagation if the event is cancelled.</param>
        /// <returns>A PropagationResult detailing which subscribers were invoked and whether propagation stopped.</returns>
        private PropagationResult _PropagatePhase<TEvent>(
            TEvent gameEvent,
            List<BindingInfo> subscribers,
            SubscriberPriority priority,
            bool breakIfCancelled
        ) where TEvent : GameEvent
        {
            PropagationResult propagationResult = new PropagationResult();

            // Filter the subscribers if a priority is specified
            List<BindingInfo> filteredSubscribers =
                subscribers.Where(sub => GameEventsHelper.GetBindingPriority(sub) == priority).ToList();

            // No subscribers for this priority
            if (filteredSubscribers.Count == 0) return propagationResult;

            // Propagate to the filtered subscribers
            propagationResult = _Propagate(gameEvent, filteredSubscribers, breakIfCancelled);

            // If propagation was stopped, reflect that in the game event
            if (breakIfCancelled && propagationResult.PropagationStopped)
            {
                var canceller = propagationResult.SubscribersInvoked.Last();
                gameEvent.StopPropagation(canceller);
            }

            return propagationResult;
        }

        /// <summary>
        /// Processes a game event by invoking all attached subscribers, with option to halt propagation if the event is cancelled.
        /// </summary>
        /// <typeparam name="TEvent">The type of the game event being processed.</typeparam>
        /// <param name="gameEvent">The instance of the game event to be propagated to subscribers.</param>
        /// <param name="subscribers">A list of BindingInfo objects, each representing a subscriber to be invoked.</param>
        /// <param name="breakIfCancelled">If set to true, stops further propagation if the event is cancelled by any subscriber.</param>
        /// <returns>A PropagationResult containing details about the propagation status and invoked subscribers.</returns>
        private PropagationResult _Propagate<TEvent>(TEvent gameEvent, List<BindingInfo> subscribers,
            bool breakIfCancelled) where TEvent : GameEvent
        {
            var propagationResult = new PropagationResult();

            foreach (var sub in subscribers)
            {
                try
                {
                    // Clone the event unless it is shared
                    var gameEventUnique = (gameEvent._shared ? gameEvent : gameEvent.Clone()) as GameEvent;

                    if (gameEventUnique == null)
                    {
                        Debug.LogError(
                            $"Error invoking event {GameEventsHelper.GetEventName<TEvent>()} on {sub.Subscriber}: Could not clone event.");
                        continue;
                    }

                    propagationResult.SubscribersInvoked.Add(sub.Subscriber);
                    sub.Invoke(new object[] { gameEventUnique });

                    if (!gameEventUnique._cancelled) continue;

                    propagationResult.PropagationStopped = true;

                    if (breakIfCancelled)
                    {
                        break;
                    }
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                    {
                        Debug.LogError($"Error in subscriber {sub.Subscriber}: {ex.InnerException.StackTrace}",
                            (Object)sub.Subscriber);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"Error invoking event {GameEventsHelper.GetEventName<TEvent>()} on {sub.Subscriber}: {e.StackTrace}",
                        (Object)sub.Subscriber);
                }
            }

            return propagationResult;
        }

        /// <summary>
        /// Checks the execution time of the given game event and logs a warning if it exceeds a certain threshold.
        /// </summary>
        /// <param name="gameEvent">The game event whose execution time is being monitored.</param>
        private void _CheckExecutionTime(GameEvent gameEvent)
        {
            if (gameEvent._executionTime > ExecutionMsLimit)
            {
                Debug.LogWarning(
                    $"Event {GameEventsHelper.GetEventName(gameEvent.GetType())} took {gameEvent._executionTime}ms ({gameEvent._numberOfInvocations} invokes) to execute. This an excessive execution time for an event, are you performing heavy operations or invoking too many subscribers?");
            }
        }

        /// <summary>
        /// Filters a list of bindings to exclude those marked as essential or cleanup subscribers
        /// and applies any additional filters defined in the game event.
        /// </summary>
        /// <param name="gameEvent">The game event that contains potential filters for the bindings.</param>
        /// <param name="bindings">The list of initial bindings to be filtered.</param>
        /// <returns>A filtered list of bindings containing only regular subscribers.</returns>
        private List<BindingInfo> _ApplyEventFilters(GameEvent gameEvent, List<BindingInfo> bindings)
        {
            List<BindingInfo> bindingCopy = new List<BindingInfo>(bindings);
            if (gameEvent._filters == null)
            {
                return bindingCopy;
            }

            foreach (var filterInEvent in gameEvent._filters)
            {
                if (filterInEvent == null) continue;
                try
                {
                    bindingCopy = filterInEvent.Filter(gameEvent, bindingCopy);
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"Error in filter {filterInEvent.GetType().Name}: {e.Message} (Game Event: {gameEvent}. Skipping filter.");
                }
            }

            return bindingCopy;
        }

        /// <summary>
        /// Cleans up the binding registry by removing bindings associated with destroyed subscribers for the specified game event.
        /// </summary>
        /// <param name="gameEvent">The game event whose associated bindings need to be cleaned.</param>
        private void _CleanBindingRegistry(GameEvent gameEvent)
        {
            var eventID = GameEventsHelper.GetEventBindingKey(gameEvent);

            if (!BindingRegistry.TryGetValue(eventID, out var bindings))
            {
                return;
            }

            bindings.RemoveAll(bindingInfo =>
            {
                if (!GameEventsHelper.IsObjectUnityNull(bindingInfo.Subscriber)) return false;

                var eventType = Type.GetType(eventID);

                Debug.LogWarning(
                    $"A null BindingInfo for event {GameEventsHelper.GetEventName(eventType)} has been removed. Please ensure that the subscriber uses Unbind or calls the method returned by Listen to unsubscribe from the event before being destroyed.");
                return true;
            });

            if (!bindings.Any())
            {
                BindingRegistry.Remove(eventID);
            }
        }

        #endregion

        #region Tools

        /// <summary>
        /// Notifies tools of the event raised and the subscribers involved in the event.
        /// </summary>
        /// <typeparam name="TEvent">The type of the game event being notified.</typeparam>
        /// <param name="subscribers">A list of subscribers that are involved in the event.</param>
        /// <param name="gameEvent">The game event instance that has been raised.</param>
        private void _NotifyTools<TEvent>(List<object> subscribers, TEvent gameEvent)
            where TEvent : GameEvent
        {
            var emitter = gameEvent._emitter;
            var emitterActor = new EventActorData(emitter.GetType().Name, emitter);
            var subscribersActors = new List<EventActorData>();
            subscribersActors.AddRange(subscribers.ConvertAll(b => new EventActorData(b.ToString(), b)));

            new OnEventRaised(emitterActor, gameEvent, subscribersActors)
                .WithFilter(new OnlyEssentialAndCleanup())
                .NonCancellable()
                .Publish(this);
        }

        #endregion

        #region Dynamic subscriber binding

        private Action _Listen<TEvent>(object subscriber, Action<TEvent> action, SubscriberPriority priority)
            where TEvent : GameEvent
        {
            var bindingInfo = new DynamicSubscriberBindingInfo<TEvent>(action, priority, subscriber);

            AddBindingInfo(bindingInfo, subscriber);

#if UNITY_EDITOR
            new OnObjectBoundToEventSystem(subscriber, false)
                .WithFilter(new OnlyEssentialAndCleanup())
                .NonCancellable()
                .Publish(this);
#endif

            return () => _Unsubscribe(bindingInfo);
        }

        private Action _ListenGeneric(object subscriber, GameEvent gameEvent, Action<GameEvent> action,
            SubscriberPriority priority)
        {
            if (gameEvent == null)
                throw new ArgumentNullException(nameof(gameEvent), "Cannot Listen to a GameEvent null.");
            if (action == null)
                throw new ArgumentNullException(nameof(action), "Cannot Listen with an action null.");

            var eventType = gameEvent.GetType();
            if (!typeof(GameEvent).IsAssignableFrom(eventType))
                throw new ArgumentException($"Type {eventType.Name} is not a valid GameEvent.", nameof(gameEvent));

            try
            {
                var wrapperType = typeof(Action<>).MakeGenericType(eventType);
                var dynamicMethod = Delegate.CreateDelegate(
                    wrapperType,
                    action.Target,
                    action.Method
                );

                var listenMethod =
                    typeof(GameEventHub).GetMethod("_Listen", BindingFlags.NonPublic | BindingFlags.Instance);
                if (listenMethod == null)
                    throw new InvalidOperationException("Internal method '_Listen' not found in GameEventHub.");

                var genericListenMethod = listenMethod.MakeGenericMethod(eventType);

                if (Instance == null)
                    throw new InvalidOperationException(
                        "GameEventHub instance is null. Ensure it is initialized before calling Listen.");

                return (Action)genericListenMethod.Invoke(Instance,
                    new object[] { subscriber, dynamicMethod, priority });
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException("Failed to dynamically invoke '_Listen'.", ex.InnerException ?? ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"An error occurred while setting up the listener: {ex.Message}",
                    ex);
            }
        }

        private void _Unsubscribe(BindingInfo bindingInfo)
        {
            RemoveBindingInfo(bindingInfo);

#if UNITY_EDITOR
            new OnObjectUnboundFromEventSystem(bindingInfo.Subscriber, false)
                .WithFilter(new OnlyEssentialAndCleanup())
                .NonCancellable()
                .Publish(this);
#endif
        }

        public class DynamicSubscriberBindingInfo<TEvent> : BindingInfo where TEvent : GameEvent
        {
            private Action<TEvent> LambdaAction { get; }
            public SubscriberPriority Priority { get; }

            public DynamicSubscriberBindingInfo(Action<TEvent> lambdaAction, SubscriberPriority priority,
                object subscriber)
                : base(null, subscriber)
            {
                BindingKey = GameEventsHelper.GetEventBindingKey<TEvent>();
                Priority = priority;
                LambdaAction = lambdaAction;
            }

            public override void Invoke(object[] args = null)
            {
                if (args is { Length: 1 } && args[0] is TEvent eventArg)
                {
                    LambdaAction(eventArg);
                }
                else
                {
                    Debug.LogError(
                        $"Invalid arguments for lambda action {LambdaAction.Method.Name}. Expected {typeof(TEvent)} as only argument.");
                }
            }
        }

        #endregion

        #region Static subscriber binding

        protected override void _Bind(object subscriber)
        {
            base._Bind(subscriber);
            new OnObjectBoundToEventSystem(subscriber, true)
                .WithFilter(new OnlyEssentialAndCleanup())
                .NonCancellable()
                .Publish(this);
        }

        protected override bool _Unbind(object subscriber)
        {
            bool wasUnbound = base._Unbind(subscriber);

            if (wasUnbound)
            {
                new OnObjectUnboundFromEventSystem(subscriber, true)
                    .WithFilter(new OnlyEssentialAndCleanup())
                    .NonCancellable()
                    .Publish(this);
            }

            return wasUnbound;
        }

        protected override BindingInfo CreateBindingInfo(OnGameEvent attribute, MethodInfo methodInfo,
            object subscriber)
        {
            return new StaticSubscriberBindingInfo(methodInfo, attribute, subscriber);
        }

        public class StaticSubscriberBindingInfo : BindingInfo
        {
            public SubscriberPriority Priority { get; }

            public StaticSubscriberBindingInfo(MemberInfo memberInfo, OnGameEvent attribute, object subscriber)
                : base(memberInfo, subscriber)
            {
                if (memberInfo is not MethodInfo methodInfo) return;

                Priority = attribute.Priority;

                if (attribute.EventID == null)
                {
                    // get the first parameter of the method
                    var parameters = methodInfo.GetParameters();
                    if (parameters.Length == 0)
                    {
                        Debug.LogError(
                            $"Method {methodInfo.Name} on {subscriber.GetType().Name} is missing the event type parameter or the typeof in the attribute. Skipping.");
                        return;
                    }

                    if (!typeof(GameEvent).IsAssignableFrom(parameters[0].ParameterType))
                    {
                        Debug.LogError(
                            $"Method {methodInfo.Name} on {subscriber.GetType().Name} has an invalid parameter type. Skipping.");
                        return;
                    }

                    BindingKey = GameEventsHelper.GetEventBindingKey(parameters[0].ParameterType);
                }
                else
                {
                    BindingKey = GameEventsHelper.GetEventBindingKey(attribute.EventID);
                }

                // Warn the user about potentially wrong subscription to abstract type
                if (BindingKey == GameEventsHelper.GetEventBindingKey(typeof(GameEvent)))
                {
                    Debug.LogWarning(
                        $"Method {methodInfo.Name} on {subscriber.GetType().Name} is subscribing to the abstract GameEvent type. This might not be the intended behavior, you should create your own event types that inherit from GameEvent.");
                }
            }

            public override void Invoke(object[] args = null)
            {
                var method = MemberInfo as MethodInfo;
                if (method == null || Subscriber == null) return;

                var parameters = method.GetParameters();
                method.Invoke(Subscriber, parameters.Length == 0 ? null : args);
            }
        }

        #endregion
    }
}