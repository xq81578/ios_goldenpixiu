#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace Slot.Common.UI
{
    [CustomEditor(typeof(SettleUI))]
    public class SettleUIInspector : Editor
    {
        private SettleUI settleUI;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            settleUI = (SettleUI)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SettleUI Tool", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show"))
                    settleUI.Show(10).Forget();
                if (GUILayout.Button("Show (isTrigger)"))
                    settleUI.Show(5, true).Forget();
                if (GUILayout.Button("Hide"))
                    settleUI.Hide().Forget();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto SetUp"))
                    AutoSetUp();
            }

            if (GUILayout.Button("Repaint"))
                Repaint();
        }

        private void AutoSetUp()
        {
            SkeletonGraphic spine = settleUI.GetComponentInChildren<SkeletonGraphic>();
            AutoSetUpTool.SetUpSpine(spine, "ani_totalWin_SkeletonData", "ani_cutscene_SkeletonData");
            var aniNames = AutoSetUpTool.GetSetUpSpineInLoopString(spine);

            // 只在值為 null 時才設定
            var animationInField = settleUI.GetType().GetField("_animationIn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if ((string)animationInField.GetValue(settleUI) == "")
            {
                animationInField.SetValue(settleUI, aniNames.Item1);
            }
            
            var animationLoopField = settleUI.GetType().GetField("_animationLoop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if ((string)animationLoopField.GetValue(settleUI) == "")
            {
                animationLoopField.SetValue(settleUI, aniNames.Item2);
            }

            AutoSetUpTool.SetUpImage(settleUI.transform, "CongratulationsImage", "tx_congratulation");
            AutoSetUpTool.SetUpImage(settleUI.transform, "YouHaveWonImage", "tx_youHaveWon");
            AutoSetUpTool.SetUpImage(settleUI.transform, "FeatureCompleteImage", "tx_featureComplete", "tx_congratulation");

            EditorUtility.SetDirty(this);
        }
    }
}
#endif