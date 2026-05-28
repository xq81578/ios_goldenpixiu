# Subscribers Priority

Subscribers can have a priority that determines the order in which they receive events. You can see the full list of priorities in the script `SubscriberPriority`

## Priority list

- `Essential`: Subscribers with this priority will always receive the event, even if it was stopped.

- `High`: Subscribers with this priority will receive the event after the `Essential` subscribers.

- `Normal`: Subscribers with this priority will receive the event after the `High` subscribers.

- `Low`: Subscribers with this priority will receive the event after the `Normal` subscribers.

- `Cleanup`: Subscribers with this priority will receive the event after the `Low` subscribers. This will receive the event even if it was stopped.