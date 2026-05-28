using CriminalMakers.GameEventHub;
using UnityEngine;
using TMPro;
using VContainer;
using UnityEngine.UI;
using Core.UI;
using UnityEngine.Localization.Components;

namespace Slot.Common.UI
{
    public class ChangeCurrency : MonoBehaviour
    {
        private class UIChangeCurrencySwitchClickEvent : GameEvent { }
        private class UIChangeCurrencyCancelClickEvent : GameEvent { }

        [Inject] private BaseGameService _gameService;
        [Inject] private CurrencySettingSO _currencySetting;
        [Inject] private PlayerBetData _playerBetData;
        [Inject] private PlatformData _platformData;
        [Inject] private GameInfoSO _gameInfo;

        [SerializeField]
        private Button _maskButton;
        [SerializeField]
        private Button _closeButton;
        [SerializeField]
        private Button _switchButton;
        [SerializeField]
        private Button _cancelButton;

        [Space(10)]
        [SerializeField]
        private TextMeshProUGUI contentText;
        [SerializeField]
        private TextMeshProUGUI valueText;
        [SerializeField]
        private Image currencyImage;
        [SerializeField]
        private TextMeshProUGUI currencyText;

        private LocalizeStringEvent contextLocalize;
        private ECurrency newCurrency;
        private double newBalance;

        private void OnEnable()
        {
            GameEventHub.Bind(this);
            AddButtonsEvent();
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
            RemoveButtonsEvent();
        }

        private ECurrency GetCurrency(ECurrency currency)
        {
            //TODO 現在只有兩種貨幣互換
            if (currency == ECurrency.PHP)
            {
                return ECurrency.COM;
            }
            else
            {
                return ECurrency.PHP;
            }
        }

        private void OnWalletClick()
        {
            this.GetComponent<UIPanelAnimator>().PopUp();

            newCurrency = GetCurrency(_platformData.CurrencyEnum);

            //避免重複發送事件
            if (!ResolutionManager.Instance.CheckIsOn(transform)) return;

            _gameService.SendGetBalanceRequest(UpdateValue, newCurrency);
        }

        private void UpdateValue(ulong balance)
        {
            newBalance = ServiceUtils.ToClientBalance(balance);
            _currencySetting.SetCurrencyUI(newCurrency, currencyImage, currencyText, valueText);
            if (contextLocalize == null)
            {
                contextLocalize = contentText.GetComponent<LocalizeStringEvent>();
            }

            if (newCurrency == ECurrency.COM)
            {
                contextLocalize.StringReference.SetReference(CommonDefine.CommonTableName, CommonDefine.CommonKey_SwitchCombo);
            }
            else
            {
                contextLocalize.StringReference.SetReference(CommonDefine.CommonTableName, CommonDefine.CommonKey_SwitchReel);
            }

            valueText.text = ServiceUtils.ToNmuberString(newCurrency == ECurrency.COM, newBalance);
            LayoutRebuilder.ForceRebuildLayoutImmediate(valueText.transform.parent.GetComponent<RectTransform>());
        }

        private void OnChangeCurrencySwitchClick()
        {
            OnChangeCurrencyCancelClick();

            //避免重複發送事件
            if (!ResolutionManager.Instance.CheckIsOn(transform)) return;

            ServiceUtils.SetCurrency(_currencySetting.GetCurrency(newCurrency));

            _platformData.SetCurrencyEnum(newCurrency);
            _playerBetData.Balance = newBalance;
            new GameChangeBalanceEvent().Publish(this);
            //取得新幣值資訊
            // _gameService.SendGetGameInfoRequest((betRange) =>
            // {
            //     _gameInfo.SetBetRange(betRange);
            //     new GameChangeBetEvent().Publish(this);
            // });
        }

        private void OnChangeCurrencyCancelClick()
        {
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button, new AudioPlaySet());
            GetComponent<UIPanelAnimator>().Close();
        }
        #region Button Click Events
        private void AddButtonsEvent()
        {
            _maskButton.onClick.AddListener(OnCloseButtonClick);
            _closeButton.onClick.AddListener(OnCloseButtonClick);
            _switchButton.onClick.AddListener(OnSwitchButtonClick);
            _cancelButton.onClick.AddListener(OnCloseButtonClick);
        }

        private void RemoveButtonsEvent()
        {
            _maskButton.onClick.RemoveListener(OnCloseButtonClick);
            _closeButton.onClick.RemoveListener(OnCloseButtonClick);
            _switchButton.onClick.RemoveListener(OnSwitchButtonClick);
            _cancelButton.onClick.RemoveListener(OnCloseButtonClick);
        }

        private void OnCloseButtonClick()
        {
            new UIChangeCurrencyCancelClickEvent().Publish(this);
        }

        private void OnSwitchButtonClick()
        {
            new UIChangeCurrencySwitchClickEvent().Publish(this);
        }
        #endregion

        #region Event Listener
        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIWalletClickEvent(UIWalletClickEvent e)
        {
            OnWalletClick();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnChangeCurrencySwitchClickEvent(UIChangeCurrencySwitchClickEvent e)
        {
            OnChangeCurrencySwitchClick();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnChangeCurrencyCancelClickEvent(UIChangeCurrencyCancelClickEvent e)
        {
            OnChangeCurrencyCancelClick();
        }
        #endregion
    }
}
