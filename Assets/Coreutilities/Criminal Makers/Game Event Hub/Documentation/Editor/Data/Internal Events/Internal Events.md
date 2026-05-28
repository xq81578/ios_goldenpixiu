# Internal Events

Game Event Hub provides a number of internal events, that are hidden from the user, but are used by the system to notify tools and help to debug during development.

> These events are raised by the GameEventHub component with a filter of OnlyEssentialAndCleanup and are not visible in most of the tools.

- `OnEventSystemStarted`: This event is raised when the component `GameEventHub` reaches the `Start` lifecycle. This event will only be raised if the property `TriggerEventOnStart` is set to `true`.


- `OnObjectBoundToEventSystem`: This event is raised whenever an object is bound to the event system.


- `OnObjectUnboundFromEventSystem`: This event is raised whenever an object is unbound from the event system.


- `OnEventRaised`: This event is raised whenever an event is raised (not including itself).


> Generally you should NOT use these events in your game, as they are used by the system to notify tools and help to debug during development. However, you can use them to create custom tools or debug your game.