using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class MarkdownNoParseElement : MarkdownElement
    {
        private const string Marker = "%%"; // Defines the marker for "no parse" blocks

        public override bool Match(string line)
        {
            return line.StartsWith(Marker);
        }

        public override VisualElement Render(string line)
        {
            // Trim the marker from the beginning
            string content = line.Substring(Marker.Length).Trim();

            // Create a label to display the text as-is
            var label = new Label(content);
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal; // Preserve spaces and newlines
            label.style.flexGrow = 1;
            
            return label;
        }
    }
}