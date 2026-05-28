using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Slot001_GoldenPixiu
{
    public class SettleUIMediator : UIOrientationMediator<SettleUI>
    {
        [SerializeField]
        private string _openFreeGameSummarySound = "se_congrats";
        [SerializeField]
        private float _showTotalWinTime = 5f;

        private CancellationTokenSource _cts;

        protected override void Initialize()
        {
            InvokeAllUIs(ui => ui.Init(this));
        }

        [Button]
        public async UniTask ShowSettleTotalWin(double totalWin)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            InvokeAllUIs(ui => ui.ShowSettleTotalWin(totalWin));
            AudioManager.PlayEffectByName(_openFreeGameSummarySound);

            var delayTask = UniTask.Delay((int)(_showTotalWinTime * 1000), cancellationToken: _cts.Token).SuppressCancellationThrow();

            await delayTask;

            HideSettleTotalWin();
        }

        public void CloseSettleTotalWin()
        {
            _cts?.Cancel();
        }

        private void HideSettleTotalWin()
        {
            InvokeAllUIs(ui => ui.HideSettleTotalWin());
        }
    }
}