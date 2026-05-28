# What is an event?

![Event](./event.png)


An event is essentially a message that is sent from one part of your code to another.

This message can be as `simple as a notification` that something has happened, or it `can contain data` that needs to be processed by the receiver.

In Game Event Hub, an event is represented by a class that inherits from `GameEvent`.


```csharp

public class YourEvent: GameEvent
{

}

```