# Game Event Properties

- `_id`: (Optional) A identifier for the event that developers can use to identify it.


- `_emitter`: (Required) The object that raised the event. Is populated automatically when the event is raised with `Publish(emitter)`. Must be provided if the event is raised with `Publish()` (no args)


- `_shared`: (Optional) A flag that indicates if the event is shared. Shared events shares the payload between all subscribers, so if one subscriber changes the payload, all other subscribers will see the change.


- `_filters`: (Optional) A list of `ISubscribeFilter` used to limit the subscribers that will receive the event.


- `_nonCancellable`: (Optional) A flag that indicates if the event can be cancelled. If set to `true`, the event can't be cancelled.


- `_cancelled`: (Automatically) A flag that indicates if the event was cancelled. This will be filled automatically during event propagation.


- `_sealed`: (Automatically) A flag that indicates if the event metadata is sealed. `Developer doesn't need to worry about this property`, it's used internally by Game Event Hub to prevent subscribers from changing the event metadata.


- `_numberOfInvocations`: (Automatically) The number of times the event was raised. This will be filled automatically during event propagation.


- `_executionTime`: (Automatically) The time it took to propagate the event. This will be filled automatically during event propagation.


- `_timestamp`: (Automatically) The time the event was raised. This will be filled automatically during event propagation.