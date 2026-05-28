using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class MarkdownHeader : MarkdownElement
    {
        // Match headers like "# Header"
        private static readonly Regex HeaderRegex = new Regex(@"^(#+)\s*(.+)");

        public override bool Match(string line)
        {
            return HeaderRegex.IsMatch(line);
        }

        public override VisualElement Render(string line)
        {
            var match = HeaderRegex.Match(line);
            if (!match.Success) return null;

            var headerLevel = match.Groups[1].Value.Length; // Number of #
            var content = match.Groups[2].Value;

            // Create and style the label based on header level
            var label = new Label(content);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 22 - (headerLevel * 2); // Smaller font for deeper headers
            label.style.marginBottom = 6;

            return label;
        }
    }
}