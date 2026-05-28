# Difference between static and dynamic subscribers

## Static

A `static` is the one that uses `GameEventHub.Bind()` and `[OnGameEvent]` attribute to subscribe methods to events. This is the most common way to subscribe to events.

All methods are bound and unbound at once, without the need of specifying the event in each case. It is named `static` because the binding should not change as frequently as the `dynamic` one.

## Dynamic

A `dynamic` subscriber is the one that uses `GameEventHub.Listen` to subscribe to a `single` event at a time. `Listen` returns a `unsub` action that can be used to unsubscribe from the event.

Generally the code is more verbose than the `static` one, but it is more flexible.