using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using VContainer.Unity;
using UnityEngine.InputSystem;

namespace Slot.Common.UI.Mediator
{
    public class BottomBarUIMediator : UIOrientationMediator<BottomBarUI>
    {
        [Inject] private GameStateData _gameStateData;
        [Inject] private PlayerBetData _playerBetData;
        [Inject] private PlatformData _platformData;
        [Inject] private GameInfoSO _gameInfoSO;

        [Space(10)] [Header("客製化相關")] [SerializeField]
        private Sprite _idleSpinSprite;

        [SerializeField] private Sprite _stopSpinSprite;

        [SerializeField, Header("按鈕開場就在, 不支援延遲加載")]
        private SkeletonDataAsset _spinAssetData = null;

        [SerializeField] private string _spinIdleAnimName = "idle";
        [SerializeField] private string _spinStopAnimName = "autoSpin";
        [SerializeField] private string _spinClickAnimName = "click";

        [Header("spin動畫")] [SerializeField] private AssetReference spinSpineAssetData;

        [SerializeField] private SpineAnimation _splashAnimation;

        [SerializeField] private Sprite _freeGameSpinSprite;
        [SerializeField] private AssetReference _winSpinAssetReference;
        [SerializeField] private string _winSpinAnimationName;
        [SerializeField] private Sprite _winPanelBgSprite;

        [Header("直板 Win 計分版 pos y: 預設為 -330，" +
                "🔴 數值不等於 -330 才會觸發變動 🔴")]
        [SerializeField]
        private float _portraitWinPosY = Default_PortraitWinPosY; // prefab 預設值

        private bool _isSpin = false;
        private double _winValue = 0.0;
        private DateTime _startTime;
        private DateTime _spinTime;

        private Coroutine _playTimeCoroutine;
        private StringBuilder _playTimeBuilder = new();
        private const float Default_PortraitWinPosY = -330f;
        private float _nextSpaceSpinTime = float.NegativeInfinity;


        protected override void Initialize()
        {
            if (_gameInfoSO == null)
            {
                var scope = LifetimeScope.Find<BootLifeTimeScope>();
                if (scope != null)
                    scope.Container.Inject(this);
            }

            InitializeUIAsync();
        }

        private void Update()
        {
            if (!Input.GetKey(KeyCode.Space))
                return;
            if (_gameInfoSO == null || !_gameInfoSO.IsInit)
                return;
            if (Time.time >= _nextSpaceSpinTime)
            {
                if (_gameStateData.IsSpinLock == false)
                {
                    _nextSpaceSpinTime = Time.time + 0.2f;
                    new SpinTriggerEvent().Publish(this);
                }
            }
        }

        private void OnDestroy()
        {
            GameEventHub.Unbind(this);
            _winSpinAssetReference.ReleaseAsset();
            if (_playTimeCoroutine != null)
                StopCoroutine(_playTimeCoroutine);
        }

        private async void InitializeUIAsync()
        {
            if (_gameInfoSO == null)
                return;
            await UniTask.WaitUntil(() => _gameInfoSO.IsInit);
            await UniTask.WaitForEndOfFrame();
            await SetBottomBarUI(_gameInfoSO.ClientOptions.Platform);
            GameEventHub.Bind(this);
        }

        private async UniTask SetBottomBarUI(PlatformType platformType)
        {
            await InvokeAllUIs(ui => ui.SetButtonBarView(platformType));
            await UniTask.WaitForEndOfFrame();

            InvokeAllUIs(ui =>
            {
                ui.ActiveBottomBarView();
                ui.InitNetText(_platformData.CurrencyEnum);
                ui.SetSpinSprite(_idleSpinSprite, _stopSpinSprite, _freeGameSpinSprite);

                if (_spinAssetData != null)
                {
                    ui.SetSpinSplineAsset(_spinAssetData, _spinIdleAnimName, _spinStopAnimName, _spinClickAnimName);
                }
            });

            OnGameChangeBalanceEvent(null);
            OnGameChangeBetEvent(null);
            SetPortraitWinPosY();
            SetWinSpine();
            SetWinBgImage();
            SetAutoTurboActive();
        }

        private void UpdateFreeGameCount()
        {
            int roundIndex = _gameStateData.FreeGameRoundIndex == -1 ? 0 : _gameStateData.FreeGameRoundIndex;
            int fgSpinCount = _gameStateData.FreeGameSpinTempCount - roundIndex;
            InvokeAllUIs(ui => ui.UpdateFreeGameCount(fgSpinCount));
        }

        private void OnSetWin(double setWin, bool isAdd)
        {
            new HideWinTempEvent().Publish(this);

            double currentWin = _winValue;
            double targetWin = setWin;
            _winValue = targetWin;

            bool isTargetWin = targetWin > 0;
            bool hasWinValue = _winValue > 0;

            if (isTargetWin)
            {
                double maxWinValue = _playerBetData.Bet * _gameInfoSO.MaxWin;
                if (targetWin > maxWinValue && maxWinValue > 0)
                {
                    targetWin = maxWinValue;
                }
            }

            InvokeAllUIs(ui =>
            {
                ui.OnSetWin(currentWin, targetWin, isAdd);
                ui.ShowMarquee(!isTargetWin, hasWinValue);
            });

            LogUtils.Log($"[BottomBarManager] OnSetWin: {currentWin} -> {targetWin}");
        }

        private void OnClickBtn()
        {
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
        }

        private void SetPortraitWinPosY()
        {
            if (_portraitUI != null && _portraitWinPosY != Default_PortraitWinPosY)
            {
                _portraitUI.SetWinPosY(_portraitWinPosY);
            }
        }

        private async void SetWinSpine()
        {
            if (!_winSpinAssetReference.RuntimeKeyIsValid())
                return;

            if (_winSpinAssetReference.IsDone && _winSpinAssetReference.IsValid())
            {
                var skeletonDataAsset = _winSpinAssetReference.Asset as SkeletonDataAsset;
                if (skeletonDataAsset != null)
                {
                    InvokeAllUIs(ui => ui.SetWinSpine(skeletonDataAsset, _winSpinAnimationName));
                }
            }
            else
            {
                var handle = _winSpinAssetReference.LoadAssetAsync<SkeletonDataAsset>();
                await handle.Task;
                if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    var skeletonDataAsset = handle.Result;
                    if (skeletonDataAsset != null)
                    {
                        InvokeAllUIs(ui => ui.SetWinSpine(skeletonDataAsset, _winSpinAnimationName));
                    }
                }
            }
        }

        private void SetWinBgImage()
        {
            if (_winPanelBgSprite == null)
                return;
            InvokeAllUIs(ui => ui.SetWinBgImage(_winPanelBgSprite));
        }

        private void SetAutoTurboActive()
        {
            InvokeAllUIs(ui =>
                ui.SetAutoTurboActive(_gameInfoSO.ClientOptions.IsAutoActive, _gameInfoSO.ClientOptions.IsTurboActive));
        }

        private bool CheckChangeCurrency(PlatformType viewType)
        {
            // 目前只有 combo 平台需要換幣，之後有其他平台的話需要再擴
            return _platformData.Id == (int)PlatformType.COMBO &&
                   viewType == PlatformType.COMBO;
        }

        private IEnumerator UpdatePlayTimeRoutine()
        {
            var waitForSeconds = new WaitForSeconds(1f);

            while (true)
            {
                yield return waitForSeconds;

                TimeSpan elapsed = DateTime.Now - _startTime;

                // 重複使用 StringBuilder，避免字串分配
                _playTimeBuilder.Clear();
                if (elapsed.Hours > 0)
                {
                    // 超過 1 小時：顯示 HH:mm:ss 格式
                    _playTimeBuilder.Append(elapsed.Hours.ToString("D2"));
                    _playTimeBuilder.Append(':');
                }

                // 未滿 1 小時：顯示 mm:ss 格式
                _playTimeBuilder.Append(elapsed.Minutes.ToString("D2"));
                _playTimeBuilder.Append(':');
                _playTimeBuilder.Append(elapsed.Seconds.ToString("D2"));

                string playTime = _playTimeBuilder.ToString();
                InvokeAllUIs(ui => ui.SetPlayTimeText(playTime));
            }
        }

        #region Event Listener

        [OnGameEvent(SubscriberPriority.High)]
        private void SpinTriggerEvent(SpinTriggerEvent e)
        {
            InvokeAllUIs(ui => ui.OnSpinClick());

            if (_isSpin)
                return;

            _isSpin = true;
            InvokeAllUIs(ui => ui.OnUpdateTransText(string.Empty));
            LogUtils.Log("[BottomBarUIMediator] SpinTriggerEvent triggered");
        }

        [OnGameEvent]
        private void OnUIBottomNormalEvent(UIBottomNormalEvent e)
        {
            _isSpin = false;

            InvokeAllUIs(ui => ui.OnSetBottomUiNormal(
                _gameStateData.IsAuto, _gameStateData.IsFreeGame));

            OnUIAutoSpinCountEvent(null);
            LogUtils.Log("[BottomBarUIMediator] OnUIBottomNormalEvent triggered");
        }

        [OnGameEvent]
        private void OnUIBottomLockEvent(UIBottomLockEvent e)
        {
            _isSpin = true;
            bool isAuto = _gameStateData.IsAuto;
            InvokeAllUIs(ui => ui.OnSetBottomUiLock(isAuto));

            if (_gameStateData.IsFreeGame)
            {
                UpdateFreeGameCount();
            }

            LogUtils.Log("[BottomBarUIMediator] OnUIBottomLockEvent triggered");
        }

        [OnGameEvent]
        private void OnFreeGameEnterEvent(FreeGameEnterEvent e)
        {
            _gameStateData.IsFreeGame = true;
            InvokeAllUIs(ui => ui.SpinFGGameObject.SetActive(true));
            UpdateFreeGameCount();
            LogUtils.Log("[BottomBarUIMediator] OnFreeGameEnterEvent triggered");
        }

        [OnGameEvent]
        private void OnFreeGameLeaveEvent(FreeGameLeaveEvent e)
        {
            _gameStateData.IsFreeGame = false;
            InvokeAllUIs(ui => ui.SpinFGGameObject.SetActive(false));
            LogUtils.Log("[BottomBarUIMediator] OnFreeGameLeaveEvent triggered");
        }

        [OnGameEvent]
        private void OnUISetTransTextEvent(UISetTransTextEvent e)
        {
            InvokeAllUIs(ui => ui.OnUpdateTransText(e.TransText));
            LogUtils.Log($"[BottomBarUIMediator] OnUISetTransTextEvent triggered - TransText: {e.TransText}");
        }

        [OnGameEvent]
        private void OnGameChangeTurboEvent(GameChangeTurboEvent e)
        {
            _gameStateData.TurboType = e.TurboTypeEnum;
            InvokeAllUIs(ui => ui.SetTurboSprite(_gameStateData.TurboType));
            LogUtils.Log(
                $"[BottomBarUIMediator] OnGameChangeTurboEvent triggered - TurboType: {_gameStateData.TurboType}");
        }

        [OnGameEvent]
        private void OnUISetWinEvent(UISetWinEvent e)
        {
            OnSetWin(e.SetWin, false);
            LogUtils.Log($"[BottomBarUIMediator] OnUISetWinEvent triggered - SetWin: {e.SetWin}");
        }

        [OnGameEvent]
        private async void OnUIAddWinEvent(UIAddWinEvent e)
        {
            if (e.AddWin <= 0)
                return;

            double targetWin = _winValue + e.AddWin;
            OnSetWin(targetWin, true);
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Regularwin);
            var playWinTasks = InvokeAllUIs(ui => ui.PlayWinAnimation());
            await UniTask.WhenAll(playWinTasks);
            new UIAddWinEndEvent().Publish(this);
            LogUtils.Log($"[BottomBarUIMediator] OnUIAddWinEvent triggered - AddWin: {e.AddWin}");
        }

        [OnGameEvent]
        private void OnUIAutoSpinCountEvent(UIAutoSpinCountEvent e)
        {
            InvokeAllUIs(ui =>
            {
                ui.UpdateAutoCount(_gameStateData.IsAuto
                    , _gameStateData.IsFreeGame
                    , _gameStateData.AutoSpinTotalSpins);
                ui.SetTurboSprite(_gameStateData.TurboType);
            });
            LogUtils.Log($"[BottomBarUIMediator] OnUIAutoSpinCountEvent triggered - IsAuto: {_gameStateData.IsAuto}");
        }

        [OnGameEvent]
        private void OnUITurboClickEvent(UITurboClickEvent e)
        {
            _gameStateData.TurboType = _gameStateData.TurboType == Bottom_TurboType.TurboOff
                ? Bottom_TurboType.TurboQuick
                : Bottom_TurboType.TurboOff;
            InvokeAllUIs(ui => ui.SetTurboSprite(_gameStateData.TurboType));
            LogUtils.Log(
                $"[BottomBarUIMediator] OnUITurboClickEvent triggered - TurboType: {_gameStateData.TurboType}");
        }

        [OnGameEvent]
        private void OnUIAutoSpinWindowClickCancelEvent(UIAutoSpinWindowClickCancelEvent e)
        {
            _gameStateData.IsAuto = false;
            InvokeAllUIs(ui => ui.SetAutoSpinStop());
            LogUtils.Log("[BottomBarUIMediator] OnUIAutoSpinWindowClickCancelEvent triggered");
        }

        [OnGameEvent]
        private void OnUIAutoSpinStopClickEvent(UIAutoSpinStopClickEvent e)
        {
            OnUIAutoSpinWindowClickCancelEvent(null);
            LogUtils.Log("[BottomBarUIMediator] OnUIAutoSpinStopClickEvent triggered");
        }

        [OnGameEvent]
        private void OnGameChangeBetEvent(GameChangeBetEvent e)
        {
            double bet = _playerBetData.Bet;
            bool isExtraBet = _playerBetData.IsExtraBet;
            if (isExtraBet)
            {
                double extraBetRatio = _playerBetData.ExtraBetRatio;
                bet *= 1 + extraBetRatio;
            }

            string betStr = ServiceUtils.ToCurrentString(bet, Bottom_Define.MoneyFormat);
            InvokeAllUIs(ui => ui.OnChangeBet(betStr));

            LogUtils.Log($"[BottomBarManager] OnChangeBet - Bet: {bet}, IsExtraBet: {isExtraBet}");
        }

        [OnGameEvent]
        private void OnGameChangeBalanceEvent(GameChangeBalanceEvent e)
        {
            double balance = _playerBetData.Balance;
            ECurrency currency = _platformData.CurrencyEnum;
            InvokeAllUIs(ui => ui.OnChangeBalance(balance, currency, CheckChangeCurrency(ui.PlatformType)));
            LogUtils.Log("OnChangeBalance balance:" + balance);
            LogUtils.Log($"[BottomBarUIMediator] OnGameChangeBalanceEvent triggered - Balance: {balance}");

            if (e != null && e.ShowNetWin)
            {
                InvokeAllUIs(ui => ui.SetNetText(_playerBetData.NetWin));
            }
        }

        [OnGameEvent]
        private void SwitchPlatformEvent(SetPlatformIdEvent e)
        {
            if (e.PlatformType == PlatformType.NONE)
                return;
            SetBottomBarUI(e.PlatformType).Forget();
        }

        [OnGameEvent]
        private void OnSetStartTimeEvent(UILoadingContinueClickEvent e)
        {
            _startTime = DateTime.Now;

            if (_playTimeCoroutine != null)
                StopCoroutine(_playTimeCoroutine);

            _playTimeCoroutine = StartCoroutine(UpdatePlayTimeRoutine());
        }

        [OnGameEvent]
        private void OnGameServerReadyEvent(GameServiceReadyEvent e)
        {
            InvokeAllUIs(ui => ui.InitNetText(_platformData.CurrencyEnum));
        }

        [OnGameEvent]
        private void OnUIAutoClickEvent(UIAutoClickEvent e)
        {
            OnClickBtn();
            LogUtils.Log("[BottomBarUIMediator] OnUIAutoClickEvent triggered");
        }

        [OnGameEvent]
        private void OnUIBetClickEvent(UIBetClickEvent e)
        {
            OnClickBtn();
            LogUtils.Log("[BottomBarUIMediator] OnUIBetClickEvent triggered");
        }

        [OnGameEvent]
        private void OnUITurboClickBtnEvent(UITurboClickEvent e)
        {
            OnClickBtn();
            LogUtils.Log("[BottomBarUIMediator] OnUITurboClickBtnEvent triggered");
        }

        [OnGameEvent]
        private void OnUIWalletClickEvent(UIWalletClickEvent e)
        {
            OnClickBtn();
            LogUtils.Log("[BottomBarUIMediator] OnUIWalletClickEvent triggered");
        }

        [OnGameEvent]
        private void OnUIMenuClickEvent(UIMenuClickEvent e)
        {
            OnClickBtn();
            LogUtils.Log("[BottomBarUIMediator] OnUIMenuClickEvent triggered");
        }

        [OnGameEvent]
        private void OnInfoUIActiveEvent(InfoUIActiveEvent e)
        {
            OnClickBtn();
        }

        [OnGameEvent]
        private void OnSpinTriggerEvent(SpinTriggerEvent e)
        {
            _spinTime = DateTime.Now;
        }

        #endregion
    }
}