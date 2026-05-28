# A null BindingInfo for event has been removed

When raising an event, Game Event Hub will check the subscriber before calling the method. If the subscriber is null (destroyed from the scene), Game Event Hub will warn you about this and clean up the null subscriber.

> A null BindingInfo for event {GameEventsHelper.GetEventName(eventType)} has been removed. Please ensure that the subscriber uses Unbind or calls the method returned by Listen to unsubscribe from the event before being destroyed.

To avoid this warning, ensure that the subscriber uses `Unbind` or calls the method returned by `Listen` to unsubscribe from the event before being destroyed.