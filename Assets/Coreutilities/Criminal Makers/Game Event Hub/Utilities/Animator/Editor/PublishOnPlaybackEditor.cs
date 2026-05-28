using UnityEditor;
using UnityEditor.Search;
using UnityEditorInternal;
using UnityEngine;

namespace CriminalMakers.GameEventHub.Utilities
{
    [CustomEditor(typeof(PublishOnPlayback), true)]
    public class PublishOnPlaybackEditor : Editor
    {
        private const string PreviewAnimatorKey = "GameEventHub_PreviewAnimator";

        // Inspected object Properties
        private PublishOnPlayback _publishOnPlayback;
        private SerializedProperty _playbackEventsProperty;

        // Editor elements
        private ReorderableList _playbackEventsList;

        // Editor fields
        private float _previousPlaybackTime;
        private Animator _previewAnimator;
        private Animator _previousPreviewAnimator;
        private GameObject _myGameObject;
        private AnimationClip _myAnimatorClip;


        #region PreviewAnimator handling

        private void LoadPreviewAnimator()
        {
            string animatorPath = EditorPrefs.GetString(PreviewAnimatorKey, null);
            if (animatorPath != null)
            {
                var previewGameObject = GameObject.Find(animatorPath);
                if (previewGameObject != null)
                {
                    _previewAnimator = previewGameObject.GetComponent<Animator>();
                }
            }
        }

        private void SavePreviewAnimator()
        {
            var pathToSave = _previewAnimator == null
                ? ""
                : SearchUtils.GetHierarchyPath(_previewAnimator.gameObject, false);
            EditorPrefs.SetString(PreviewAnimatorKey, pathToSave);
        }

        #endregion

        #region Inspector Lifecycle

        private void OnEnable()
        {
            if (target == null || serializedObject.targetObject == null) return;

            // Fetch the target object and its serialized properties
            _publishOnPlayback = (PublishOnPlayback)target;
            _playbackEventsProperty = serializedObject.FindProperty("playbackEvents");

            LoadPreviewAnimator();

            // Initialize the ReorderableList
            _playbackEventsList =
                new ReorderableList(serializedObject, _playbackEventsProperty, true, true, true, true);
            _playbackEventsList.drawHeaderCallback = DrawHeader;
            _playbackEventsList.drawElementCallback = DrawPlaybackEvent;

            // Fetch the associated AnimationClip
            _myAnimatorClip = StateMachineBehaviourHelper.FindClipForBehavior(_publishOnPlayback);
        }

        private void OnDisable()
        {
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
        }

        #endregion

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();

            if (WarnIfAnimationClipNotAssigned()) return;

            RenderCreateAndAddButton();

            EditorGUILayout.Space();

            _playbackEventsList.DoLayoutList();

            EditorGUILayout.Space();

            DrawAnimatorPreviewControls();

            serializedObject.ApplyModifiedProperties();
        }

        #region Drawing Methods

        /// <summary>
        /// Checks if an AnimationClip is assigned to the property and displays a warning message if it is not assigned.
        /// </summary>
        /// <returns>
        /// Returns true if the AnimationClip is not assigned, signaling that a warning was shown in the editor.
        /// Otherwise, returns false if the AnimationClip is assigned.
        /// </returns>
        private bool WarnIfAnimationClipNotAssigned()
        {
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Cannot edit in when the game is running", MessageType.Info);
                return true;
            }
            if (!Application.isPlaying && _myAnimatorClip != null) return false;

            _myAnimatorClip = StateMachineBehaviourHelper.FindClipForBehavior(_publishOnPlayback);
            EditorGUILayout.HelpBox("Please assign first an AnimationClip to the state", MessageType.Warning);
            return true;
        }

        /// <summary>
        /// Renders the controls for previewing an Animator in the Unity Editor. Displays relevant warnings or informational messages based on the state of the preview Animator or assigned AnimationClip.
        /// Allows assigning a GameObject with an Animator component to facilitate the preview functionality.
        /// Updates and handles changes in the assigned preview Animator.
        /// </summary>
        private void DrawAnimatorPreviewControls()
        {
            EditorGUILayout.LabelField("Editor-Only preview", EditorStyles.boldLabel);

            if (_previewAnimator == null)
            {
                EditorGUILayout.HelpBox("Assign a GameObject with an Animator component to preview the animation.",
                    MessageType.Info);
            }
            else
            {
                var hasAnimatorClip =
                    StateMachineBehaviourHelper.FindClipForBehavior(_previewAnimator, _publishOnPlayback) != null;

                if (!hasAnimatorClip)
                {
                    EditorGUILayout.HelpBox(
                        "Preview animator is different than the edited animator. Preview will still work.",
                        MessageType.Warning);
                }
            }

            _previewAnimator = (Animator)EditorGUILayout.ObjectField(
                "Preview Animator", // Label for the field
                _previewAnimator, // Current value of the field
                typeof(Animator), // Object type to restrict the selection to GameObject
                true // Allow scene objects (true for selecting GameObjects from the scene)
            );

            if (_previewAnimator != _previousPreviewAnimator) // Detect change
            {
                _previousPreviewAnimator = _previewAnimator; // Update the cached value
                OnPreviewAnimatorChanged();
            }
        }

        /// <summary>
        /// Renders a "Create & Add" button within the Inspector, allowing the creation of
        /// a new ScriptableObject for a game event and its automatic addition to the playback events list.
        /// Displays a contextual tip to guide the user on editing ScriptableObjects.
        /// </summary>
        private void RenderCreateAndAddButton()
        {
            GUILayout.BeginHorizontal(); // Begin a horizontal group

            var style = new GUIStyle(EditorStyles.miniLabel);
            style.richText = true;
            style.margin = new RectOffset(0, 0, 5, 0);
            GUILayout.Label("<color=yellow>Right-click > Properties</color> on a ScriptableObject to edit it", style);

            GUILayout.FlexibleSpace(); // Push subsequent elements to the right

            if (GUILayout.Button("Create & Add"))
            {
                var saved = GameEventsHelper.SaveGameEventAsScriptableObject(null);

                if (saved)
                {
                    var newEntry = new PlaybackEvent();
                    newEntry.associatedEvent = GameEventsHelper.lastCreatedGameEvent;
                    newEntry.playbackTime = _previousPlaybackTime;
                    _publishOnPlayback.playbackEvents.Add(newEntry);

                    serializedObject.ApplyModifiedProperties();
                    Repaint();
                }
            }

            GUILayout.EndHorizontal();
        }

        #endregion

        #region Data Change Handlers

        private void OnPreviewAnimatorChanged()
        {
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }

            if (_previewAnimator == null) return;

            _myGameObject = _previewAnimator.gameObject;

            SavePreviewAnimator();
        }


        private void OnPlaybackTimePropertyChanged(float newPlaybackTime)
        {
            if (_myGameObject == null || _myAnimatorClip == null) return;

            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
            }

            AnimationMode.SampleAnimationClip(_myGameObject, _myAnimatorClip, newPlaybackTime);
        }

        #endregion

        #region ReorderableList Drawers

        private void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Game Event");

            rect.x = rect.width - (rect.width * 0.3f);
            EditorGUI.LabelField(rect, "Time");
        }

        private void DrawPlaybackEvent(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _playbackEventsProperty.GetArrayElementAtIndex(index);

            rect.y += 2; // Add slight vertical padding

            // Draw the event name on the left (70% of the row width)
            var eventNameRect = new Rect(rect.x, rect.y, rect.width * 0.5f, EditorGUIUtility.singleLineHeight);

            var associatedEvent = element.FindPropertyRelative("associatedEvent");


            EditorGUI.ObjectField(eventNameRect, associatedEvent, typeof(GameEventSO), GUIContent.none);

            // Draw the playback time on the right (30% of the row width)
            var playbackTimeRect = new Rect(
                rect.x + rect.width - (rect.width * 0.3f), // Slight spacing between sections
                rect.y,
                rect.width * 0.30f,
                EditorGUIUtility.singleLineHeight
            );

            SerializedProperty playbackTimeProperty = element.FindPropertyRelative("playbackTime");

            _previousPlaybackTime = playbackTimeProperty.floatValue;

            float newPlaybackTime = EditorGUI.Slider(
                playbackTimeRect,
                GUIContent.none,
                playbackTimeProperty.floatValue,
                0f,
                _myAnimatorClip.length
            );

            // Check if the value has changed
            if (!Mathf.Approximately(_previousPlaybackTime, newPlaybackTime))
            {
                _previousPlaybackTime = newPlaybackTime; // Update cached value

                OnPlaybackTimePropertyChanged(newPlaybackTime);
            }

            // Save the new value back to the property
            playbackTimeProperty.floatValue = newPlaybackTime;
        }

        #endregion
    }
}