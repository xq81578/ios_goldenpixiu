using System.Collections.Generic;

namespace CriminalMakers.GameEventHub
{
    public class PropagationResult
    {
        public bool PropagationStopped;
        public List<object> SubscribersInvoked;

        public PropagationResult()
        {
            PropagationStopped = false;
            SubscribersInvoked = new List<object>();
        }
    }
}