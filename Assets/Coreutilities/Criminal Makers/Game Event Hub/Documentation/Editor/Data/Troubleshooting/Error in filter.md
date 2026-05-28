# Error in filter

When raising an event, you can add one or more filters using `WithFilter()`. If an error occurs in the filter, the Game Event Hub will catch and log the error:

> Error in filter {filterInEvent.GetType().Name}: {e.Message} (Game Event: {gameEvent}. Skipping filter.

To resolve this issue, check the filter and the method that caused the error. Fix the issue in the filter to prevent the error from occurring again.