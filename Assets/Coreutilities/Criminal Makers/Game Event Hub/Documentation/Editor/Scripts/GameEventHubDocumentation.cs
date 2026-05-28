using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CriminalMakers.GameEventHub.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Documentation
{
    public class GameEventHubDocumentation : EditorWindow
    {
        // Events
        public event Action OnWindowReady;

        // UI Elements
        private TwoPaneSplitView _splitView;
        private List<DocumentationSection> _documentationItems = new();
        private VisualElement markdownContainer;
        private ListView sectionList;
        private Label fileName;
        private Label indexCounter;

        // Data
        private DocumentationSection selectedSection;
        private int currentIndex;

        public int CurrentIndex
        {
            get => currentIndex;
            set
            {
                if (selectedSection == null) return;

                currentIndex = value;
                fileName.text = selectedSection.documentationItems[currentIndex].name;
                indexCounter.text = $"{currentIndex + 1}/{(selectedSection.documentationItems.Count)}";
            }
        }

        [MenuItem("Tools/Game Event Hub/Documentation", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<GameEventHubDocumentation>("Documentation");
            window.minSize = new Vector2(600, 400);
        }

        [MenuItem("Tools/Game Event Hub/Changelog", false, priority = 2)]
        public static void ShowChangelog()
        {
            ShowWindow("Change log");
        }

        public static void ShowWindow(string sectionName)
        {
            var window = GetWindow<GameEventHubDocumentation>("Documentation");
            window.minSize = new Vector2(600, 400);


            if (window._documentationItems.Count > 0)
            {
                window.selectedSection = window._documentationItems.FirstOrDefault(s => s.title == sectionName);
                window.CurrentIndex = 0;
                window.sectionList.selectedIndex = window._documentationItems.IndexOf(window.selectedSection);
                window.DrawMarkdown();
            }
            else
            {
                window.OnWindowReady = null;
                window.OnWindowReady += () =>
                {
                    window.selectedSection = window._documentationItems.FirstOrDefault(s => s.title == sectionName);
                    window.CurrentIndex = 0;
                    window.sectionList.selectedIndex = window._documentationItems.IndexOf(window.selectedSection);
                    window.DrawMarkdown();
                };
            }
        }

        private void LoadAllDocumentation()
        {
            _documentationItems.Clear();
            string[] guids = AssetDatabase.FindAssets("t:DocumentationSection");

            foreach (var guid in guids)
            {
                // Convert GUID to asset path
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // Check if the file has a .md extension
                _documentationItems.Add(AssetDatabase.LoadAssetAtPath<DocumentationSection>(assetPath));
            }

            _documentationItems.Sort((a, b) => a.order.CompareTo(b.order));
        }

        private void CreateGUI()
        {
            LoadAllDocumentation();
            _splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);

            _splitView.Add(DrawLeftPane());

            _splitView.Add(DrawRightPane());

            rootVisualElement.Add(_splitView);

            OnWindowReady?.Invoke();
        }

        private VisualElement DrawLeftPane()
        {
            var root = new VisualElement();

            root.Add(UIToolkitHelpers.Title("Documentation"));

            sectionList = new ListView();
            sectionList.fixedItemHeight = 50;
            sectionList.style.flexGrow = 1;
            sectionList.selectionType = SelectionType.Single;
            sectionList.itemsSource = _documentationItems;
#if UNITY_2022_1_OR_NEWER
            sectionList.selectionChanged += objects =>
            {
                selectedSection = objects.FirstOrDefault() as DocumentationSection;
                CurrentIndex = 0;
                DrawMarkdown();
            };
#endif
#if !UNITY_2022_1_OR_NEWER
            sectionList.onSelectionChange += objects =>
            {
                selectedSection = objects.FirstOrDefault() as DocumentationSection;
                CurrentIndex = 0;
                DrawMarkdown();
            };
#endif
            sectionList.makeItem = () =>
            {
                var label = new Label();
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.paddingLeft = 10;


                return label;
            };
            sectionList.bindItem = (element, i) =>
            {
                var label = element as Label;
                label.text = _documentationItems[i].title;
            };


            root.Add(sectionList);

            var addSectionBtn = new Button(() =>
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Documentation Section", // Title of the save dialog
                    "NewDocumentationSection", // Default file name
                    "asset", // File extension
                    "Please select a location to save the new Documentation Section."); // Prompt message

                // Ensure the user selected a valid path
                if (string.IsNullOrEmpty(path)) return;

                // Create a new instance of the ScriptableObject
                DocumentationSection newSection = CreateInstance<DocumentationSection>();

                // Save it at the selected path
                AssetDatabase.CreateAsset(newSection, path);
                AssetDatabase.SaveAssets();

                // Optional: Select the newly created asset in the editor
                Selection.activeObject = newSection;

                Debug.Log($"New Documentation Section saved at: {path}");
            });
            addSectionBtn.text = "Create new section";
            addSectionBtn.style.minHeight = 40;
            root.Add(addSectionBtn);

            return root;
        }

        private VisualElement DrawRightPane()
        {
            var rightPane = new VisualElement();

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.justifyContent = Justify.FlexEnd;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingTop = 10;
            toolbar.style.paddingRight = 10;
            toolbar.style.minHeight = 50;
            fileName = new Label("No file selected");
            fileName.style.color = Color.yellow;
            toolbar.Add(fileName);

            indexCounter = new Label("x");

            var prevButton = UIToolkitHelpers.ButtonWithIcon("prev", 28, 10, "Animation.PrevKey", "Animation.PrevKey");
            prevButton.clicked += () =>
            {
                if (selectedSection == null) return;
                if (CurrentIndex - 1 < 0) return;
                CurrentIndex--;
                DrawMarkdown();
            };
            toolbar.Add(prevButton);

            toolbar.Add(indexCounter);

            var nextButton = UIToolkitHelpers.ButtonWithIcon("next", 28, 10, "Animation.NextKey", "Animation.NextKey");
            nextButton.clicked += () =>
            {
                if (selectedSection == null) return;
                if (CurrentIndex + 1 >= selectedSection.documentationItems.Count) return;
                CurrentIndex++;
                DrawMarkdown();
            };
            toolbar.Add(nextButton);

            rightPane.Add(toolbar);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);

            markdownContainer = new VisualElement();
            markdownContainer.style.paddingTop = 20;
            markdownContainer.style.paddingLeft = 20;
            markdownContainer.style.paddingRight = 20;
            markdownContainer.style.paddingBottom = 20;

            scrollView.Add(markdownContainer);

            rightPane.Add(scrollView);

            DrawMarkdown();

            return rightPane;
        }

        private void DrawMarkdown()
        {
            markdownContainer.Clear();

            if (selectedSection == null)
            {
                return;
            }

            // Get the content of the selected file
            string markdownText = selectedSection.documentationItems[currentIndex].text;

            // Get the directory of the Markdown file (useful for relative paths like images)
            string selectedFilePath = AssetDatabase.GetAssetPath(selectedSection.documentationItems[currentIndex]);
            string baseDirectory =
                Path.GetDirectoryName(Application.dataPath + selectedFilePath.Substring("Assets".Length));

            // Parse and render the Markdown
            var parser = new MarkdownParser(baseDirectory);
            var markdownVisualElement = parser.Parse(markdownText);

            // Add the parsed Markdown to the UI
            markdownContainer.Add(markdownVisualElement);
        }
    }
}