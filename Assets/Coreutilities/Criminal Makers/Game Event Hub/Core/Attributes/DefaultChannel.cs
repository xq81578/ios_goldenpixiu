using System;

namespace CriminalMakers.GameEventHub
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultChannel: Attribute
    {
        public string Channel { get; set; }
        
        public DefaultChannel(string channel)
        {
            Channel = channel;
        }
    }
}