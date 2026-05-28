using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public abstract class MarkdownElement
    {
        protected string baseExecutionPath;
        
        public void Init(string baseExecutionPath)
        {
            this.baseExecutionPath = baseExecutionPath;
        }
        
        // Determines if this element can parse and render the given markdown line
        public abstract bool Match(string line);

        // Renders this element into a VisualElement
        public abstract VisualElement Render(string line);
    }
}