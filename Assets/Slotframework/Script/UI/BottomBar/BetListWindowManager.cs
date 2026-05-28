using System;
using System.Collections.Generic;
using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Slot.Common.UI
{
    public class BetListWindowManager : MonoBehaviour
    {
        private const string name_select = "Select";

        private class UIBetOptionClickEvent : GameEvent
        {
            public int Index { get; }
            public UIBetOptionClickEvent(int index)
            {
                Index = index;
            }
        }

        [Inject]
        private PlayerBetData _playerBetData;
        [Inject]
        private PlatformData _platformData;
        [Inject]
        private GameInfoSO _gameInfoSO;

        [SerializeField]
        private CurrencySettingSO _currencySetting;
        [SerializeField]
        private Sprite buttonOffSprite = null;
        [SerializeField]
        private Sprite buttonOnSprite = null;

        private CanvasGroup canvasGroup = null;
        private List<Image> betButtonImageList = new List<Image>();
        private List<TextMeshProUGUI> betCurrencyTextList = new List<TextMeshProUGUI>();
        private List<TextMeshProUGUI> betValueTextList = new List<TextMeshProUGUI>();
        private string _currencyStr = "";
        private bool isShowing = false;

        #region Unity Methods
        private void OnEnable()
        {
            GameEventHub.Bind(this);
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
        }

        private void Start()
        {
            if (_playerBetData == null)
            {
                var scope = LifetimeScope.Find<BootLifeTimeScope>();
                if (scope != null)
                    scope.Container.Inject(this);
            }
            
            //取得CanvasGroup
            canvasGroup = GetComponent<CanvasGroup>();
            var node = transform.Find("Buttons");
            int count = node.childCount;
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                var tf = node.GetChild(i);
                tf.GetComponent<Button>().onClick.AddListener(() =>
                {
                    new UIBetOptionClickEvent(idx).Publish(this);
                });
                betButtonImageList.Add(tf.GetComponent<Image>());
                if (tf.GetChild(0).childCount < 2)
                {
                    // 純數字
                    betCurrencyTextList.Add(tf.GetComponentInChildren<TextMeshProUGUI>());
                    betValueTextList.Add(tf.GetComponentInChildren<TextMeshProUGUI>());
                }
                else
                {
                    // 有貨幣符號
                    betCurrencyTextList.Add(tf.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>());
                    betValueTextList.Add(tf.GetChild(0).GetChild(1).GetComponent<TextMeshProUGUI>());
                }
            }

            UpdateBetList();
        }
        #endregion

        private async void OnActive()
        {
            bool isActive = !canvasGroup.interactable;
            await PlayAnimation(isActive);
            canvasGroup.interactable = isActive;
            canvasGroup.blocksRaycasts = isActive;
        }

        private async UniTask PlayAnimation(bool isActive)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            isShowing = true;
            int value = isActive ? 1 : 0;
            const float duration = 0.2f;
            canvasGroup.DOFade(value, duration);
            canvasGroup.transform.DOScale(value, duration);
            await UniTask.WaitForSeconds(duration);
            await UniTask.WaitForEndOfFrame();
            isShowing = false;
        }

        private void OnClose()
        {
            if (canvasGroup.interactable)
            {
                PlayAnimation(false).Forget();
            }
        }

        private void UpdateBetList()
        {
            var betList = _gameInfoSO.BetRange;
            if (betList == null || betList.Length == 0 || _playerBetData == null)
            {
                return;
            }   

            for (int i = 0; i < betValueTextList.Count; i++)
            {
                if (i > betList.Length - 1)
                {
                    betCurrencyTextList[i].text = "";
                    betValueTextList[i].text = "";
                    betButtonImageList[i].GetComponent<Button>().interactable = false;
                    continue;
                }

                if (betCurrencyTextList[i] == betValueTextList[i])
                {
                    betValueTextList[i].alignment = TextAlignmentOptions.Center;
                }
                else
                {
                    betCurrencyTextList[i].gameObject.SetActive(true);
                    betCurrencyTextList[i].text = _currencyStr;
                    betValueTextList[i].alignment = TextAlignmentOptions.Right;
                }
                betValueTextList[i].text = betList[i].ToString(Bottom_Define.MoneyFormat);
                betButtonImageList[i].GetComponent<Button>().interactable = true;
            }

            double currentBet = _playerBetData.Bet;
            //尋找目前的bet在哪個index
            int index = Array.FindIndex(betList, x => x == currentBet);
            if (index == -1)
            {
                index = 0;
                _playerBetData.Bet = betList[0];
                new GameChangeBetEvent().Publish(this);
            }

            //先將所有按鈕圖片改為Off
            foreach (var btn in betButtonImageList)
            {
                if (buttonOffSprite == null)
                {
                    btn.transform.Find(name_select).gameObject.SetActive(false);
                    continue;
                }
                btn.sprite = buttonOffSprite;
            }
            //將選到的按鈕圖片改為On
            if (buttonOnSprite == null)
            {
                betButtonImageList[index].transform.Find(name_select).gameObject.SetActive(true);
                return;
            }
            betButtonImageList[index].sprite = buttonOnSprite;
        }

        private void OnClickOption(int index)
        {
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button, new AudioPlaySet());
            _playerBetData.Bet = _gameInfoSO.BetRange[index];
            new GameChangeBetEvent().Publish(this);
            OnClose();
        }

        #region Event Listener
        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBottomLockEvent(UIBottomLockEvent e)
        {
            OnClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnSpinTriggerkEvent(SpinTriggerEvent e)
        {
            OnClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIAutoClickEvent(UIAutoClickEvent e)
        {
            OnClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIMenuClickEventEvent(UIMenuClickEvent e)
        {
            OnClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnInfoUIActiveEvent(InfoUIActiveEvent e)
        {
            OnClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIAllCloseEvent(UIAllCloseEvent e)
        {
            OnClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnGameChangeBetEvent(GameChangeBetEvent e)
        {
            UpdateBetList();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBetClickEvent(UIBetClickEvent e)
        {
            if (isShowing)
                return;

            _currencyStr = _currencySetting.GetCurrencyText(_platformData.CurrencyEnum);
            UpdateBetList();
            OnActive();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBetClickOptionClickEvent(UIBetOptionClickEvent e)
        {
            OnClickOption(e.Index);
        }
        #endregion
    }
}