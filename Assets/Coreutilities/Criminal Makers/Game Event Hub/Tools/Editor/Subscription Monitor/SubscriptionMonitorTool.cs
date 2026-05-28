using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class SubscriptionMonitorTool : EditorWindow
    {
        private List<string> _subscriptionsKeys = new List<string>();
        private ListView _subscriptionListView;

        [MenuItem("Tools/Game Event Hub/Subscription Monitor")]
        public static void ShowWindow()
        {
            var window = GetWindow<SubscriptionMonitorTool>();
            window.titleContent = new GUIContent("Subscription Monitor");
            window.minSize = new Vector2(450, 200);
            window.maxSize = new Vector2(1920, 720);
            window.Show();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                GameEventHub.Bind(this);
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += RefreshData;
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                GameEventHub.Bind(this);
                EditorApplication.delayCall += RefreshData;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                GameEventHub.Unbind(this);
            }
        }

        [OnGameEvent(SubscriberPriority.Essential)]
        private void OnObjectBound(OnObjectBoundToEventSystem e)
        {
            RefreshData();
        }

        [OnGameEvent(SubscriberPriority.Essential)]
        private void OnObjectUnbound(OnObjectUnboundFromEventSystem e)
        {
            RefreshData();
        }

        private void CreateGUI()
        {
            _subscriptionsKeys = new List<string>();

            rootVisualElement.Add(UIToolkitHelpers.Title("Subscription Monitor"));

            _subscriptionListView = UIToolkitHelpers.CreateListView(
                _subscriptionsKeys,
                new List<BaseListViewLine<string>> { new SubscriptionLine() },
                DrawSubscriptionLine
            );

            rootVisualElement.Add(_subscriptionListView);

            EditorApplication.delayCall += RefreshData;
        }

        private VisualElement DrawSubscriptionLine()
        {
            var lineRoot = new VisualElement();
            lineRoot.style.flexDirection = FlexDirection.Row;
            lineRoot.style.justifyContent = Justify.SpaceBetween;
            lineRoot.style.alignItems = Align.Center;
            lineRoot.style.paddingLeft = 10;
            lineRoot.style.paddingRight = 10;

            var eventName = new Label("Event Name");
            eventName.name = "event-name";
            lineRoot.Add(eventName);

            var actionsContainer = new VisualElement();
            actionsContainer.style.flexDirection = FlexDirection.Row;
            actionsContainer.style.justifyContent = Justify.FlexEnd;
            actionsContainer.style.alignItems = Align.Center;

            var subscribersLabel = new Label("X Subscribers");
            subscribersLabel.name = "subscribers-label";
            subscribersLabel.style.marginRight = 10;
            actionsContainer.Add(subscribersLabel);

            actionsContainer.Add(UIToolkitHelpers.ButtonWithIcon(
                "data",
                35,
                16,
                "UnityEditor.HierarchyWindow",
                "UnityEditor.HierarchyWindow",
                "See subscribers"));

            lineRoot.Add(actionsContainer);


            return lineRoot;
        }

        private void RefreshData()
        {
            var registry = GameEventHub.GameEventHubRegistry;

            _subscriptionsKeys.Clear();

            foreach (var key in registry.Keys)
            {
                var eventType = Type.GetType(key);
                if (GameEventsHelper.IsSystemEvent(eventType)) continue;
                _subscriptionsKeys.Add(key);
            }

            if (_subscriptionListView != null)
            {
                _subscriptionListView.Rebuild();
            }
        }
    }
}