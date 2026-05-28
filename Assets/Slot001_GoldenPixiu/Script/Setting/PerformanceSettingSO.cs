using Sirenix.OdinInspector;
using UnityEngine;

namespace Slot001_GoldenPixiu
{
    [CreateAssetMenu(fileName = "PerformanceSetting", menuName = "ScriptableObjects/PerformanceSettingSO")]
    public class PerformanceSettingSO:ScriptableObject
    {
        [Title("Line Win 连线显示间隔时间"), FoldoutGroup("Normal"), SerializeField]
        public float LineWinSwitchTime = 1.5f;
        [Title("MG Line Win 总显示时间"), FoldoutGroup("Normal"), SerializeField]
        public float MGLineWinShowTime = 2f;
 
        [Title("报奖前的等待时间"), FoldoutGroup("Normal"), SerializeField]
        public float AwardPreWaitTime = 0.5f;

        [Title("FG中 连线显示时间"), FoldoutGroup("FreeGameSetting"), SerializeField]
        public float FGLineEffectShowTime = 5f;
        [Title("FG 启动spin前的等待时间"), FoldoutGroup("FreeGameSetting"), SerializeField]
        public float FGStartSpinPreWaitTime = 1f;
   
        [Title("自动关闭FG UI的时间"), FoldoutGroup("FreeGameSetting"), SerializeField]
        public float AutoCloseFreeSpinUITime = 5f;
      
    }
}