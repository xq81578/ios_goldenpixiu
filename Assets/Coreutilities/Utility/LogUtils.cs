
using UnityEngine;

public class LogUtils
{
    public static void Log(object message)
    {
        #if (!RELEASE_BUILD)
            Debug.Log(message);
        #endif
    }
    
    public static void LogWarning(object message)
    {
        #if (!RELEASE_BUILD)
            Debug.LogWarning(message);
        #endif
    }
    
    public static void LogError(object message)
    {
        #if (!RELEASE_BUILD)
            Debug.LogError(message);
        #endif
    }
    
    public static void LogAssertion(object message)
    {
        #if (!RELEASE_BUILD)
            Debug.LogAssertion(message);
        #endif
    }
}