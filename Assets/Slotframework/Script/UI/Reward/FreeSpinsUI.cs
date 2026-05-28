using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Slot.Common.UI
{
    public class FreeSpinsUI : RewardPresentationUI
    {
        [SerializeField]
        private Image _youHaveWonImage;
        [SerializeField]
        private Image _freeSpinsImage;

        public virtual async UniTask Show(int count, bool isRetrigger = false)
        {
            string valueText = isRetrigger ? $"+{count}" : count.ToString();
            await base.Show(valueText);
        }
    }
}
