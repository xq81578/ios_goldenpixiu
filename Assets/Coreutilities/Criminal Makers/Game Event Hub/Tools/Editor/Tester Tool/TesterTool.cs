using System;
using CriminalMakers.GameEventHub.Utilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class TesterTool : EditorWindow
    {
        private SerializedObject _serializedObject;

        [SerializeField] private GameEventSO testToolGameEventSo;

        [MenuItem("Tools/Game Event Hub/Tester")]
        public static void ShowWindow()
        {
            var window = GetWindow<TesterTool>();
            window.titleContent = new GUIContent("Tester Tool");
            window.minSize = new Vector2(450, 200);
            window.maxSize = new Vector2(1920, 720);
            window.CreateGUI();
            window.Show();
        }

        public static void ShowWindow(GameEvent gameEvent)
        {
            var window = GetWindow<TesterTool>();
            window.titleContent = new GUIContent("Tester Tool");
            window.minSize = new Vector2(450, 200);
            window.maxSize = new Vector2(1920, 720);
            window.CreateGUI();
            window.Show();
            window.CreateTemporalGameEventSo();
            window.testToolGameEventSo.GameEvent = gameEvent.CopyEvent();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
        
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (testToolGameEventSo == null)
            {
                CreateTemporalGameEventSo();
            }
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            if (_serializedObject == null || _serializedObject.targetObject == null)
            {
                _serializedObject = new SerializedObject(this);
            }
            
            if (testToolGameEventSo == null)
            {
                CreateTemporalGameEventSo();
            }


            rootVisualElement.Add(UIToolkitHelpers.Title("Tester tool"));

            var inspectorContainer = new VisualElement();
            inspectorContainer.style.marginTop = 20;
            inspectorContainer.style.marginRight = 10;
            inspectorContainer.style.flexGrow = 1;

            var horizontalContainer = new VisualElement();
            horizontalContainer.style.flexDirection = FlexDirection.Row;
            horizontalContainer.style.justifyContent = Justify.SpaceBetween;
            horizontalContainer.style.alignItems = Align.Center;
            horizontalContainer.style.marginLeft = 10;
            horizontalContainer.style.marginRight = 10;

            // Add a Property field of type GameObject
            var gameEventSoPropertyField = new PropertyField();
            gameEventSoPropertyField.label = "";
            gameEventSoPropertyField.style.flexGrow = 1;
            var favoritesProperty = _serializedObject.FindProperty("testToolGameEventSo");
            // _serializedObject.FindProperty("testToolGameEventSo").
            gameEventSoPropertyField.Unbind();
            gameEventSoPropertyField.BindProperty(favoritesProperty);
            gameEventSoPropertyField.RegisterValueChangeCallback((evt) =>
            {
                inspectorContainer.Clear();

                // Check for MissingReferenceException
                if (evt.changedProperty.objectReferenceValue == null || evt.changedProperty.objectReferenceValue.Equals(null))
                {
                    return;
                }
                
                if (evt.changedProperty.objectReferenceValue is GameEventSO newWrapper)
                {
                    // Dynamically add an InspectorElement for the new object
                    var gameEventInspector = new InspectorElement(newWrapper);
                    gameEventInspector.style.flexGrow = 1; // Expand to fill space
                    inspectorContainer.Add(gameEventInspector);
                }
                else
                {
                    var noSoAssignedText = UIToolkitHelpers.ItalicLabel(
                        "No Game Event SO assigned. Create or select one to inspect.");
                    noSoAssignedText.style.marginLeft = 10;
                    // Show a placeholder for when no ScriptableObject is assigned
                    inspectorContainer.Add(noSoAssignedText);
                }
            });

            horizontalContainer.Add(gameEventSoPropertyField);

            var createBtn = UIToolkitHelpers.ButtonWithIcon("new-temp", 20, 12, "d_Toolbar Plus",
                "d_Toolbar Plus", "New temporal");
            createBtn.clickable = null;
            createBtn.clicked += CreateTemporalGameEventSo;
            horizontalContainer.Add(createBtn);

            var saveBtn = UIToolkitHelpers.ButtonWithIcon("save", 20, 12, "d_SaveAs", "d_SaveAs", "Save");
            saveBtn.clickable = null;
            saveBtn.clicked += () =>
            {
                if (testToolGameEventSo != null)
                {
                    GameEventsHelper.SaveGameEventAsScriptableObject(testToolGameEventSo.GameEvent);
                }
            };
            horizontalContainer.Add(saveBtn);

            rootVisualElement.Add(horizontalContainer);


            var gameEventInspector = new InspectorElement(testToolGameEventSo);
            gameEventInspector.style.flexGrow = 1;

            inspectorContainer.Add(gameEventInspector);

            rootVisualElement.Add(inspectorContainer);
        }

        private void CreateTemporalGameEventSo()
        {
            testToolGameEventSo = CreateInstance<GameEventSO>();
            testToolGameEventSo.name = GameEventsHelper.TestToolName;
        }
    }
}