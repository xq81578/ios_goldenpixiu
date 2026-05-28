using System;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public abstract class AbstractLogFilter
    {
        protected Action refresh;
        
        public virtual void Initialize(Action refresh)
        {
            this.refresh = refresh;
        }
        
        public abstract bool EvaluateFilter(GameEvent gameEvent);
        
        public abstract VisualElement DrawFilter();

        public abstract int Order { get; }
        
        public abstract StyleLength Height { get; }
    }
}