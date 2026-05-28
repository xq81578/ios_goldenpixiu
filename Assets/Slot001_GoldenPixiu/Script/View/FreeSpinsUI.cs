using Sirenix.OdinInspector;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slot001_GoldenPixiu
{
    public class FreeSpinsUI : MonoBehaviour
    {
        [SerializeField, FoldoutGroup("獲得Spin")]
        private GameObject _obtainFreeSpinsPanel;
        [SerializeField, FoldoutGroup("獲得Spin")]
        private SkeletonGraphic _obtainFreeSpinsSpine;
        [SerializeField, FoldoutGroup("獲得Spin")]
        private Button _closeObtainFreeSpinsBtn;
        [SerializeField, FoldoutGroup("獲得Spin")]
        private TextMeshProUGUI _freeSpinCountText;
        [SerializeField, FoldoutGroup("獲得Spin")]
        private GameObject _freeSpintTextGo;
        [SerializeField, FoldoutGroup("獲得Spin")]
        private GameObject _exFreeSpintTextGo;

        [SerializeField]
        private Orientation _orientation;
        public void Init(FreeSpinsUIMediator mediator)
        {
            _closeObtainFreeSpinsBtn.onClick.AddListener(mediator.CloseObtainFreeSpinsPanel);
        }

        public void OpenObtainFreeSpinsPanel(int freeSpinCount, bool isRetrigger = false)
        {
            SetFreeSpinCount(freeSpinCount, isRetrigger);
          
            var aniName =   _orientation == Orientation.Landscape ? "cutscene_in_L" : "cutscene_in_P";
            _obtainFreeSpinsSpine.AnimationState.SetAnimation(0, aniName, false);
            aniName = _orientation == Orientation.Landscape ? "cutscene_loop_L" : "cutscene_loop_P";
            _obtainFreeSpinsSpine.AnimationState.AddAnimation(0, aniName, true, 0);
            _obtainFreeSpinsPanel.SetActive(true);
            
        }

        public void CloseObtainFreeSpinsPanel()
        {
            _obtainFreeSpinsPanel.SetActive(false);
        }

        private void SetFreeSpinCount(int freeSpinCount, bool isRetrigger = false)
        {
            _freeSpinCountText.text = !isRetrigger ? freeSpinCount.ToString() : $"+{freeSpinCount}";
        }
    }
}
