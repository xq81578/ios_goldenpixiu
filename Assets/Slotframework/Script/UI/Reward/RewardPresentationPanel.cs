using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;

namespace Slot.Common.UI
{
    public class RewardPresentationPanel : MonoBehaviour
    {
        [SerializeField]
        private WinCelebrationUI _winCelebrationUI;
        [SerializeField]
        private SettleUI _settleUI;
        [SerializeField]
        private FreeSpinsUI _freeSpinsUI;
        [SerializeField]
        private MaxWinUI _maxWinUI;

        [SerializeField]
        private RewardPresentationUI _activeUI;

        public void SetCloseButtonClickCallback(Action closeCallback)
        {
            _winCelebrationUI.SetCloseButtonClickCallback(closeCallback);
            _settleUI.SetCloseButtonClickCallback(closeCallback);
            _freeSpinsUI.SetCloseButtonClickCallback(closeCallback);
            _maxWinUI.SetCloseButtonClickCallback(closeCallback);
        }

        public void SetUIHideCompleteCallback(Action closeCallback)
        {
            _winCelebrationUI.SetUIHideCompleteCallback(closeCallback);
            _settleUI.SetUIHideCompleteCallback(closeCallback);
            _freeSpinsUI.SetUIHideCompleteCallback(closeCallback);
            _maxWinUI.SetUIHideCompleteCallback(closeCallback);
        }

        public void LoadSpines<T>(SkeletonDataAsset _spine)
        {
            switch (typeof(T).Name)
            {
                case nameof(SettleUI):
                    _settleUI.LoadSpine(_spine);
                    break;
                case nameof(FreeSpinsUI):
                    _freeSpinsUI.LoadSpine(_spine);
                    break;
                case nameof(WinCelebrationUI):
                    _winCelebrationUI.LoadSpine(_spine);
                    break;
                case nameof(MaxWinUI):
                    _maxWinUI.LoadSpine(_spine);
                    break;
            }
        }

        [Button]
        public async UniTask ShowWinCelebration(double bet, double totalWin)
        {
            _activeUI = _winCelebrationUI;
            await _winCelebrationUI.ShowTotalWin(bet, totalWin);
        }

        [Button]
        public async void ShowSettle(double value, bool isBigWin = false)
        {
            _activeUI = _settleUI;
            _settleUI.Show(value, isBigWin).Forget();
        }

        [Button]
        public void ShowFreeSpins(int count, bool isRetrigger = false)
        {
            _activeUI = _freeSpinsUI;
            _freeSpinsUI.Show(count, isRetrigger).Forget();
        }

        [Button]
        public async void ShowMaxWin(double value)
        {
            if (!_maxWinUI.Active)
            {
                ShowSettle(value, true);
                return;
            }

            _activeUI = _maxWinUI;
            await _maxWinUI.Show(value);
        }

        [Button]
        public async void CloseActiveUI()
        {
            if (_activeUI != null)
                await _activeUI.Hide();
        }
    }
}
