# Subscribing to Events

In order to make the passengers board the bus, we need to subscribe to the `OnBusArrived` event. There are two ways to subscribe to an event: **Static** subscription and **Dynamic** subscription.


For this demo, we will use the **Static** subscription.


1. Open the `Passenger` script in the any of the **Passenger Gameobjects**.


2. Add the following code to the `OnEnable` method:

```

GameEventHub.Bind(this)

```


3. Add the following code to the `OnDisable` method:

```

GameEventHub.Unbind(this)

```


4. Create a custom method, named as you like (e.g `BoardBus`) and add an attribute to it:

```csharp

[OnGameEvent]
public void BoardBus(OnBusArrived e)
{
    actionBubbleText.text = "Boarding bus";
    Destroy(gameObject, 1f);
}

```

`OnGameEvent` is a custom attribute for static subscription and works during `GameEventHub.Bind(this)` operation. 


It tells the Game Event Hub that this method should be called when the event is published. The method MUST have **only one parameter**, which is the event type you want to subscribe to.