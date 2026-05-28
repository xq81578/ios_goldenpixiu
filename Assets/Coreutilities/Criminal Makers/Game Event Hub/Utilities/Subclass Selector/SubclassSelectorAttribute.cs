using System;
using UnityEngine;

namespace CriminalMakers.GameEventHub.Utilities
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class SubclassSelectorAttribute : PropertyAttribute
    {
        public bool drawProperties { get; }

        public bool includeExcludedByAttribute { get; }

        public SubclassSelectorAttribute(bool drawProperties = true, bool includeExcludedByAttribute = false)
        {
            this.drawProperties = drawProperties;
            this.includeExcludedByAttribute = includeExcludedByAttribute;
        }
    }
}