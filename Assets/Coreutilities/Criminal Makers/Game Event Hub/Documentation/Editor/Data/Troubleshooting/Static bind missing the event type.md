# Static bind fail: Missing the event type

When performing the `GameEventHub.Bind()` operation, all methods with the `[OnGameEvent]` attribute will be scanned and subscribed.

However, if the method doesn't have any parameters, and the event type is not specified in the attribute, the subscription will fail:

> Method {name} on {subscriber} is missing the event type parameter or the typeof in the attribute. Skipping.

To avoid this error, make sure to specify the event, as the first and only parameter of the method:

```csharp
[OnGameEvent]
public void OnEvent(GameEvent evt) 
{

}
```

Or as part of the attribute:

```csharp
[OnGameEvent(typeof(GameEvent))]
public void OnEvent() 
{

}
```