using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace CriminalMakers.GameEventHub.Documentation
{
    [InitializeOnLoad]
    public class MarkdownVideo : MarkdownElement
    {
        private static readonly Regex VideoRegex = new Regex(@"\!\[(?<alt>.*?)\]\((?<url>.+\.(mp4|webm|avi|mov))\)");
        private static readonly List<MarkdownVideo> ActiveVideos = new List<MarkdownVideo>(); // Track active videos

        private GameObject videoPlayerObject; // The GameObject for video rendering
        private RenderTexture renderTexture; // The RenderTexture for the video

        private VisualElement container;
        private VideoPlayer videoPlayer;
        private VisualElement videoVisualElement; // The UI element for displaying video
        private Label loadingLabel; // Label to indicate the video is loading

        static MarkdownVideo()
        {
            AssemblyReloadEvents.beforeAssemblyReload += HandleDomainReload;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChange;
            Application.quitting += CleanupAll;
        }

        // Handle domain reloads to clean up active videos
        private static void HandleDomainReload()
        {
            CleanupAll(); // Clean up all active instances
        }

        // Handle play mode state changes and clean up when entering Play mode
        private static void HandlePlayModeStateChange(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                CleanupAll(); // Clean up all active instances
            }
        }

        public override bool Match(string line)
        {
            return VideoRegex.IsMatch(line);
        }

        public override VisualElement Render(string line)
        {
            var match = VideoRegex.Match(line);
            if (!match.Success) return null;

            // Extract the alt text and the video URL from the Markdown
            string altText = match.Groups["alt"].Value;
            string videoUrl = match.Groups["url"].Value;

            // Container for video and alternative text
            container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.alignItems = Align.Center; // Center video horizontally

            // Add a loading label initially
            loadingLabel = new Label("Waiting for video. Move your mouse around to refresh Unity Editor")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.yellow,
                    marginTop = 10,
                    marginBottom = 10
                }
            };
            container.Add(loadingLabel);

            // Resolve video path
            var resolvedPath = ResolvePath(videoUrl);

            if (File.Exists(resolvedPath))
            {
                // Create and configure a new video GameObject if none exists
                if (videoPlayerObject == null)
                {
                    videoPlayerObject = new GameObject("MarkdownVideoPlayer");
                    videoPlayerObject.SetActive(true);
                    videoPlayerObject.hideFlags = HideFlags.HideAndDontSave;

                    videoPlayer = videoPlayerObject.AddComponent<VideoPlayer>();

                    videoPlayer.url = resolvedPath;
                    videoPlayer.isLooping = true;
                    videoPlayer.playOnAwake = false;
                    videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

                    // Set up RenderTexture after the video player is prepared
                    EditorApplication.update += CheckPreparation;
                    videoPlayer.Prepare();

                    // Add this MarkdownVideo instance to the active list
                    ActiveVideos.Add(this);
                }
            }
            else
            {
                // If the video file is not found, fallback to alt text
                var altLabel = new Label(altText)
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Italic,
                        color = Color.gray
                    }
                };

                // Remove the "Loading..." label and show the fallback alt text
                container.Remove(loadingLabel);
                loadingLabel = null;

                container.Add(altLabel);
            }

            // Return the complete container
            return container;
        }

        void CheckPreparation()
        {
            if (videoPlayer == null)
            {
                EditorApplication.update -= CheckPreparation; // Stop checking once prepared
                return;
            }

            if (videoPlayer.isPrepared)
            {
                EditorApplication.update -= CheckPreparation; // Stop checking once prepared
                OnVideoPrepared(videoPlayer, container);
            }
        }

        private void OnVideoPrepared(VideoPlayer source, VisualElement container)
        {
            // Remove the "Loading..." label
            if (loadingLabel != null)
            {
                container.Remove(loadingLabel);
                loadingLabel = null;
            }

            // Create a RenderTexture for the video
            renderTexture = new RenderTexture((int)source.width, (int)source.height, 0);
            source.targetTexture = renderTexture;

            // Create a VisualElement to display the video
            videoVisualElement = new VisualElement
            {
                style =
                {
                    width = source.width, // Adjust size as needed
                    height = source.height,
                    backgroundImage = new StyleBackground(Background.FromRenderTexture(renderTexture))
                }
            };

            // Force UI to repaint at regular intervals
            videoVisualElement.schedule.Execute(ForceRepaint).Every(1000 / 30); // Adjust FPS as needed
            container.Add(videoVisualElement);

            // Start playback
            source.Play();
        }

        // Cleanup all dynamically created objects and resources
        public void Cleanup()
        {
            // Destroy video player GameObject
            if (videoPlayerObject != null)
            {
                Object.DestroyImmediate(videoPlayerObject);
                videoPlayerObject = null;
            }

            // Release the RenderTexture
            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                renderTexture = null;
            }

            // Remove this instance from the list of active videos
            ActiveVideos.Remove(this);
        }

        // Static method to clean up all active video instances
        public static void CleanupAll()
        {
            foreach (var video in new List<MarkdownVideo>(ActiveVideos))
            {
                video.Cleanup();
            }
        }

        // Force UI repaint in the Unity Editor for the video to update frames
        private void ForceRepaint()
        {
            if (videoPlayerObject != null && videoPlayerObject.GetComponent<VideoPlayer>().isPlaying)
            {
                videoVisualElement.MarkDirtyRepaint();
            }
        }

        // Resolve a relative video path based on the base directory
        private string ResolvePath(string relativePath)
        {
            if (string.IsNullOrEmpty(baseExecutionPath))
            {
                Debug.LogError("baseExecutionPath is null or empty. Ensure Init() is called before use.");
                return null;
            }

            // Normalize the base execution path and return the resolved path
            return Path.GetFullPath(Path.Combine(baseExecutionPath, relativePath));
        }

        ~MarkdownVideo()
        {
            Cleanup(); // Ensure cleanup is triggered when the object is garbage collected
        }
    }
}