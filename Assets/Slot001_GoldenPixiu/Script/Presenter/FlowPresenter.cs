using System.Collections.Generic;
using System.Threading.Tasks;
using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using Slot.Common;
using Slot.Common.Bottom;

namespace Slot001_GoldenPixiu
{
    public class FlowPresenter
    {
        private readonly ReelControllerMediator _reelController;
        private readonly GamePlayUIMediator _gamePlayUI;
        private readonly FreeSpinsUIMediator _freeSpinsUI;
        private readonly WinCelebrationUIMediator _winCelebrationUI;
        private readonly SettleUIMediator _settleUI;

        public FlowPresenter(
            ReelControllerMediator reelController,
            GamePlayUIMediator gamePlayUI,
            FreeSpinsUIMediator freeSpinsUI,
            WinCelebrationUIMediator winCelebrationUI,
            SettleUIMediator settleUI
            )
        {
            _reelController = reelController;
            _gamePlayUI = gamePlayUI;
            _freeSpinsUI = freeSpinsUI;
            _winCelebrationUI = winCelebrationUI;
            _settleUI = settleUI;
        }

        public async UniTask PlayClearEffect( double boardWin,  double addWin)
        {
            // 注意由於控制流程表演，要確定確實完成Clear才能切換到下一步狀態
            // 表演計分動畫
            await ShowWinTemp(boardWin);
            // 表演分數增加到總分的動畫
            AddBottomWin(addWin);
        
        }

        public async UniTask ShowWinTemp(double win)
        {
            new SetWinTempEvent(win).Publish(this);

            _gamePlayUI.PlayCharacterMultipler();
            await UniTask.Delay(800);
        }

        private async UniTask SettleWinTemp(int multiplier, Bottom_MathType mathType)
        {
            if (multiplier <= 1) return;

            var tcs = new UniTaskCompletionSource();
            void OnSettleWinTempEnd(SettleWinTempEndEvent evt) => tcs.TrySetResult();
            var OnSettleEndListener = GameEventHub.Listen<SettleWinTempEndEvent>(this, OnSettleWinTempEnd);
            new SettleWinTempEvent(multiplier, mathType).Publish(this);
            try
            {
                await tcs.Task.Timeout(System.TimeSpan.FromSeconds(5));
            }
            finally
            {
                OnSettleEndListener();
            }
        }

        
        public void PlayPXNomormalAnimation()
        {
            _gamePlayUI.PlayPXNomormalAnimation();
        }

        public void AddBottomWin(double addWin)
        {
            new UIAddWinEvent(addWin).Publish(this);
            AudioManager.PlayEffectByName("se_regularwin");
        }

        public void ShowFreeGameImageBg(bool isShow=false)
        {
           _gamePlayUI.ShowFreeGameImageBg(isShow);
        }

        public void ShowAniLightPanel(bool isShow=false)
        {
           _gamePlayUI.ShowAniLightPanel(isShow); 
        }

        public async UniTask ChangeSjSlider(float value = -1f)
        {
           await _gamePlayUI.ChangeSjSlider(value);
        }

        public async UniTask PlayFreeGameIntro()
        {
            AudioManager.PlayEffectByName("se_scatter_ring");
            // await UniTask.Delay((int)(2.3f * 1000));
            // _reelController.PlayScatterWinFx();
            await UniTask.Delay((int)(2.3f * 1000));
            await _gamePlayUI.PlayFreeGameEntry();
        }

        public async UniTask OpenObtainFreeSpinsPanel(int freeSpinCount, bool isRetrigger = false)
        {
            AudioManager.PlayOneTrackByName("mu_trans_background");
            _freeSpinsUI.OpenObtainFreeSpinsPanel(freeSpinCount, isRetrigger);
            var task = UniTask.Delay((int)(5f * 1000));
            await UniTask.WaitUntil(() => !_freeSpinsUI.IsOpening || task.Status == UniTaskStatus.Succeeded);
            _freeSpinsUI.CloseObtainFreeSpinsPanel();
            AudioManager.PlayOneTrackByName("mu_free_background");
        }

        public async UniTask PlayFreeGameShow()
        {
            var tasks = _gamePlayUI.PlayFreeGameShow();
            // AudioManager.PlayEffectByName("se_loading", checkRepeat: true);
            await UniTask.Delay(1250);
            _gamePlayUI.TransGamePlayUI(isFreeGame: true);
            await UniTask.Delay(1300);
            await UniTask.WhenAll(tasks);
        }

        public void ExitFreeGame()
        {
            _gamePlayUI.TransGamePlayUI(isFreeGame: false);
            _gamePlayUI.ResetWinText();
        }

        public async Task ShowTotalWin(float bet, double spinWin)
        {
            
            await _winCelebrationUI.ShowTotalWin(bet, spinWin);
        }

        public void PlarPXWinAnimation(float bet, double spinWin)
        {
            _gamePlayUI.PlarPXWinAnimation(bet, spinWin);
        }
        

        public async Task ShowSettleTotalWin(double totalWin)
        {
            await UniTask.Delay(500);
            AudioManager.PlayOneTrackByName("mu_congrats");
            await _settleUI.ShowSettleTotalWin(totalWin);
            AudioManager.PlayOneTrackByName("mu_main_background");
        }

    
    }
}