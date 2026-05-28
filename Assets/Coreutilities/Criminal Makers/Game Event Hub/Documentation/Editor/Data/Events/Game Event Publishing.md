# Methods during event publishing

- `SetId(string id)`: Sets the event identifier and returns the event itself.


- `SetEmitter(object emitter)`: Sets the event emitter and returns the event itself.


- `Cancellable()`: Marks the event as cancellable and returns the event itself.


- `NonCancellable()`: Marks the event as non-cancellable and returns the event itself.


- `Shared()`: Marks the event as `_shared = true` and returns the event itself.


- `Unique()`: Marks the event as `_shared = false` and returns the event itself.


- `WithFilter()`: Adds a filter to the event and returns the event itself. You can chain multiple filters.


- `Publish()`: Raises the event. This method is used to raise the event with no arguments. The `emitter` must be provided.


- `Publish(emitter)`: Raises the event. This method is used to raise the event with an emitter. The `emitter` must be provided.


- `PublishDelayed(emitter, delay)`: Raises the event after a delay.


- `CopyEvent()`: Creates an `unsealed` copy of the event with metadata cleared.