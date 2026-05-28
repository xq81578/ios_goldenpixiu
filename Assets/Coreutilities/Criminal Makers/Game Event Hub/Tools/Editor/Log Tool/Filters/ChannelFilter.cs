using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class ChannelFilter : AbstractLogFilter
    {
        public override StyleLength Height => new(StyleKeyword.Auto);

        [EditorPrefProp("GameEventHub_LogTool_Channels", null, nameof(SerializeChannels), nameof(DeserializeChannels))]
        private List<string> _channels = new List<string>();

        private TextField channelField;
        private VisualElement tagContainer;

        public override void Initialize(Action refresh)
        {
            base.Initialize(refresh);
            EditorPrefsManager.Load(this, () => refresh?.Invoke());
        }

        public override bool EvaluateFilter(GameEvent gameEvent)
        {
            if(_channels == null || _channels.Count == 0) return true;
            
            return gameEvent switch
            {
                OnEventRaised e when !_channels.Contains(e.EventRaised._channel?.ToLower()) => false,
                _ => true
            };
        }

        public override VisualElement DrawFilter()
        {
            var root = new VisualElement();
            root.style.marginTop = 10;
            root.style.marginBottom = 10;
            root.style.minWidth = new StyleLength(new Length(100, LengthUnit.Percent));

            root.Add(new Label("Channel Filter"));

            var addTagContainer = new VisualElement();
            addTagContainer.style.marginTop = 5;
            addTagContainer.style.flexDirection = FlexDirection.Row;
            addTagContainer.style.width = new StyleLength(StyleKeyword.Auto);

            channelField = new TextField();
            channelField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) // Check for Enter/Return key
                {
                    AddChannelAndRefresh();
                }
            });
            channelField.style.flexGrow = 1;
            addTagContainer.Add(channelField);

            var addbtn = new Button(AddChannelAndRefresh);
            addbtn.text = "Add";
            addTagContainer.Add(addbtn);

            root.Add(addTagContainer);

            tagContainer = new VisualElement();
            tagContainer.style.flexDirection = FlexDirection.Row;
            tagContainer.style.flexWrap = Wrap.Wrap;
            tagContainer.style.marginTop = 10;
            tagContainer.style.marginBottom = 5;

            RefreshTags(tagContainer);

            root.Add(tagContainer);

            return root;
        }

        private void AddChannelAndRefresh()
        {
            if (string.IsNullOrEmpty(channelField.value)) return;
            _channels.Add(channelField.value.ToLower());
            RefreshTags(tagContainer);
            channelField.value = "";
            channelField.Focus();
            refresh();
            EditorPrefsManager.Save(this);
        }

        private void RefreshTags(VisualElement root)
        {
            root.Clear();
            if(_channels.Count == 0)
            {
                root.Add(UIToolkitHelpers.ItalicLabel("All channels are watched (no filter)"));
                return;
            }
            
            foreach (var channel in _channels)
            {
                root.Add(CreateTag(channel, new Color(0.45490196f, 0.79607844f, 0.93333334f), () =>
                {
                    _channels.Remove(channel);
                    RefreshTags(root);
                    refresh();
                    EditorPrefsManager.Save(this);
                }));
            }
        }

        public override int Order => 3;


        private VisualElement CreateTag(string text, Color backgroundColor, Action closeButtonCallback)
        {
            // Main Tag Container
            var tag = new VisualElement();
            tag.style.flexDirection = FlexDirection.Row; // Ensures horizontal layout (label + close button)
            tag.style.backgroundColor = new StyleColor(backgroundColor); // Background color
            tag.style.color = new StyleColor(Color.black); // Default text color
            tag.style.paddingLeft = 10; // Padding for text
            tag.style.paddingTop = 2;
            tag.style.paddingBottom = 2;
            tag.style.borderTopLeftRadius = 8; // Rounded left side
            tag.style.borderBottomLeftRadius = 8;
            tag.style.borderTopRightRadius = 8; // Rounded right side
            tag.style.borderBottomRightRadius = 8;
            tag.style.marginRight = 5; // Spacing between tags
            tag.style.marginBottom = 5;

            // Label Text
            var label = new Label(text);
            label.style.unityTextAlign = TextAnchor.MiddleCenter; // Center aligned text
            label.style.flexGrow = 1;

            // Close Button ("X")
            var closeButton = new Button(() => closeButtonCallback?.Invoke()); // Register callback function
            closeButton.text = "X";
            closeButton.style.backgroundColor = Color.clear;
            closeButton.style.borderLeftWidth = 0;
            closeButton.style.borderRightWidth = 0;
            closeButton.style.borderTopWidth = 0;
            closeButton.style.borderBottomWidth = 0;
            closeButton.style.color = Color.red;
            closeButton.style.borderTopRightRadius = 4;
            closeButton.style.borderBottomRightRadius = 4;
            closeButton.style.paddingLeft = 5;
            closeButton.style.paddingRight = 5;
            closeButton.style.marginLeft = 5; // Spacing between text and button
            closeButton.style.unityTextAlign = TextAnchor.MiddleCenter; // Align text in the button

            // Add elements to the container
            tag.Add(label); // The label with text
            tag.Add(closeButton); // Add the close button

            return tag;
        }
        
        private static void SerializeChannels(string key, object value)
        {
            var channelsJoined = string.Join(";", (List<string>) value);
            EditorPrefs.SetString(key, channelsJoined);
        }
        
        private static object DeserializeChannels(string key)
        {
            var channels = EditorPrefs.GetString(key);
            return string.IsNullOrEmpty(channels) ? new List<string>() : new List<string>(channels.Split(';'));
        }
    }
}