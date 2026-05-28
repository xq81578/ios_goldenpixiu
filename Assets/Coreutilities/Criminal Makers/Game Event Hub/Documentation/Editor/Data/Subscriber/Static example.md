# Static subscription

Here is an example of a static subscriber:

```csharp

public class OnKeyPressSubscriber : MonoBehaviour
{
    private void OnEnable()
    {
        GameEventHub.Bind(this);
    }

    private void OnDisable()
    {
        GameEventHub.Unbind(this);
    }

    [OnGameEvent]
    private void OnKeyPressed(OnKeyPressed e)
    {
        canvasGroup.alpha = 1;
    }
}

```

Some key notes:

- `GameEventHub.Bind(this)` binds all methods with the `[OnGameEvent]` attribute to their respective events.

- `GameEventHub.Unbind(this)` unbinds all methods with the `[OnGameEvent]` attribute from their respective events.

- You can have as many methods with the `[OnGameEvent]` attribute as you want.

- The method `must` have a single parameter that is the event class you want to listen to.

- Alternatively, you can have a non-parameter method and pass the `typeof` the event as a parameter to the `[OnGameEvent]` attribute.

- You can specify the priority of the subscriber by passing it as a parameter to the `[OnGameEvent]` attribute.