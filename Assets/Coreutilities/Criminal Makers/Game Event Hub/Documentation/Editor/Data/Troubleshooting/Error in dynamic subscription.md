# Invalid arguments for lambda action

When subscribing to an event dynamically (using `Listen` method), it's expected to pass a lambda action that only accepts one parameter and is the same type as the event expected 

> Invalid arguments for lambda action {lambdaName}. Expected {typeof(TEvent)} as only argument.

To resolve this issue, make sure the lambda action passed to the `Listen` method only accepts one parameter and is the same type as the event expected.

```csharp
GameEventHub.Listen(this, (YourEventType evt) => 
{
    // Handle the event
});
```