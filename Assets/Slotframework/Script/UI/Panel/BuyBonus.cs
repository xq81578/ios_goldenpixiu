using System;
using CriminalMakers.GameEventHub;
using Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Slot.Common.UI
{
    public class BuyBonus : MonoBehaviour
    {
        private class UIBetMinusClickEvent : GameEvent { }
        private class UIBetPlusClickEvent : GameEvent { }
        private class UIBuyBonusNormalClickEvent : GameEvent { }
        private class UIBuyBonusSuperClickEvent : GameEvent { }

        [Inject]
        private PlayerBetData _playerBetData;
        [Inject]
        private PlatformData _platformData;
        [Inject]
        private GameInfoSO _gameInfo;
        [Inject]
        private CurrencySettingSO currencySetting;
        //取得 buyType 設定
        [Inject]
        private PayTableSO payTable;

        [SerializeField]
        private UIPanelAnimator _uiPanelAnimator;

        [Space(10)]
        [SerializeField]
        private Button _maskButton;
        [SerializeField]
        private Button _closeButton;
        [SerializeField]
        private Button _minusButton;
        [SerializeField]
        private Button _plusButton;
        [SerializeField]
        private Button _normalBuyButton;
        [SerializeField]
        private Button _superBuyButton;
        [SerializeField]
        private Button _startButton;
        [SerializeField]
        private Button _cancelButton;

        [Space(10)]
        /// <summary>
        /// BuyBonus 動態介面
        /// </summary>
        [SerializeField]
        private CanvasGroup buyBonusCanvasGroup;
        /// <summary>
        /// BuyBonus 第一層介面 各種選項
        /// </summary>
        [SerializeField]
        private CanvasGroup buyStep1BoardCanvasGroup;
        /// <summary>
        /// BuyBonus 第二層介面 確認要買的內容
        /// </summary>
        [SerializeField]
        private CanvasGroup buyStep2BoardCanvasGroup;

        //第一層 投注
        [SerializeField]
        private TextMeshProUGUI betCurrencyText;
        [SerializeField]
        private TextMeshProUGUI betValueText;
        [SerializeField]
        private Button betMinusButton;
        [SerializeField]
        private Button betPlusButton;

        //第一層 buyFreeSpins
        [SerializeField]
        private Image buyFreeCurrencyImage;
        [SerializeField]
        private TextMeshProUGUI buyFreeCurrencyText;
        [SerializeField]
        private TextMeshProUGUI buyFreeValueText;
    
        //第一層 buySuperFreeSpins
        [SerializeField]
        private Image buySuperCurrencyImage;
        [SerializeField]
        private TextMeshProUGUI buySuperCurrencyText;
        [SerializeField]
        private TextMeshProUGUI buySuperValueText;


        //第二層 確認內容
        [SerializeField]
        private GameObject freeTextImageGameObject;
        [SerializeField]
        private GameObject superFreeTextImageGameObject;
        [SerializeField]
        private Image buyCurrencyImage;
        [SerializeField]
        private TextMeshProUGUI buyCurrencyText;
        [SerializeField]
        private TextMeshProUGUI buyValueText;
        [SerializeField]
        private Button buyButton;

        [SerializeField]
        private GridLayoutGroup layoutGroup;

        private BuyType tempBuyType = BuyType.BUY_NONE;
        private double currentBet;
        private double[] betList;

        private void OnEnable()
        {
            GameEventHub.Bind(this);
            _maskButton.onClick.AddListener(OnCloseButtonClick);
            _closeButton.onClick.AddListener(OnCloseButtonClick);
            _minusButton.onClick.AddListener(OnMinusButtonClick);
            _plusButton.onClick.AddListener(OnPlusButtonClick);
            _normalBuyButton.onClick.AddListener(OnBuyNormalButtonClick);
            _superBuyButton.onClick.AddListener(OnBuySuperButtonClick);
            _startButton.onClick.AddListener(OnStartButtonClick);
            _cancelButton.onClick.AddListener(OnCancelButtonClick);
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
            _maskButton.onClick.RemoveListener(OnCloseButtonClick);
            _closeButton.onClick.RemoveListener(OnCloseButtonClick);
            _minusButton.onClick.RemoveListener(OnMinusButtonClick);
            _plusButton.onClick.RemoveListener(OnPlusButtonClick);
            _normalBuyButton.onClick.RemoveListener(OnBuyNormalButtonClick);
            _superBuyButton.onClick.RemoveListener(OnBuySuperButtonClick);
            _startButton.onClick.RemoveListener(OnStartButtonClick);
            _cancelButton.onClick.RemoveListener(OnCancelButtonClick);
        }

        private void Awake()
        {
            if (payTable == null)
            {
                Debug.LogWarning("[BuyBonus] payTable is null");
                return;
            }

            _playerBetData.ExtraBetRatio = payTable.GetBetRatio(BuyType.BUY_EXTRA_BET);
        }

        private void Start()
        {
            ECurrency currency = _platformData.CurrencyEnum;

            //初始貨幣設定值
            ServiceUtils.SetCurrency(currencySetting.GetCurrency(currency));

            buyStep1BoardCanvasGroup.alpha = 0;
            buyStep1BoardCanvasGroup.blocksRaycasts = false;

            buyStep2BoardCanvasGroup.alpha = 0;
            buyStep2BoardCanvasGroup.blocksRaycasts = false;

            UpdateBuyBonusValueText();

            
        }

        //取得各種 BuyType 價格
        private double GetBuyValue(BuyType type)
        {
            return payTable.GetTotalBet(_playerBetData.Bet, type);
        }

        //更新第一層數字
        private void UpdateBuyBonusValueText()
        {
            //取得現在設定的投注額
            currentBet = _playerBetData.Bet;

            if (betList != null && betList.Length > 0)
            {
                betMinusButton.interactable = Array.IndexOf(betList, currentBet) != 0;
                betPlusButton.interactable = Array.IndexOf(betList, currentBet) != betList.Length - 1;
            }

            //buy bonus 介面文字
            betValueText.text = ServiceUtils.ToCurrentString(currentBet, Bottom_Define.MoneyFormat);
            buyFreeValueText.text = ServiceUtils.ToCurrentString(GetBuyValue(BuyType.BUY_FREE_SPINS), Bottom_Define.MoneyFormat);
            buySuperValueText.text = ServiceUtils.ToCurrentString(GetBuyValue(BuyType.BUY_SUPER_FREE_SPINS), Bottom_Define.MoneyFormat);

            currencySetting.SetCurrencyUI(_platformData.CurrencyEnum, null, betCurrencyText, betValueText);
            currencySetting.SetCurrencyUI(_platformData.CurrencyEnum, buyFreeCurrencyImage, buyFreeCurrencyText, buyFreeValueText);
            currencySetting.SetCurrencyUI(_platformData.CurrencyEnum, buySuperCurrencyImage, buySuperCurrencyText, buySuperValueText);

            betCurrencyText.gameObject.SetActive(!_platformData.IsUFA);
            buyFreeCurrencyText.gameObject.SetActive(!_platformData.IsUFA);
            buySuperCurrencyText.gameObject.SetActive(!_platformData.IsUFA);

            if (_platformData.IsUFA)
            {
                betValueText.alignment = TextAlignmentOptions.Center;
                buyFreeValueText.alignment = TextAlignmentOptions.Center;
                buySuperValueText.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                betValueText.alignment = TextAlignmentOptions.Left;
                buyFreeValueText.alignment = TextAlignmentOptions.Left;
                buySuperValueText.alignment = TextAlignmentOptions.Left;
            }
        }

        //第一層與第二層介面切換
        private void OpenStepPanel(bool isOpenFirstStep)
        {
            buyStep1BoardCanvasGroup.alpha = isOpenFirstStep ? 1 : 0;
            buyStep1BoardCanvasGroup.blocksRaycasts = isOpenFirstStep;

            buyStep2BoardCanvasGroup.alpha = isOpenFirstStep ? 0 : 1;
            buyStep2BoardCanvasGroup.blocksRaycasts = !isOpenFirstStep;
        }

        //開啟第二層
        private void OpenStep2Board()
        {
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button, checkRepeat: true);

            OpenStepPanel(false);

            freeTextImageGameObject.SetActive(tempBuyType == BuyType.BUY_FREE_SPINS);
            superFreeTextImageGameObject.SetActive(tempBuyType == BuyType.BUY_SUPER_FREE_SPINS);
            buyButton.interactable = _playerBetData.Balance - GetBuyValue(tempBuyType) >= 0;

            buyValueText.text = ServiceUtils.ToCurrentString(GetBuyValue(tempBuyType), Bottom_Define.MoneyFormat);
            currencySetting.SetCurrencyUI(_platformData.CurrencyEnum, buyCurrencyImage, buyCurrencyText, buyValueText);
            buyCurrencyText.gameObject.SetActive(!_platformData.IsUFA);
            LayoutRebuilder.ForceRebuildLayoutImmediate(buyValueText.transform.parent.GetComponent<RectTransform>());
        }

        //按下 BuyBonus 按鈕
        private void OnBuyBonusClick()
        {
            //SoundManager.PlaySound("se_buy_window");
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Buy_Window, checkRepeat: true);

            _uiPanelAnimator.PopUp(() =>
            {
                layoutGroup.transform.localPosition = Vector3.zero;
            });

            betList = _gameInfo.BetRange;
            currentBet = _playerBetData.Bet;

            UpdateBuyBonusValueText();

            OpenStepPanel(true);

            //直版一排兩個
            layoutGroup.constraintCount = 2;

            //橫版一排三個
            if (ResolutionManager.Instance.CheckRootIsLandscape(this.transform))
            {
                layoutGroup.constraintCount = 3;
            }
        }

        //第一層 - 取消購買
        private void OnBuyBonusCancel()
        {
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Cancel, checkRepeat: true);
            _uiPanelAnimator.Close();
            OpenStepPanel(true);

            tempBuyType = BuyType.BUY_NONE;
        }

        //第一層 - 購買各種選項
        public void OnBuyNormalClick()
        {
            tempBuyType = BuyType.BUY_FREE_SPINS;
            OpenStep2Board();
        }

        public void OnBuySuperClick()
        {
            tempBuyType = BuyType.BUY_SUPER_FREE_SPINS;
            OpenStep2Board();
        }

        //第二層 - 確認購買
        private void OnBuyFreeSpinsOK()
        {
            _uiPanelAnimator.Close();

            //避免重複發送事件
            if (!ResolutionManager.Instance.CheckIsOn(transform))
                return;

            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Confirm, checkRepeat: true);
            _playerBetData.BuyType = tempBuyType;
            tempBuyType = BuyType.BUY_NONE;

            new SpinTriggerEvent().Publish(this);
        }

        //第二層 - 取消購買，回到第一層
        private void OnBuyFreeSpinsCancel()
        {
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button, checkRepeat: true);
            OpenStepPanel(true);
        }

        //減少投注額
        private void OnBetMinusClick()
        {
            //避免重複發送事件
            if (!ResolutionManager.Instance.CheckIsOn(transform))
                return;

            int idx = Array.IndexOf(betList, currentBet);
            _playerBetData.Bet = betList[idx - 1];
            new GameChangeBetEvent().Publish(this);
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
        }

        //增加投注額
        private void OnBetPlusClick()
        {
            //避免重複發送事件
            if (!ResolutionManager.Instance.CheckIsOn(transform))
                return;

            int idx = Array.IndexOf(betList, currentBet);
            _playerBetData.Bet = betList[idx + 1];
            new GameChangeBetEvent().Publish(this);
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
        }

        #region Button Click Events
        private void OnCloseButtonClick()
        {
            new UIBuyBonusCancelEvent().Publish(this);
        }

        private void OnMinusButtonClick()
        {
            new UIBetMinusClickEvent().Publish(this);
        }

        private void OnPlusButtonClick()
        {
            new UIBetPlusClickEvent().Publish(this);
        }

        private void OnStartButtonClick()
        {
            new UIBuyFreeSpinsOkEvent().Publish(this);
        }

        private void OnCancelButtonClick()
        {
            new UIBuyFreeSpinsCancelEvent().Publish(this);
        }

        private void OnBuyNormalButtonClick()
        {
            new UIBuyBonusNormalClickEvent().Publish(this);
        }

        private void OnBuySuperButtonClick()
        {
            new UIBuyBonusSuperClickEvent().Publish(this);
        }
        #endregion

        #region Event Listener
        [OnGameEvent]
        private void OnGameChangeBetEvent(GameChangeBetEvent e)
        {
            UpdateBuyBonusValueText();
        }

        [OnGameEvent]
        private void OnUIBuyBonusClickEvent(UIBuyBonusClickEvent e)
        {
            OnBuyBonusClick();
        }

        [OnGameEvent]
        private void OnUIBuyBonusCancelEvent(UIBuyBonusCancelEvent e)
        {
            OnBuyBonusCancel();
        }

        [OnGameEvent]
        private void OnUIBuyFreeSpinsOkEvent(UIBuyFreeSpinsOkEvent e)
        {
            OnBuyFreeSpinsOK();
        }

        [OnGameEvent]
        private void OnUIBuyFreeSpinsCancelEvent(UIBuyFreeSpinsCancelEvent e)
        {
            OnBuyFreeSpinsCancel();
        }

        [OnGameEvent]
        private void OnUIBetMinusClickEvent(UIBetMinusClickEvent e)
        {
            OnBetMinusClick();
        }

        [OnGameEvent]
        private void OnUIBetPlusClickEvent(UIBetPlusClickEvent e)
        {
            OnBetPlusClick();
        }

        [OnGameEvent]
        private void OnUIBuyBonusNormalClickEvent(UIBuyBonusNormalClickEvent e)
        {
            OnBuyNormalClick();
        }

        [OnGameEvent]
        private void OnUIBuyBonusSuperClickEvent(UIBuyBonusSuperClickEvent e)
        {
            OnBuySuperClick();
        }
        #endregion
    }
}