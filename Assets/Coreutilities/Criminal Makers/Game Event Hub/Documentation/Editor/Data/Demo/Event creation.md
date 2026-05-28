# Event creation

First step is to create an event. An event is a simple class that inherits from `GameEvent` class. 

1. Create a new script named `OnBusArrived`. It should inherit from `GameEvent` class.


```

public class OnBusArrived: GameEvent
{
}

```


> Note: For this demo, we are creating a fieldless event. You can have as many fields as you want in your event class. This is known as Payload