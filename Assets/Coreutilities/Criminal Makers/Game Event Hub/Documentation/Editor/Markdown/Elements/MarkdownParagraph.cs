using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class MarkdownParagraph : MarkdownElement
    {
        // Regex to capture inline styles: **bold**, _italic_
        private static readonly Regex InlineRegex = new Regex(@"
        (\*\*(?<bold>.+?)\*\*)|   # Bold (**text**)
        (_(?<italic>.+?)_)        # Italic (_text_)
        (`(?<highlight>.+?)`)         # Inline code (highlight with `backticks`)
    ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public override bool Match(string line)
        {
            // Match any non-empty line (fallback for plain or inline-styled lines)
            return !string.IsNullOrWhiteSpace(line);
        }

        public override VisualElement Render(string line)
        {
            // Convert inline styles into rich-text formatted text
            string richTextLine = ConvertInlineStylesToRichText(line);

            // Create a single label with the rich-text content
            var label = new Label(richTextLine);

            // Enable rich text for the label
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.fontSize = 14;
            label.style.whiteSpace = WhiteSpace.Normal; // Allow wrapping
            label.style.flexGrow = 1; // Make it flexible to grow based on parent
            label.style.maxWidth = Length.Percent(100); // Constrain width to container

            return label;
        }

        private string ConvertInlineStylesToRichText(string input)
        {
            // Use Regex to substitute inline markdown styles with Unity's rich text tags
            string result = input;

            // Handle bold (**text** -> <b>text</b>)
            result = Regex.Replace(result, @"\*\*(.+?)\*\*", "<b>$1</b>");

            // Handle italic (_text_ -> <i>text</i>)
            result = Regex.Replace(result, @"_(.+?)_", "<i>$1</i>");

            // Handle inline highlight/code (`text` -> <color>text</color>)
            result = Regex.Replace(result, @"`(.+?)`", "<color=#f1c40f>$1</color>");

            return result;
        }
    }
}