using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class UIToolkitHelpers
    {
        public static VisualElement PingButton()
        {
            var visualElement = new VisualElement();

            // GameObject
            visualElement.Add(ButtonWithIcon(
                "ping-gameobject",
                35,
                16,
                "GameObject Icon",
                "GameObject Icon",
                "Ping"));
            
            // ScriptableObject
            visualElement.Add(ButtonWithIcon(
                "ping-scriptableobject",
                35,
                16,
                "ScriptableObject Icon",
                "ScriptableObject Icon",
                "Ping"));
            
            // Animator
            visualElement.Add(ButtonWithIcon(
                "ping-animator",
                35,
                16,
                "Animator Icon",
                "Animator Icon",
                "Ping"));
            
            // RectTransform (UI)
            visualElement.Add(ButtonWithIcon(
                "ping-ui",
                35,
                16,
                "Canvas Icon",
                "Canvas Icon",
                "Ping"));
            
            // No data info
            var destroyed = new Label("Object has been destroyed");
            destroyed.name = "info-label";
            destroyed.style.marginRight = 10;
            visualElement.Add(destroyed);

            return visualElement;
        }

        public static void BindPingButton(object objectToPing, VisualElement pingButtonParent)
        {
            pingButtonParent.Q<Label>("info-label").style.display = DisplayStyle.None;
            pingButtonParent.Q<Button>("ping-gameobject").style.display = DisplayStyle.None;
            pingButtonParent.Q<Button>("ping-scriptableobject").style.display = DisplayStyle.None;
            pingButtonParent.Q<Button>("ping-animator").style.display = DisplayStyle.None;
            pingButtonParent.Q<Button>("ping-ui").style.display = DisplayStyle.None;
            
            
            if (GameEventsHelper.IsObjectUnityNull(objectToPing))
            {
                pingButtonParent.Q<Label>("info-label").text = "Object has been destroyed";
                pingButtonParent.Q<Label>("info-label").style.display = DisplayStyle.Flex;
            }
            else
            {
                switch (objectToPing)
                {
                    case var _ when GameEventsHelper.IsAnimator(objectToPing):
                    {
                        pingButtonParent.Q<Button>("ping-animator").style.display = DisplayStyle.Flex;
                        pingButtonParent.Q<Button>("ping-animator").clickable = null;
                        pingButtonParent.Q<Button>("ping-animator").clicked += () =>
                        {
                            var success = GameEventsHelper.PingGameobject(objectToPing);
                            if (!success)
                            {
                                BindPingButton(objectToPing, pingButtonParent);
                            }
                        };
                        break;
                    }
                    case var _ when GameEventsHelper.IsRectTransform(objectToPing):
                    {
                        pingButtonParent.Q<Button>("ping-ui").style.display = DisplayStyle.Flex;
                        pingButtonParent.Q<Button>("ping-ui").clickable = null;
                        pingButtonParent.Q<Button>("ping-ui").clicked += () =>
                        {
                            var success = GameEventsHelper.PingGameobject(objectToPing);
                            if (!success)
                            {
                                BindPingButton(objectToPing, pingButtonParent);
                            }
                        };
                        break;
                    }
                    case var _ when GameEventsHelper.IsComponent(objectToPing):
                    {
                        pingButtonParent.Q<Button>("ping-gameobject").style.display = DisplayStyle.Flex;
                        pingButtonParent.Q<Button>("ping-gameobject").clickable = null;
                        pingButtonParent.Q<Button>("ping-gameobject").clicked += () =>
                        {
                            var success = GameEventsHelper.PingGameobject(objectToPing);
                            if (!success)
                            {
                                BindPingButton(objectToPing, pingButtonParent);
                            }
                        };
                        break;
                    }
                    case var _ when GameEventsHelper.IsScriptableObject(objectToPing):
                    {
                        pingButtonParent.Q<Button>("ping-scriptableobject").style.display = DisplayStyle.Flex;
                        pingButtonParent.Q<Button>("ping-scriptableobject").clickable = null;
                        pingButtonParent.Q<Button>("ping-scriptableobject").clicked += () =>
                        {
                            var success = GameEventsHelper.PingScriptableObject(objectToPing);
                            if (!success)
                            {
                                BindPingButton(objectToPing, pingButtonParent);
                            }
                        };
                        break;
                    }
                }
            }
        }

        public static Button ButtonWithIcon(
            string name,
            float buttonSize,
            float imageSize,
            string lightThemeIconName,
            string darkThemeIconName,
            string tooltip = ""
        )
        {
            // Create the button
            var button = new Button();
            button.name = name;
            button.tooltip = tooltip;
            button.style.width = buttonSize;
            button.style.height = buttonSize;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;

            var iconImage = DrawUnityIcon(darkThemeIconName, lightThemeIconName, imageSize, name + "_icon");

            button.Add(iconImage);

            return button; // Return the configured button
        }

        public static VisualElement ToggleWithIcons(bool initialState, GUIContent iconTrue, GUIContent iconFalse,
            Action<bool> onToggle = null)
        {
            // Create a Toggle
            var customToggle = new Toggle();

            // Set the initial state
            customToggle.value = initialState;

            // Hide the default checkbox appearance
            customToggle.Q<VisualElement>("unity-checkmark").style.display =
                DisplayStyle.None; // Remove standard checkbox visuals

            // Add a custom Image in place of the default visuals
            var iconImage = new Image();
            iconImage.image = initialState ? iconTrue.image : iconFalse.image; // Assign the appropriate icon
            iconImage.style.width = 16; // Set the size of the icon
            iconImage.style.height = 16;

            customToggle.Add(iconImage);

            // Hook up value change logic to toggle the icon
            customToggle.RegisterValueChangedCallback(evt =>
            {
                iconImage.image = evt.newValue ? iconTrue.image : iconFalse.image;
                onToggle?.Invoke(evt.newValue);
            });

            return customToggle;
        }

        public static VisualElement ListViewLine(string name, int height = 50, int padding = 10)
        {
            var rootFilterLine = new VisualElement();
            rootFilterLine.name = name;
            rootFilterLine.style.height = height;
            rootFilterLine.style.flexDirection = FlexDirection.Row;
            rootFilterLine.style.alignItems = Align.Center;
            rootFilterLine.style.paddingLeft = padding;
            rootFilterLine.style.paddingRight = padding;

            return rootFilterLine;
        }

        public static Label Title(string title)
        {
            var filtersTitle = new Label(title);
            filtersTitle.style.unityFontStyleAndWeight = FontStyle.Normal;
            filtersTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            filtersTitle.style.fontSize = 20;
            filtersTitle.style.marginTop = 20;
            filtersTitle.style.marginBottom = 20;
            return filtersTitle;
        }

        public static Label ItalicLabel(string text, int marginLeft = 0, int marginRight = 0,
            TextAnchor textAlign = TextAnchor.MiddleLeft)
        {
            var label = new Label(text);
            label.enableRichText = true;
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            label.style.marginLeft = marginLeft;
            label.style.marginRight = marginRight;
            label.style.unityTextAlign = textAlign;
            return label;
        }

        public static VisualElement Spacer(int height = 10)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            return spacer;
        }

        public static Label FullHeightLabel(string text, int fontSize = 24)
        {
            var label = new Label(text);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = fontSize;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            return label;
        }

        public static VisualElement DrawLogFilterLine()
        {
            var logFilterLine = new VisualElement();
            logFilterLine.style.flexDirection = FlexDirection.Row;
            logFilterLine.style.alignItems = Align.Center;
            logFilterLine.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
            logFilterLine.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            logFilterLine.style.justifyContent = Justify.SpaceBetween;

            return logFilterLine;
        }

        public static Image DrawUnityIcon(string darkThemeIconName, string lightThemeIconName, float imageSize,
            string name)
        {
            var iconContent = EditorGUIUtility.isProSkin
                ? EditorGUIUtility.IconContent(darkThemeIconName) // Use dark theme icon
                : EditorGUIUtility.IconContent(lightThemeIconName); // Use light theme icon

            // Add the icon as an Image inside the button
            var iconImage = new Image
            {
                name = name,
                image = iconContent.image, // Set the icon
                style =
                {
                    width = imageSize, // Set the desired size for the icon
                    height = imageSize
                }
            };

            return iconImage;
        }

        public static TwoPaneSplitView DrawTwoPanelSplitView(
            Func<VisualElement> DrawLeftPane,
            Func<VisualElement> DrawRightPane,
            List<(string name, string tooltip, string lightThemeIconName, string darkThemeIconName)> buttonData
        )
        {
            var splitView = new TwoPaneSplitView(0, 0, TwoPaneSplitViewOrientation.Horizontal);

            // Left panel
            splitView.Add(DrawLeftPane());

            // Right panel
            var rightPane = DrawRightPane();
            rightPane.style.overflow = Overflow.Hidden;

            // Base position for stacking buttons vertically
            float baseButtonTop = 10f;

            foreach (var (name, tooltip, lightThemeIconName, darkThemeIconName) in buttonData)
            {
                var button = ButtonWithIcon(name, 40, 16, lightThemeIconName, darkThemeIconName);

                button.tooltip = tooltip;
                button.style.position = Position.Absolute;
                button.style.borderTopRightRadius = 15;
                button.style.borderBottomRightRadius = 15;
                button.style.height = 30;
                button.style.marginLeft = -2; // Negative offset for alignment

                // Adjust the vertical position for stacked layout
                button.style.top = baseButtonTop;

                // Increment top position for the next button
                baseButtonTop += 40;

                rightPane.Add(button);
            }

            splitView.Add(rightPane);

            return splitView;
        }

        public static ListView CreateListView<T>(
            List<T> itemsSource, // The source of items for the ListView
            List<BaseListViewLine<T>> lines, // List of ListViewLine objects
            Func<VisualElement> makeItem, // Function to create a new item
            int itemHeight = 50, // Fixed height for each ListView item
            string name = null, // Optional ListView name
            float flexGrow = 1 // Optional flexGrow for auto-sizing
        )
        {
            // Create the ListView
            var listView = new ListView
            {
                name = name,
                fixedItemHeight = itemHeight,
                itemsSource = itemsSource,
                makeItem = makeItem,
            };
            listView.bindItem = (element, i) =>
            {
                lines.First(logLine => logLine.IsLineApplicable(itemsSource[i]))
                    .BindLogLine(element, itemsSource[i]);

                lines.First(logLine => logLine.IsLineApplicable(itemsSource[i]))
                    .AppendRightClickMenu(element, itemsSource[i], i);
            };
            listView.fixedItemHeight = itemHeight;
            listView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;

            // Apply additional styles
            listView.style.flexGrow = flexGrow;

            return listView;
        }
    }
}