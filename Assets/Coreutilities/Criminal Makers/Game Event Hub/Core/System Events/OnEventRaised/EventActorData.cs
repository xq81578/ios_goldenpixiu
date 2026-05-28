namespace CriminalMakers.GameEventHub
{
    public class EventActorData
    {
        public string Name;
        public object OriginalObject;

        public EventActorData()
        {
        }

        public EventActorData(string name, object originalObject)
        {
            Name = name;
            OriginalObject = originalObject;
        }
    }
}