# Quick Start: Subscribe to the event

Open a different `MonoBehaviour` script in your project and static subscribe to it

```csharp

public class YourEventSubscriber : MonoBehaviour
{
    void OnEnable()
    {
        GameEventHub.Bind(this);
    }

    void OnDisable()
    {
        GameEventHub.Unbind(this);
    }

    [OnGameEvent]
    public void YourEventHandler(YourEvent yourEvent)
    {
        // Do something
    }
}
```