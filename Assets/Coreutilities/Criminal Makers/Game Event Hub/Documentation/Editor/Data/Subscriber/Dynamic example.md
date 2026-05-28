# Dynamic subscription

Here is an example of a dynamic subscriber:

```csharp

public class OnKeyPressSubscriber : MonoBehaviour
{
    private void OnEnable()
    {
        Action unsub = GameEventHub.Listen(this, (OnKeyPressed e) =>
        {
            canvasGroup.alpha = 1;

            unsub();
        });
    }
}

```

Some key notes:

- `GameEventHub.Listen(this, (OnKeyPressed e) => { ... })` binds the lambda to the `OnKeyPressed` event.

- The lambda `must` have a single parameter that is the event class you want to listen to.

- The lambda returns an `Action` that can be used to unsubscribe from the event.