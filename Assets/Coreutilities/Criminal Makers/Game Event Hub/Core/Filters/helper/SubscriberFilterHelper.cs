using UnityEngine;

namespace CriminalMakers.GameEventHub
{
    public static class SubscriberFilterHelper
    {
        public static GameObject ExtractGameObject(object obj)
        {
            if (obj is GameObject)
            {
                return obj as GameObject;
            }
            if (obj is Component)
            {
                return (obj as Component).gameObject;
            }
            
            return null;
        }

        public static Component ExtractComponent(object obj)
        {
            if (obj is Component)
            {
                return obj as Component;
            }
            
            return null;
        }
        
        public static bool IsComponent(object obj)
        {
            return obj is Component;
        }
        
        public static bool IsGameObject(object obj)
        {
            return obj is GameObject;
        }
    }
}