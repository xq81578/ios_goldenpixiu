# Quick Start: Publish the event

Open a `MonoBehaviour` script in your project and emit the event using the `Publish()` method.

```csharp

public class YourEventPublisher : MonoBehaviour
{
    // Update is called once per frame
    void Start()
    {
        new YourEvent().Publish(this);
    }
}
```