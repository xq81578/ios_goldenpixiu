namespace CriminalMakers.GameEventHub
{
    public enum SubscriberPriority
    {
        /// <summary>
        /// Represents the highest-priority subscriber, invoked first in the event handling sequence and executed
        /// always, even if the event is canceled by other handlers or if are not in the same scene or there is a priority filter.
        ///
        /// Use this priority for critical systems and essential validations, especially those that may decide 
        /// whether to cancel the event for lower-priority subscribers.
        /// </summary>
        Essential,

        /// <summary>
        /// Represents the default priority assigned to static subscribers within the event handling mechanism. 
        /// This priority ensures a standard execution order when no specific priority is set.
        /// </summary>
        High,

        /// <summary>
        /// Denotes the default priority level for dynamic subscribers within the event handling framework.
        /// This priority ensures a consistent execution order for subscribers that are added and removed
        /// at runtime when no specific priority is designated.
        /// </summary>
        Medium,

        /// <summary>
        /// Invoked after medium-priority subscribers have executed and before the cleanup phase.
        /// This position in the sequence allows for final adjustments or assessments 
        /// before resources are released.
        /// </summary>
        Low,

        /// <summary>
        /// Invoked as the final step in the event handling process. This is an ideal point to clean up resources,
        /// given that all other subscribers have already been executed.
        ///
        /// These subscribers will be invoked regardless of whether the event was canceled by a higher-priority or if are not in the same scene or there is a priority filter.
        /// </summary>
        Cleanup,
    }
}