# Methods during event propagation

- `StopPropagation(object canceller)`: Stops the event propagation, preventing further subscribers from receiving the event. Subscribers with priority `Essential` and `Cleanup` will still receive the event. `NonCancelable` events can't be stopped.


- `fields`: Any `public` field that developer defines in the event class can be used and modified during event propagation. If the event is `shared`, changes to these fields will be reflected in all subscribers. 