using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class MarkdownImage : MarkdownElement
    {
        // Regex to match the Markdown image syntax: ![alt text](image_url)
        private static readonly Regex ImageRegex = new Regex(@"\!\[(?<alt>.*?)\]\((?<url>.+\.(png|jpg|jpeg))\)");

        public override bool Match(string line)
        {
            // Check if the line matches the image syntax
            return ImageRegex.IsMatch(line);
        }

        public override VisualElement Render(string line)
        {
            var match = ImageRegex.Match(line);
            if (!match.Success) return null;

            // Extract alt text and image URL
            string altText = match.Groups["alt"].Value;
            string imageUrl = match.Groups["url"].Value;

            // Create a container for the image and alt text
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.alignItems = Align.Center; // Center the image horizontally

            // Create an Image visual element
            var image = new Image();

            // Load and assign the image
            var resolvedPath = ResolvePath(imageUrl); // Resolve relative path to absolute
            Texture2D texture = LoadTexture(resolvedPath); // Load texture from the resolved path

            if (texture != null)
            {
                image.image = texture;

                // Dynamically adjust width and height to match the loaded image
                image.style.width = texture.width;
                image.style.height = texture.height;

                // Add the image to the container
                container.Add(image);
            }
            else
            {
                // Add fallback alt text if the image fails to load
                var altLabel = new Label(altText);
                altLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                altLabel.style.color = Color.gray;
                container.Add(altLabel);
            }

            return container;
        }

        // Resolve a relative image path starting from baseExecutionPath
        private string ResolvePath(string relativePath)
        {
            if (string.IsNullOrEmpty(baseExecutionPath))
            {
                Debug.LogError("baseExecutionPath is null or empty. Ensure Init() is called before use.");
                return null;
            }

            // Normalize the base execution path and combine with the relative path
            string resolvedPath = Path.GetFullPath(Path.Combine(baseExecutionPath, relativePath));

            return resolvedPath;
        }

        // Helper function to load a texture from a file path
        private Texture2D LoadTexture(string filePath)
        {
            // Create a new Texture2D
            Texture2D texture = new Texture2D(372, 174);

            try
            {
                // Check if the file exists
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"Image file does not exist: {filePath}");
                    return null;
                }

                // Load the image data from the file
                byte[] imageData = File.ReadAllBytes(filePath);
                if (imageData.Length > 0)
                {
                    texture.LoadImage(imageData); // Load texture data
                    return texture;
                }
            }
            catch (Exception e)
            {
                // Handle exceptions gracefully for debugging purposes
                Debug.LogWarning($"Failed to load image at '{filePath}': {e.Message}");
            }

            return null; // Return null if image loading fails
        }
    }
}