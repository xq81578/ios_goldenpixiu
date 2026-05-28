using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slot001_GoldenPixiu
{
    public class SettleUI : MonoBehaviour
    {
        [System.Serializable]
        public struct AnimationPair
        {
            public string AnimationName;
            public string LoopAnimationName;
        }

        [SerializeField]
        private GameObject _freeGameWinScorePanel;
        [SerializeField]
        private TextMeshProUGUI _freeGameTotalWinText;
        [SerializeField]
        private SkeletonGraphic _freeGameWinFxSpine;
        [SerializeField]
        private Button _closeFreeGameUIBtn;
        [SerializeField]
        private Orientation _orientation;
        [SerializeField]
        private AnimationPair _landscapeAnimationData;
        [SerializeField]
        private AnimationPair _portraitAnimationData;

        private bool IsPortrait => _orientation == Orientation.Portrait;
        private AnimationPair AnimationData => IsPortrait ? _portraitAnimationData : _landscapeAnimationData;

        private void Awake()
        {
            // _freeGameWinFxSpine.AnimationState.Data.DefaultMix = 0f;
        }

        public void Init(SettleUIMediator mediator)
        {
            _closeFreeGameUIBtn.onClick.RemoveAllListeners();
            _closeFreeGameUIBtn.onClick.AddListener(mediator.CloseSettleTotalWin);
        }

        public void ShowSettleTotalWin(double totalWin)
        {
            _freeGameTotalWinText.text = totalWin.ToString("#,##0.00");
            var state = _freeGameWinFxSpine.AnimationState;
            state.ClearTrack(0);
            state.SetAnimation(0, "spinswin_in", false);
            state.AddAnimation(0, "spinswin_loop", true, 0);
            SetUIVisibility(true);
        }

        public void HideSettleTotalWin()
        {
            SetUIVisibility(false);
        }

        private void SetUIVisibility(bool isVisible)
        {
            _freeGameWinScorePanel.SetActive(isVisible);
            _freeGameWinFxSpine.gameObject.SetActive(isVisible);
        }
    }
}