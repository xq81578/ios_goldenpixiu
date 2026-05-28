using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Utilities
{
    [CustomEditor(typeof(GameEventSO))]
    public class GameEventSoInspector : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            // Create the root container for the custom inspector
            var root = new VisualElement();

            // Get the serialized properties of the object
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true); // Skip the "Script" property

            while (property.NextVisible(false))
            {
                // Create a UI Toolkit field for each property
                var propertyField = new PropertyField(property);
                propertyField.Bind(serializedObject); // Bind the property to the serialized object
                root.Add(propertyField);
            }

            // Add a custom button at the bottom of the inspector
            var publish = new Button(() =>
            {
                var gameEventSo = serializedObject.targetObject as GameEventSO;
                if(gameEventSo?.GameEvent == null) return;
                gameEventSo.FeedEventProperties();
#pragma warning disable GameEventHub004
                gameEventSo.GameEvent.Publish();
#pragma warning restore GameEventHub004
            })
            {
                text = "Publish"
            };
            publish.style.marginTop = 10;
            publish.style.height = 40;
            root.Add(publish);

            return root;
        }
    }
}