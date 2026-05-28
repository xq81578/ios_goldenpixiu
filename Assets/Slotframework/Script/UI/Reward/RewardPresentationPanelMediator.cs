using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Slot.Common.UI.Mediator
{
    public class RewardPresentationPanelMediator : UIOrientationMediator<RewardPresentationPanel>
    {
        [Header("動態加載資源")]
        [SerializeField]
        private AssetReference _settleSpineAssetReference;
        [SerializeField]
        private AssetReference _freeSpinsSpineAssetReference;
        [SerializeField]
        private AssetReference _winCelebrationSpineAssetReference;
        [SerializeField]
        private AssetReference _maxWinSpineAssetReference;

        [Header("Win Celebration")]
        [SerializeField]
        private int _winCelebrationBaseRatio = 10;
        [SerializeField]
        private string _scoringSoundName = "bw_scoring";
        [SerializeField]
        private string _endSoundName = "bw_end";

        [Header("Free Spins")]
        [SerializeField]
        private string _freeSpinsOpenSoundName;
        [SerializeField]
        private string _freeSpinsOpenBGMName;

        [Header("Settle")]
        [SerializeField]
        private string _settleSpinsOpenSoundName;
        [SerializeField]
        private string _settleSpinsOpenBGMName;

        [Header("Max Win")]
        [SerializeField]
        private string _maxWinConfirmSoundName;
        [SerializeField]
        private string _maxWinConfirmBGMName;

        [Header("Close Settings")]
        [SerializeField]
        private string _resolvedResumeBGM = "";
        [SerializeField]
        private float _autoCloseTime = 5f;
        [SerializeField]
        private float _autoCloseTimeCounter = 0f;
        private int _closeCallbackCount = 0;

        private void OnDestroy()
        {
            _settleSpineAssetReference.ReleaseAsset();
            _freeSpinsSpineAssetReference.ReleaseAsset();
            _winCelebrationSpineAssetReference.ReleaseAsset();
            _maxWinSpineAssetReference.ReleaseAsset();
        }

        protected override void Initialize()
        {
            _closeCallbackCount = 0;
            _autoCloseTimeCounter = 0;

            InvokeAllUIs(ui => ui.SetCloseButtonClickCallback(OnClose));
            InvokeAllUIs(ui => ui.SetUIHideCompleteCallback(() => _closeCallbackCount++));
            LoadSpines();
        }

        /// <summary>
        /// 報獎
        /// </summary>
        /// <param name="bet"></param>
        /// <param name="totalWin"></param>
        /// <returns></returns>
        [Button]
        public async UniTask ShowWinCelebration(double bet, double totalWin, bool autoClose = true)
        {
            if (totalWin == 0 || bet == 0 || totalWin / bet < _winCelebrationBaseRatio)
                return;

            string bgmName = AudioManager.currentMusicName;
            AudioManager.StopBGM();
            AudioManager.PlayEffectByName(_scoringSoundName, loop: true);
            await InvokeAllUIs(ui => ui.ShowWinCelebration(bet, totalWin));
            AudioManager.StopEffectByName(_scoringSoundName);
            AudioManager.PlayEffectByName(_endSoundName);
            if (autoClose)
                AutoCloseTrigger();
            await UIClose();
            AudioManager.StopEffectByName(_endSoundName);
            AudioManager.PlayOneTrackByName(bgmName, true);
        }

        /// <summary>
        /// 免費旋轉次數
        /// </summary>
        /// <param name="count"></param>
        /// <param name="isRetrigger"></param>
        /// <returns></returns>
        [Button]
        public async UniTask ShowFreeSpins(int count, bool isRetrigger = false, string resumeBGM = "")
        {
            PlayAudio(_freeSpinsOpenSoundName, _freeSpinsOpenBGMName, resumeBGM);
            InvokeAllUIs(ui => ui.ShowFreeSpins(count, isRetrigger));
            AutoCloseTrigger();
            await UIClose();
            ResumeBGM();
        }

        /// <summary>
        /// 結算畫面
        /// </summary>
        /// <param name="value"></param>
        /// <param name="isBigWin"></param>
        /// <param name="autoClose"></param>
        /// <returns></returns>
        [Button]
        public async UniTask ShowSettle(double value, bool isBigWin = false, bool autoClose = true, string resumeBGM = "")
        {
            PlayAudio(_settleSpinsOpenSoundName, _settleSpinsOpenBGMName, resumeBGM);
            InvokeAllUIs(ui => ui.ShowSettle(value, isBigWin));

            if (autoClose)
                AutoCloseTrigger();

            await UIClose();
            ResumeBGM();
        }

        /// <summary>
        /// Max Win 流程
        /// </summary>
        /// <param name="totalWin"></param>
        /// <param name="maxWinRatio"></param>
        /// <returns></returns>
        [Button]
        public async UniTask ShowMaxWin(double totalWin, int maxWinRatio, string resumeBGM = "")
        {
            var tcs = new UniTaskCompletionSource();
            string titleString = await LocalizationSettings.StringDatabase.GetLocalizedStringAsync(CommonDefine.DialogTableName, CommonDefine.DialogKey_SystemTitle);
            string maxWinRatioString = maxWinRatio.ToString("N0");
            string messageString = await LocalizationSettings.StringDatabase.GetLocalizedStringAsync(CommonDefine.DialogTableName, CommonDefine.DialogKey_MaxWin, arguments: new object[] { maxWinRatioString });
            string confirmString = await LocalizationSettings.StringDatabase.GetLocalizedStringAsync(CommonDefine.DialogTableName, CommonDefine.DialogKey_Confirm);

            DialogMediator.ShowDialog(titleString, messageString
                , new ActionButton(confirmString, async () =>
                {
                    PlayAudio(_maxWinConfirmSoundName, _maxWinConfirmBGMName, resumeBGM);
                    InvokeAllUIs(ui => ui.ShowMaxWin(totalWin));
                    await UIClose();
                    tcs.TrySetResult();
                }), false);

            await tcs.Task;
            ResumeBGM();
        }

        private void LoadSpines()
        {
            TryLoadSpine(_settleSpineAssetReference, spine => InvokeAllUIs(ui => ui.LoadSpines<SettleUI>(spine)));
            TryLoadSpine(_freeSpinsSpineAssetReference, spine => InvokeAllUIs(ui => ui.LoadSpines<FreeSpinsUI>(spine)));
            TryLoadSpine(_winCelebrationSpineAssetReference, spine => InvokeAllUIs(ui => ui.LoadSpines<WinCelebrationUI>(spine)));
            TryLoadSpine(_maxWinSpineAssetReference, spine => InvokeAllUIs(ui => ui.LoadSpines<MaxWinUI>(spine)));
        }

        private void TryLoadSpine(AssetReference reference, Action<SkeletonDataAsset> onLoaded)
        {
            if (!reference.RuntimeKeyIsValid())
                return;

            reference.LoadAssetAsync<SkeletonDataAsset>().Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    onLoaded?.Invoke(handle.Result);
            };
        }

        private async UniTask UIClose()
        {
            int uiCount = AllUIs.Count;
            await UniTask.WaitUntil(() => _closeCallbackCount >= uiCount);
            _closeCallbackCount = 0;
        }

        private async void AutoCloseTrigger()
        {
            _autoCloseTimeCounter = 0f;
            while (_autoCloseTimeCounter < _autoCloseTime && _closeCallbackCount == 0)
            {
                await UniTask.Yield();
                _autoCloseTimeCounter += Time.deltaTime;
            }

            if (_closeCallbackCount == 0)
                OnClose();
        }

        private void OnClose()
        {
            InvokeAllUIs(ui => ui.CloseActiveUI());
        }

        private void PlayAudio(string effectSound, string bgSound, string resumeBGM)
        {
            if (!string.IsNullOrEmpty(effectSound))
                AudioManager.PlayEffectByName(effectSound);
            if (!string.IsNullOrEmpty(bgSound))
            {
                _resolvedResumeBGM = string.IsNullOrEmpty(resumeBGM) ? AudioManager.currentMusicName : resumeBGM;
                AudioManager.PlayOneTrackByName(bgSound, true);
            }
        }

        private void ResumeBGM()
        {
            if (!string.IsNullOrEmpty(_resolvedResumeBGM))
            {
                AudioManager.PlayOneTrackByName(_resolvedResumeBGM, true);
                _resolvedResumeBGM = string.Empty;
            }
        }
    }
}