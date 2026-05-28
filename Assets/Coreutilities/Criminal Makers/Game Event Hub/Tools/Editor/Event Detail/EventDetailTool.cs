using System.Globalization;
using System.Linq;
using CriminalMakers.GameEventHub.Utilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class EventDetailTool : EditorWindow
    {
        [SerializeReference, SubclassSelector] public GameEvent gameEvent;

        private SerializedObject _serializedObject;


        public static void ShowWindow(GameEvent gameEvent)
        {
            var window = GetWindow<EventDetailTool>();
            window.titleContent = new GUIContent(GameEventsHelper.GetEventName(gameEvent.GetType()));
            window.gameEvent = gameEvent;
            window.Show();
            EditorApplication.delayCall += window.CreateGUI;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            if (gameEvent == null)
            {
                rootVisualElement.Add(UIToolkitHelpers.Title("No event selected"));
                return;
            }

            DrawGameEventInformation();
        }

        private void DrawGameEventInformation()
        {
            _serializedObject = new SerializedObject(this);

            var scrollView = new ScrollView();

            scrollView.Add(UIToolkitHelpers.Title("Event Payload"));


            var openInTesterTool = UIToolkitHelpers.ButtonWithIcon("tester-tool", 30, 16, "Selectable Icon", "Selectable Icon", "Open in tester tool");
            openInTesterTool.style.position = Position.Absolute;
            openInTesterTool.style.right = 45;
            openInTesterTool.style.top = 10;
            openInTesterTool.clicked += () =>
            {
                TesterTool.ShowWindow(gameEvent);
            };
            scrollView.Add(openInTesterTool);
            
            var saveAsScriptableObject = UIToolkitHelpers.ButtonWithIcon("save", 30, 16, "d_SaveAs", "d_SaveAs", "Save event as Scriptable Object");
            saveAsScriptableObject.style.position = Position.Absolute;
            saveAsScriptableObject.style.right = 10;
            saveAsScriptableObject.style.top = 10;
            saveAsScriptableObject.clicked += () =>
            {
                GameEventsHelper.SaveGameEventAsScriptableObject(gameEvent);
            };
            scrollView.Add(saveAsScriptableObject);

            var inspectorContainer = new VisualElement();
            inspectorContainer.style.marginTop = 10;
            inspectorContainer.style.marginLeft = 10;
            inspectorContainer.style.marginRight = 10;

            var propertyField = new PropertyField();
            propertyField.label = "";
            propertyField.SetEnabled(false);
            propertyField.BindProperty(_serializedObject.FindProperty("gameEvent"));
            inspectorContainer.Add(propertyField);

            scrollView.Add(inspectorContainer);

            // Title
            scrollView.Add(UIToolkitHelpers.Title("Event Metadata"));

            AddLabelField(scrollView, "Channel", gameEvent._channel);
            AddLabelField(scrollView, "Emitter", GameEventsHelper.IsObjectUnityNull(gameEvent._emitter) ? "Destroyed" : gameEvent._emitter.ToString());
            AddLabelField(scrollView, "Shared", gameEvent._shared.ToString());
            AddLabelField(scrollView, "Filters applied", BuildFilterString());
            AddLabelField(scrollView, "Non Cancellable", gameEvent._nonCancellable.ToString());
            AddLabelField(scrollView, "Cancelled", gameEvent._cancelled.ToString());
            AddLabelField(scrollView, "Sealed", gameEvent._sealed.ToString());
            AddLabelField(scrollView, "Timestamp", gameEvent._timestamp.ToString());
            AddLabelField(scrollView, "Invoke count", gameEvent._numberOfInvocations.ToString());

            // Execution Time
            string executionTime =
                (gameEvent._executionTime / 1000f).ToString("F5", CultureInfo.CurrentCulture) + " seconds";
            AddLabelField(scrollView, "Execution Time", executionTime);

            // Add spacing at the bottom
            scrollView.style.marginBottom = 15;

            rootVisualElement.Add(scrollView);
        }

        private void AddLabelField(VisualElement root, string label, string value)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginBottom = 10;
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Title label
            var titleLabel = new Label(label + ":");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginRight = 10;
            titleLabel.style.minWidth = 120; // Adjust for alignment
            container.Add(titleLabel);

            // Value label
            var valueLabel = new Label(value);
            valueLabel.style.whiteSpace = WhiteSpace.Normal; // Enables wrapping
            container.Add(valueLabel);

            root.Add(container);
        }

        private string BuildFilterString()
        {
            if (gameEvent._filters == null || gameEvent._filters.Count == 0)
            {
                return "None";
            }

            // join
            return string.Join(", ", gameEvent._filters.Select(filter => $"[{filter}]"));
        }
    }
}