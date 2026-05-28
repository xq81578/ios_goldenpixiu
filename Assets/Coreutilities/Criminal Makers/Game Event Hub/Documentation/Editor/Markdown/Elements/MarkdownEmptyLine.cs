using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class MarkdownEmptyLine: MarkdownElement
    {
        public override bool Match(string line)
        {
            return string.IsNullOrWhiteSpace(line);
        }

        public override VisualElement Render(string line)
        {
            var space = new VisualElement();
            space.style.height = 15;
            return space;
        }
    }
}