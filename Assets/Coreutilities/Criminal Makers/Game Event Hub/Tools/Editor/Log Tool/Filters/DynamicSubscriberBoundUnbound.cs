using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class DynamicSubscriberBoundUnbound : AbstractLogFilter
    {
        public override int Order => 1;
        
        public override StyleLength Height => 50;

        [EditorPrefProp("GameEventHub_LogTool_DynamicSubscriber", true)]
        private bool _isEnabled = true;

        public override void Initialize(Action refresh)
        {
            base.Initialize(refresh);
            EditorPrefsManager.Load(this, () => refresh?.Invoke());
        }

        public override bool EvaluateFilter(GameEvent gameEvent)
        {
            return gameEvent switch
            {
                OnObjectBoundToEventSystem { isStatic: false } => _isEnabled,
                OnObjectUnboundFromEventSystem { isStatic: false } => _isEnabled,
                _ => true
            };
        }

        public override VisualElement DrawFilter()
        {
            var rootFilterLine = UIToolkitHelpers.DrawLogFilterLine();

            var icon = UIToolkitHelpers.DrawUnityIcon("sv_icon_dot15_pix16_gizmo", "sv_icon_dot15_pix16_gizmo", 16,
                "filter-icon");
            rootFilterLine.Add(icon);

            var label = new Label("Dynamic subscriber bound/unbound");
            label.style.flexGrow = 1;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.textOverflow = TextOverflow.Ellipsis;
            rootFilterLine.Add(label);


            rootFilterLine.Add(UIToolkitHelpers.ToggleWithIcons(_isEnabled,
                EditorGUIUtility.IconContent("d_scenevis_visible_hover"),
                EditorGUIUtility.IconContent("d_PBrowserPackagesNotVisible"),
                b =>
                {
                    _isEnabled = b;
                    EditorPrefsManager.Save(this);
                    refresh();
                }));

            return rootFilterLine;
        }
    }
}