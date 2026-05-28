using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Slot.Common.UI
{
    public class SettleUI : RewardPresentationUI
    {
        [SerializeField]
        protected Image _congratulationsImage;
        [SerializeField]
        protected Image _featureCompleteImage;
        [SerializeField]
        protected Image _youHaveWonImage;

        public virtual async UniTask Show(double value, bool isBigWin = false)
        {
            _congratulationsImage.enabled = isBigWin;
            _featureCompleteImage.enabled = !isBigWin;
            await base.Show(value);
        }
    }
}
