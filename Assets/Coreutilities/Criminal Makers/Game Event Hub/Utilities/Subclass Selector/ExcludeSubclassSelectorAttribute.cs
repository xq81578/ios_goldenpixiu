using System;
using UnityEngine;

namespace CriminalMakers.GameEventHub.Utilities
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ExcludeSubclassSelectorAttribute: PropertyAttribute
    {
        
    }
}