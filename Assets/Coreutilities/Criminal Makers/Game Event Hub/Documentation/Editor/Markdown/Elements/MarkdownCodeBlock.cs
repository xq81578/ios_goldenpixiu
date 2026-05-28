using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class MarkdownCodeBlock : MarkdownElement
    {
        private static readonly Regex CodeStartRegex = new Regex(@"^```");

        private bool isWithinCodeBlock = false;

        // Syntax highlighting keywords and colors (from CodeEditorUtility)
        private static readonly string[] Keywords =
        {
            "using",
            "void",
            "typeof",
            "public",
            "private",
            "protected",
            "class",
            "this",
            "GameEventHub",
            "OnGameEvent",
            "new"
        };

        private static readonly Color[] KeywordColors =
        {
            Color.cyan,
            Color.cyan,
            Color.cyan,
            Color.red,
            Color.red,
            Color.red,
            Color.magenta,
            Color.cyan,
            Color.green,
            Color.green,
            Color.red
        };

        public override bool Match(string line)
        {
            return CodeStartRegex.IsMatch(line) || isWithinCodeBlock;
        }

        public override VisualElement Render(string line)
        {
            if (CodeStartRegex.IsMatch(line))
            {
                isWithinCodeBlock = !isWithinCodeBlock; // Toggle for entering/exiting code block
                return null; // No visual output for the ``` line itself
            }

            if (isWithinCodeBlock)
            {
                var codeBlock = new Box();
                codeBlock.style.paddingTop = 2;
                codeBlock.style.paddingBottom = 2;
                codeBlock.style.paddingLeft = 10;
                codeBlock.style.paddingRight = 10;
                codeBlock.style.marginLeft = 5;
                codeBlock.style.marginRight = 5;
                codeBlock.style.backgroundColor = new Color(0, 0, 0); // Dark gray background
                codeBlock.style.flexDirection = FlexDirection.Row;



                // Syntax-highlighted code line
                var label = new Label(ApplySyntaxHighlighting(line));
                label.style.color = Color.white; // Default text color
                label.style.fontSize = 14;
                label.style.unityFontStyleAndWeight = FontStyle.Normal;
                label.style.whiteSpace = WhiteSpace.Normal; // Allow wrapping
                label.style.flexGrow = 1; // Flexible width to fit parent

                codeBlock.Add(label);
                return codeBlock;
            }

            return null;
        }

        private string ApplySyntaxHighlighting(string codeLine)
        {
            string highlightedCode = codeLine;

            // Apply syntax highlighting by replacing keywords with colored versions
            for (int i = 0; i < Keywords.Length; i++)
            {
                // Colorize the keyword
                highlightedCode = Regex.Replace(
                    highlightedCode,
                    $@"\b{Keywords[i]}\b", // Match whole words only
                    $"<color=#{ColorUtility.ToHtmlStringRGB(KeywordColors[i])}>{Keywords[i]}</color>"
                );
            }

            return highlightedCode;
        }
    }
}