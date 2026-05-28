using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    [InitializeOnLoad]
    public class WelcomeTool : EditorWindow
    {
        
        private const string WelcomeShownKey = "GameEventHub_WelcomeShown";
        
        static WelcomeTool()
        {
            // Check if the window has been shown before
            if (!EditorPrefs.HasKey(WelcomeShownKey))
            {
                // Show the welcome tool for the first time
                ShowWindow();
                
                // Mark welcome as shown
                EditorPrefs.SetBool(WelcomeShownKey, true);
            }
        }
        
        [MenuItem("Tools/Game Event Hub/Welcome", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<WelcomeTool>();
            window.titleContent = new GUIContent("Welcome");
            window.minSize = new Vector2(450, 200);
            window.maxSize = new Vector2(1920, 720);
            window.Show();
        }

        private void CreateGUI()
        {
            var scrollView = new ScrollView();

            scrollView.Add(UIToolkitHelpers.Title("Welcome to Game Event Hub"));

            var welcomeText = new Label(@"Thank you for purchasing Game Event Hub. We hope you find it useful.

You can always access this window and other tools from Tools > Game Event Hub.

");
            welcomeText.style.fontSize = 14;
            welcomeText.style.unityFontStyleAndWeight = FontStyle.Normal;
            welcomeText.style.unityTextAlign = TextAnchor.MiddleLeft;
            welcomeText.style.marginTop = 20;
            welcomeText.style.whiteSpace = WhiteSpace.Normal;
            welcomeText.style.marginLeft = 10;
            welcomeText.style.marginRight = 10;
            scrollView.Add(welcomeText);

            // Add image located in certain path
            var image = new Image();
            image.image =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Criminal Makers/Game Event Hub/_Tools/Editor/Welcome/welcome-tools-menu.png");
            scrollView.Add(image);

            scrollView.Add(
                AddSection(
                    "Start now",
                    "Add game Event Hub to scene",
                    () => { GameEventHub.CreateOrRetrieveInstance(); },
                    "Do demo",
                    () =>
                    {
                        if (GameEventsHelper.DemoSceneExists())
                        {
                            GameEventsHelper.OpenDemoScene();
                            GameEventsHelper.SafetlyOpenDocumentation("Demo Scene");
                        }
                    }
                    )
            );

            scrollView.Add(
                AddSection("Help", "Show documentation", () => { GameEventsHelper.SafetlyOpenDocumentation(); },
                    "Contact support", () => { Application.OpenURL("mailto:support@criminal-makers.com"); })
            );

            scrollView.Add(
                AddSection("Give us feedback", "Leave Rating",
                    () => { Application.OpenURL("https://assetstore.unity.com/packages/slug/303196"); },
                    "Email us",
                    () => { Application.OpenURL("mailto:support@criminal-makers.com"); })
            );

            rootVisualElement.Add(scrollView);
        }

        private VisualElement AddSection(string title, string button1, Action onClick1, string button2 = "",
            Action onClick2 = null)
        {
            var sectionContainer = new VisualElement();
            sectionContainer.style.paddingLeft = 10;
            sectionContainer.style.paddingRight = 10;
            var sectionLabel = UIToolkitHelpers.Title(title);
            sectionLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            sectionContainer.Add(sectionLabel);

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            if (!string.IsNullOrEmpty(button1))
            {
                var button1Button = new Button(onClick1);
                button1Button.text = button1;
                button1Button.style.height = 30;
                button1Button.style.flexGrow = 1;
                buttonContainer.Add(button1Button);
            }

            if (!string.IsNullOrEmpty(button2))
            {
                var button2Button = new Button(onClick2);
                button2Button.text = button2;
                button2Button.style.height = 30;
                button2Button.style.flexGrow = 1;
                buttonContainer.Add(button2Button);
            }

            sectionContainer.Add(buttonContainer);
            return sectionContainer;
        }
    }
}