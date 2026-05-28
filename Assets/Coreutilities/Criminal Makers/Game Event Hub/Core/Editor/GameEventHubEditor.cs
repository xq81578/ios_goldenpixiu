using System;
using System.Linq;
using CriminalMakers.GameEventHub.Tools;
using UnityEditor;
using UnityEngine;

namespace CriminalMakers.GameEventHub
{
    [CustomEditor(typeof(GameEventHub))]
    public class GameEventHubEditor : Editor
    {
        private bool documentationExists;
        
        private void OnEnable()
        {
            documentationExists = GameEventsHelper.DocumentationExists();
        }


        public override void OnInspectorGUI()
        {
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Status", titleStyle);

            // Add some space before KPIs
            GUILayout.Space(20);

            // Draw KPI boxes
            EditorGUILayout.BeginHorizontal();

            // Active Events Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Active Events", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(CountNonSystemEventsActive().ToString(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            GUILayout.Space(5); // Space between the boxes

            // Active Subscribers Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Active Subscribers", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(CountNonSystemSubscribersActive().ToString(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();


            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Preferences", titleStyle);
            EditorGUILayout.Space(10);

            // Draw custom inspector fields, excluding the "Script" field
            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true); // Skip the "m_Script" field

            while (property.NextVisible(false))
            {
                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(20);

            EditorGUILayout.LabelField("Tools", titleStyle);
            EditorGUILayout.Space(10);

            if (documentationExists)
            {
                if (GUILayout.Button("Documentation", GUILayout.Height(30)))
                {
                    GameEventsHelper.SafetlyOpenDocumentation();
                }
            }

            GUILayout.Space(10);

            // Draw three buttons: Monitor, Tester and Log in one line
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Monitor", GUILayout.Height(20)))
            {
                SubscriptionMonitorTool.ShowWindow();
            }

            if (GUILayout.Button("Tester", GUILayout.Height(20)))
            {
                TesterTool.ShowWindow();
            }

            if (GUILayout.Button("Log", GUILayout.Height(20)))
            {
                LogTool.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        private int CountNonSystemEventsActive()
        {
            var registry = GameEventHub.GameEventHubRegistry;
            var count = registry.Keys.Count(key => !GameEventsHelper.IsSystemEvent(Type.GetType(key)));
            return count;
        }

        private int CountNonSystemSubscribersActive()
        {
            var registry = GameEventHub.GameEventHubRegistry;
            var count = 0;

            foreach (var kvp in registry)
            {
                var eventID = kvp.Key;
                var bindings = kvp.Value;

                var eventType = Type.GetType(eventID);

                if (GameEventsHelper.IsSystemEvent(eventType)) continue;

                count += bindings.Count(
                    bindingInfo => !GameEventsHelper.IsSystemEvent(bindingInfo.Subscriber.GetType()));
            }

            return count;
        }
    }
}