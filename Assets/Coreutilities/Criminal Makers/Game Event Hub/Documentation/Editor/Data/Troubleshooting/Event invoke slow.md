# Event took too long to invoke

When an event is raised, Game Event Hub will start a watch to measure how long it takes to invoke the event. If the event takes longer than `50ms`, thus affecting the performance of the game, Game Event Hub will log a warning message:

> Event {type} took {time}ms ({numberOfInvocations} invokes) to execute. This an excessive execution time for an event, are you performing heavy operations or invoking too many subscribers?

As a rule of thumb, events should be lightweight and fast to execute. If an event is taking too long to execute, consider the following:

- **Optimize the event**: Check the event and the subscribers to see if there are any heavy operations that can be optimized or sent to a background thread.

- **Reduce the number of subscribers**: If the event has too many subscribers, consider reducing the number of subscribers or splitting the event into multiple events.

- **Use filters**: If you need to perform heavy operations in the event, consider using filters to filter out the subscribers that don't need to be invoked.