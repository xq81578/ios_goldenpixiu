#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace Slot.Common.UI
{
    [CustomEditor(typeof(RoundedRectGraphic), true)]
    [CanEditMultipleObjects]
    public class RoundedRectGraphicEditor : GraphicEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Material");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
