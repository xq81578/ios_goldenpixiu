using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class EventActorsTool : EditorWindow
    {
        private EventActorData _publisher;
        private List<EventActorData> _subscribers;

        public static void ShowWindow(EventActorData publisher, List<EventActorData> subscribers, string eventName)
        {
            var window = GetWindow<EventActorsTool>();
            window.titleContent = new GUIContent(eventName);
            window._publisher = publisher;
            window._subscribers = subscribers;
            window.CreateGUI();
            window.Show();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            bool nothing = true;
            if (_publisher != null)
            {
                nothing = false;
                DrawPublisherTable();
            }

            if (_subscribers != null && _subscribers.Count > 0)
            {
                nothing = false;
                DrawSubscribersTable();
            }

            if (nothing)
            {
                rootVisualElement.Add(UIToolkitHelpers.Title("Cannot retrieve event data"));
            }
        }

        private void DrawSubscribersTable()
        {
            rootVisualElement.Add(UIToolkitHelpers.Title("Subscribers"));

            rootVisualElement.Add(UIToolkitHelpers.CreateListView(
                _subscribers,
                new List<BaseListViewLine<EventActorData>> { new EventActorLine() },
                DrawLine)
            );
        }

        private void DrawPublisherTable()
        {
            rootVisualElement.Add(UIToolkitHelpers.Title("Publisher"));

            List<EventActorData> data = new List<EventActorData> { _publisher };

            rootVisualElement.Add(UIToolkitHelpers.CreateListView(
                data,
                new List<BaseListViewLine<EventActorData>> { new EventActorLine() },
                DrawLine)
            );
        }

        private VisualElement DrawLine()
        {
            var lineRoot = new VisualElement();
            lineRoot.style.flexDirection = FlexDirection.Row;
            lineRoot.style.justifyContent = Justify.SpaceBetween;
            lineRoot.style.alignItems = Align.Center;
            lineRoot.style.paddingLeft = 10;
            lineRoot.style.paddingRight = 10;

            var eventName = new Label("Actor Name");
            eventName.name = "actor-name";
            lineRoot.Add(eventName);

            var actionsContainer = new VisualElement();
            actionsContainer.style.flexDirection = FlexDirection.Row;
            actionsContainer.style.justifyContent = Justify.FlexEnd;
            actionsContainer.style.alignItems = Align.Center;

            actionsContainer.Add(UIToolkitHelpers.PingButton());

            lineRoot.Add(actionsContainer);


            return lineRoot;
        }
    }
}