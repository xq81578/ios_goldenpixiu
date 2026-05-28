# Event publish

Now that we have created an event, let's publish it when the bus arrives at the bus stop.


1. Open the `Bus` script in the **Bus Gameobject**.

2. Go to the **OnTriggerEnter2D** method and add the following code:


```

GameEventHub.Publish(this, new OnBusArrived());

```


Alternatively, you can use the **Publish** method directly from the GameEvent base class:

```

new OnBusArrived().Publish(this);

```