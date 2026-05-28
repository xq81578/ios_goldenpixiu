using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class MarkdownBlockquote : MarkdownElement
    {
        // Regex to match blockquote lines starting with ">"
        private static readonly Regex BlockquoteRegex = new Regex(@"^\s*> (.+)");

        public override bool Match(string line)
        {
            // Return true if the line is a blockquote
            return BlockquoteRegex.IsMatch(line);
        }

        public override VisualElement Render(string line)
        {
            // Extract the blockquote text
            var match = BlockquoteRegex.Match(line);
            if (!match.Success)
            {
                return null;
            }

            string blockquoteText = match.Groups[1].Value;

            // Create the container for the blockquote
            var container = new Box();
            container.style.paddingLeft = 10; // Indent the blockquote
            container.style.marginTop = 5;
            container.style.marginBottom = 5;
            container.style.paddingTop = 5;
            container.style.paddingBottom = 5;
            container.style.borderLeftColor = new Color(0.7f, 0.7f, 0.3f); // Light gray border
            container.style.borderLeftWidth = 4; // Width of the vertical border
            container.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f); // Light gray background
            container.style.flexDirection = FlexDirection.Column;

            // Add the blockquote text as a label
            var label = new Label(blockquoteText);
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Italic; // Italic for blockquote
            label.style.whiteSpace = WhiteSpace.Normal; // Allow wrapping
            label.style.flexGrow = 1; // Flexible size

            container.Add(label);

            return container;
        }
    }
}