using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    [CustomEditor(typeof(TextAsset))]
    public class MarkdownInspector : Editor
    {
        private VisualElement rootElement;
        private TextAsset markdownFile;
        private bool isMarkdownFile;

        public override VisualElement CreateInspectorGUI()
        {
            // Store the targeted TextAsset
            markdownFile = (TextAsset)target;

            // Check if the file is a Markdown file
            isMarkdownFile = AssetDatabase.GetAssetPath(markdownFile)
                .EndsWith(".md", StringComparison.OrdinalIgnoreCase);

            if (!isMarkdownFile)
            {
                // If it's not a Markdown file, use the default inspector
                return base.CreateInspectorGUI();
            }

            // Create a container for the UI
            rootElement = new VisualElement();
            rootElement.style.paddingTop = 10;
            rootElement.style.paddingLeft = 10;
            rootElement.style.paddingRight = 10;

            // Render the Markdown content
            RenderMarkdown();

            return rootElement;
        }

        private void RenderMarkdown()
        {
            if (markdownFile == null || !isMarkdownFile) return;

            // Clear any previous content
            rootElement.Clear();

            // Parse and display the Markdown content
            string markdownText = markdownFile.text;

            // Get the directory of the Markdown file (for handling relative paths like images)
            string selectedFilePath = AssetDatabase.GetAssetPath(markdownFile);
            string baseDirectory =
                Path.GetDirectoryName(Application.dataPath + selectedFilePath.Substring("Assets".Length));

            // Parse and render the Markdown
            var parser = new MarkdownParser(baseDirectory);
            var visualElement = parser.Parse(markdownText);

            // Add the rendered Markdown to the inspector
            rootElement.Add(visualElement);
        }
    }
}