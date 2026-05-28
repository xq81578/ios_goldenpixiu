using UnityEngine;
using CriminalMakers.GameEventHub;
using DG.Tweening;
using TMPro;
using VContainer;
using UnityEngine.UI;
using Slot.Common;

namespace Slot.Common.UI
{
    //處理 BuyType 按鈕與區域的顯示
    public class BuyTypeButtons : MonoBehaviour
    {
        [Inject] private PlatformData _platformData;
        [Inject] private GameStateData _gameStateData;
        [Inject] private PlayerBetData _PlayerBetData;
        [Inject] private CurrencySettingSO _currencySetting;
        [Inject] private GameInfoSO _gameInfoSO;

        /// <summary>
        /// 主畫面 BuyBonus 按鈕區域 包含 ExtraBet
        /// </summary>
        [SerializeField] private CanvasGroup areaCanvasGroup;

        /// 主畫面 BuyBonus 按鈕
        /// </summary>
        [SerializeField] private CanvasGroup buyBonusButtonCanvasGroup;

        [SerializeField] private Button _buyBonusButton;
        [SerializeField] private Button _switchButton;

        /// <summary>
        /// Extra Bet On 按鈕
        /// </summary>
        [SerializeField] GameObject extraBetOnGameObject;

        /// <summary>
        /// Extra Bet Off 按鈕
        /// </summary>
        [SerializeField] GameObject extraBetOffGameObject;

        /// <summary>
        /// Extra Bet 貨幣符號
        /// </summary>
        [SerializeField] TextMeshProUGUI extraBetCurrencyText;

        /// <summary>
        /// Extra Bet 金額
        /// </summary>
        [SerializeField] TextMeshProUGUI extraBetValueText;

        private double _currentBet;
        private bool _isExtraBet = false;

        private void OnEnable()
        {
            GameEventHub.Bind(this);
            _buyBonusButton.onClick.AddListener(OnBuyBonusButton);
            _switchButton.onClick.AddListener(OnSwitchButton);
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
            _buyBonusButton.onClick.RemoveListener(OnBuyBonusButton);
            _switchButton.onClick.RemoveListener(OnSwitchButton);
        }

        private void Awake()
        {
            if (_PlayerBetData == null)
            {
                LogUtils.LogWarning("[BuyTypeButtons] PlayerBetData is not injected!");
                return;
            }

            _PlayerBetData.IsExtraBet = _isExtraBet;
        }

        private void Start()
        {
            UpdateMainUi();
        }

        private void UpdateMainUi()
        {
            //取得現在設定的投注額
            _currentBet = _PlayerBetData.Bet;

            //ExtraBet 顯示
            if (extraBetOnGameObject != null)
            {
                extraBetOnGameObject.SetActive(_isExtraBet);
                extraBetOffGameObject.SetActive(!_isExtraBet);

                double extraBet = _currentBet * (1 + _PlayerBetData.ExtraBetRatio);
                string extraBetStr = ServiceUtils.ToCurrentString(extraBet, Bottom_Define.MoneyFormat);
                //轉string設定到小數點第二位
                extraBetCurrencyText.gameObject.SetActive(!_platformData.IsUFA);
                extraBetCurrencyText.text = _currencySetting.GetCurrencyText(_platformData.CurrencyEnum);
                extraBetValueText.text = $"{extraBetStr}";


                extraBetValueText.alignment = TextAlignmentOptions.MidlineLeft;
            }

            //如果有開ExtraBet, 就不能買FreeGame, 因此將那邊面板關掉
            if (_PlayerBetData.IsExtraBet)
            {
                buyBonusButtonCanvasGroup.alpha = 0.5f;
                buyBonusButtonCanvasGroup.interactable = false;
                buyBonusButtonCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                buyBonusButtonCanvasGroup.alpha = 1f;
                buyBonusButtonCanvasGroup.interactable = true;
                buyBonusButtonCanvasGroup.blocksRaycasts = true;
            }

            //如果已經購買FG了, 這個購買物件要被關掉
            BuyType buyType = _PlayerBetData.BuyType;
            if (buyType == BuyType.BUY_FREE_SPINS || buyType == BuyType.BUY_SUPER_FREE_SPINS)
            {
                areaCanvasGroup.alpha = 0.5f;
                areaCanvasGroup.interactable = false;
                areaCanvasGroup.blocksRaycasts = false;
            }
        }

        private void OnBuyBonusButton()
        {
            new UIBuyBonusClickEvent().Publish(this);
        }

        private void OnSwitchButton()
        {
            new UIExtraBetClickEvent().Publish(this);
            new UIAllCloseEvent().Publish(this);
        }

        //待機狀態
        private void OnSetUiNormal()
        {
            if (!_gameStateData.IsAuto)
            {
                areaCanvasGroup.alpha = 1f;
                areaCanvasGroup.interactable = true;
                areaCanvasGroup.blocksRaycasts = true;
            }

            //2024/10/04新增: Extra Bet介面在尚未進入FG前、轉場時已經消失, 改到Idle時才會做處理
            bool isFG = _gameStateData.IsFreeGame;
            if (isFG)
            {
                areaCanvasGroup.alpha = 0f;
            }
        }

        //Spin 狀態
        private void OnSetUiLock()
        {
            if (areaCanvasGroup.alpha != 0)
                areaCanvasGroup.alpha = 0.5f;
            areaCanvasGroup.interactable = false;
            areaCanvasGroup.blocksRaycasts = false;
        }

        //開啟 ExtraBet
        private void OnExtraBetClick()
        {
            _isExtraBet = !_isExtraBet;
            _PlayerBetData.IsExtraBet = _isExtraBet;

            UpdateMainUi();
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button, checkRepeat: true);

            new GameChangeBetEvent().Publish(this);
        }

        //按下 BuyBonus 按鈕
        private void OnBuyBonusClick()
        {
            buyBonusButtonCanvasGroup.transform.DOScale(0, 0.3f);
        }


        //第一層 - 取消購買
        private void OnBuyBonusCancel()
        {
            buyBonusButtonCanvasGroup.transform.DOScale(1, 0.3f);
        }

        //第二層 - 確認購買
        private void OnBuyFreeSpinsOK()
        {
            if (extraBetOnGameObject != null)
            {
                //購買Free就要把ExtraBet關掉
                _isExtraBet = false;
                _PlayerBetData.IsExtraBet = _isExtraBet;
            }

            buyBonusButtonCanvasGroup.transform.DOScale(1, 0.3f);
        }

        #region Event Listener

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBottomLockEvent(UIBottomLockEvent e)
        {
            OnSetUiLock();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBottomNormalEvent(UIBottomNormalEvent e)
        {
            OnSetUiNormal();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnGameChangeBetEvent(GameChangeBetEvent e)
        {
            UpdateMainUi();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIAutoSpinWindowClickCancelEvent(UIAutoSpinWindowClickCancelEvent e)
        {
            OnSetUiNormal();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIExtraBetClickEvent(UIExtraBetClickEvent e)
        {
            OnExtraBetClick();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBuyBonusClickEvent(UIBuyBonusClickEvent e)
        {
            OnBuyBonusClick();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBuyBonusCancelEvent(UIBuyBonusCancelEvent e)
        {
            OnBuyBonusCancel();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBuyFreeSpinsOkEvent(UIBuyFreeSpinsOkEvent e)
        {
            OnBuyFreeSpinsOK();
        }

        [OnGameEvent]
        private void SwitchPlatformEvent(SetPlatformIdEvent e)
        {
            bool isBuyBonusActive = _gameInfoSO.ClientOptions.IsBuyBonusActive;
            areaCanvasGroup.alpha = isBuyBonusActive ? 1 : 0;
            areaCanvasGroup.blocksRaycasts = isBuyBonusActive;
        }

        #endregion
    }
}