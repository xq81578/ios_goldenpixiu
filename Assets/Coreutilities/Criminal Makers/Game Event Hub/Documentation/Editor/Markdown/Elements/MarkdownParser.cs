using System.Collections.Generic;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class MarkdownParser
    {
        private readonly List<MarkdownElement> elements;

        public MarkdownParser(string baseExecutionPath)
        {
            elements = new List<MarkdownElement>
            {
                new MarkdownNoParseElement(),
                new MarkdownHeader(),
                new MarkdownCodeBlock(),
                new MarkdownEmptyLine(),
                new MarkdownBlockquote(),
                new MarkdownVideo(),
                new MarkdownImage(),
                new MarkdownLink(),
                new MarkdownParagraph() // Always keep Paragraph as the fallback
            };

            foreach (var element in elements)
            {
                element.Init(baseExecutionPath);
            }
        }

        public VisualElement Parse(string markdownText)
        {
            MarkdownVideo.CleanupAll();
            
            var root = new VisualElement();
            var lines = markdownText.Split('\n');

            foreach (var line in lines)
            {
                foreach (var element in elements)
                {
                    if (element.Match(line))
                    {
                        var renderedElement = element.Render(line);
                        if (renderedElement != null)
                        {
                            root.Add(renderedElement);
                        }

                        break;
                    }
                }
            }

            return root;
        }
    }
}