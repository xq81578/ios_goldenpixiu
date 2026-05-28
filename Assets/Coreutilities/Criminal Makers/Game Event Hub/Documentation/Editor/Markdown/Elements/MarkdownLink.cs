using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class MarkdownLink: MarkdownElement
    {
        private static readonly Regex LinkRegex = new Regex(@"\[(?<text>.*?)\]\((?<url>.+?)\)");

        public override bool Match(string line)
        {
            return LinkRegex.IsMatch(line);
        }

        public override VisualElement Render(string line)
        {
            var match = LinkRegex.Match(line);
            if (!match.Success) return null;
            
            string text = match.Groups["text"].Value;
            string url = match.Groups["url"].Value;
            
            var label = new Label(text);
            label.style.color = new Color(0.1f, 0.5f, 0.8f);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 14;
            label.style.whiteSpace = WhiteSpace.Normal; // Allow wrapping
            label.style.flexGrow = 1; // Make it flexible to grow based on parent
            label.style.maxWidth = Length.Percent(100); // Constrain width to container
            
            label.RegisterCallback<ClickEvent>(evt => Application.OpenURL(url));
            
            return label;
        }
    }
}