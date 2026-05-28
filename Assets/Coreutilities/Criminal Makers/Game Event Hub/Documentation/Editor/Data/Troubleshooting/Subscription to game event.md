# Subscribed to abstract Game Event

If you attempt to subscribe to the abstract `Game Event` class, you will receive a warning message:

> Method {methodName} on {subscriber} is subscribing to the abstract GameEvent type. This might not be the intended behavior, you should create your own event types that inherit from GameEvent.

Abstract Game Event `is not meant to be used directly`. It's a base class for creating custom event types.

To resolve this issue, create a new class that inherits from `GameEvent` and use it as the event type.

```csharp
public class MyEvent : GameEvent
{
    // Add properties and methods here
}
```