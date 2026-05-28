using UnityEngine;
using UnityEditor;
using UnityEngine.Events;

/// <summary>
/// Editor extensions.
/// </summary>
[CustomEditor(typeof(ToggleButton))]
public class ToggleButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ToggleButton toggle = target as ToggleButton;

        Undo.RecordObject(toggle, "record property");


        toggle.Interactable = EditorGUILayout.Toggle("Interactable", toggle.Interactable);
        toggle.IsOn = EditorGUILayout.Toggle("Is On", toggle.IsOn);

        if (toggle.Interactable)
            if (toggle.IsOn)
                toggle.sprite = toggle.OnSprite;
            else
                toggle.sprite = toggle.OffSprite;
        else
            toggle.sprite = toggle.DisabledSprite;

        toggle.OnSprite = EditorGUILayout.ObjectField("On Sprite", toggle.OnSprite, typeof(Sprite), true) as Sprite;
        toggle.OffSprite = EditorGUILayout.ObjectField("Off Sprite", toggle.OffSprite, typeof(Sprite), true) as Sprite;
        toggle.DisabledSprite = EditorGUILayout.ObjectField("Disabled Sprite", toggle.DisabledSprite, typeof(Sprite), true) as Sprite;
        // GUILayout.Label("callback event [void onToggle(bool toggleValue)]", GUI.skin.box);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onToggle"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onToggleReversed"));
        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("SetNativeSize"))
            toggle.SetNativeSize();
    }
}
