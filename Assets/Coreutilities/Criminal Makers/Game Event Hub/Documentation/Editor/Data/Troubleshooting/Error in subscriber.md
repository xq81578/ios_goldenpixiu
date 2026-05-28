# Error in subscriber

When an event is raised, every method subscribed to that event will be called. These methods might throw exceptions, which will be caught and logged by the Game Event Hub to help you identify the issue.

> Error in subscriber {subscriber}: {stack trace}

To resolve this issue, check the subscriber and the method that caused the error. Fix the issue in the subscriber to prevent the error from occurring again.