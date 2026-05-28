using System;

namespace CriminalMakers.GameEventHub.Tools
{
    [AttributeUsage(AttributeTargets.Field)]
    public class EditorPrefProp: Attribute
    {
        public string Key { get; set; }
        
        public object DefaultValue { get; set; }
        
        public string NameOfSaveMethod { get; set; }
        
        public string NameOfLoadMethod { get; set; }
        
        public EditorPrefProp(string key, object defaultValue)
        {
            Key = key;
            DefaultValue = defaultValue;
        }
        
        public EditorPrefProp(string key, object defaultValue, string nameOfSaveMethod, string nameOfLoadMethod)
        {
            Key = key;
            DefaultValue = defaultValue;
            NameOfSaveMethod = nameOfSaveMethod;
            NameOfLoadMethod = nameOfLoadMethod;
        }
    }
}