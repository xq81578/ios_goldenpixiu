#if UNITY_EDITOR
using System.Reflection;
using Slot.Common;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace Slot.Common.UI
{
    [CustomEditor(typeof(WinCelebrationUI))]
    public class WinCelebrationUIInspector : Editor
    {
        private WinCelebrationUI winCelebrationUI;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            winCelebrationUI = (WinCelebrationUI)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("WinCelebrationUI Tool", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show"))
                    winCelebrationUI.ShowTotalWin(100, 5000000).Forget();
                if (GUILayout.Button("Hide"))
                    winCelebrationUI.Hide().Forget();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set Default Data"))
                    SetDefaultData();
                if (GUILayout.Button("Set Spin Data"))
                    AutoSetUp();
                if (GUILayout.Button("Clear Spine"))
                    CleanSpineAsset();
            }

            if (GUILayout.Button("Repaint"))
                Repaint();
        }

        private void SetDefaultData()
        {
            WinAnimationData[] defaultWinAnimationDatas = new WinAnimationData[]
            {
                new WinAnimationData()
                {
                    Type = WinCelebrationType.BigWin,
                    Ratio = 10,
                    AnimationName = "bigWin",
                    IntroSuffix = "_in",
                    LoopSuffix = "_loop",
                    LandscapeSuffix = "_L",
                    PortraitSuffix = "_P",
                    AnimationDuration = 4f,
                    AudioName = "bw_bigwin",
                    VoiceAudioName = "vo_bigwin",
                },
                new WinAnimationData()
                {
                    Type = WinCelebrationType.MegaWin,
                    Ratio = 25,
                    AnimationName = "megaWin",
                    IntroSuffix = "_in",
                    LoopSuffix = "_loop",
                    LandscapeSuffix = "_L",
                    PortraitSuffix = "_P",
                    AnimationDuration = 4f,
                    AudioName = "bw_megawin",
                    VoiceAudioName = "vo_megawin",
                },
                new WinAnimationData()
                {
                    Type = WinCelebrationType.SuperWin,
                    Ratio = 50,
                    AnimationName = "superWin",
                    IntroSuffix = "_in",
                    LoopSuffix = "_loop",
                    LandscapeSuffix = "_L",
                    PortraitSuffix = "_P",
                    AnimationDuration = 4f,
                    AudioName = "bw_superwin",
                    VoiceAudioName = "vo_superwin",
                },
                new WinAnimationData()
                {
                    Type = WinCelebrationType.EpicWin,
                    Ratio = 100,
                    AnimationName = "epicWin",
                    IntroSuffix = "_in",
                    LoopSuffix = "_loop",
                    LandscapeSuffix = "_L",
                    PortraitSuffix = "_P",
                    AnimationDuration = 4f,
                    AudioName = "bw_epicwin",
                    VoiceAudioName = "vo_epicwin",
                },
            };

            typeof(WinCelebrationUI)
                .GetField("_winAnimationDatas", BindingFlags.NonPublic | BindingFlags.Instance)?
                .SetValue(winCelebrationUI, defaultWinAnimationDatas);

            EditorUtility.SetDirty(this);
        }

        private void AutoSetUp()
        {
            SkeletonGraphic spine = winCelebrationUI.GetComponentInChildren<SkeletonGraphic>();
            AutoSetUpTool.SetUpSpine(spine, "ani_win_SkeletonData");
            EditorUtility.SetDirty(this);
        }

        private void CleanSpineAsset()
        {
            SkeletonGraphic spine = winCelebrationUI.GetComponentInChildren<SkeletonGraphic>();
            spine.skeletonDataAsset = null;
            EditorUtility.SetDirty(this);
        }
    }
}
#endif