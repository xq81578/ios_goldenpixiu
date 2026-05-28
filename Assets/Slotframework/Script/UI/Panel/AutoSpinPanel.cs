using System;
using CriminalMakers.GameEventHub;
using DG.Tweening;
using Slot.Common;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Slot.Common.UI
{
    public class AutoSpinPanel : MonoBehaviour
    {
        [Inject] private AutoSpinSetting _autoSpinSetting;

        #region UI Elements

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _maskButton;
        [SerializeField] private Button _closeButton;

        [SerializeField] private ToggleGroup _totalSpinsToggleGroup;
        [SerializeField] private Toggle[] _totalSpinsToggles;

        [SerializeField] private Button _stopIfFreeGameActiveButton;
        [SerializeField] private GameObject _stopIfFreeGameActiveGameObject;

        #endregion

        #region Game Event

        private class UIAutoSpinPanelCloseEvent : GameEvent
        {
        }

        private class UIAutoSpinPanelUpdateEvent : GameEvent
        {
            public AutoSpinOptionTypeEnum OptionTypeEnum { get; private set; }
            public bool IsSetValue { get; private set; }
            public bool IsSetActive { get; private set; }
            public int ToggleIndex { get; private set; } = -1;

            public UIAutoSpinPanelUpdateEvent Set(AutoSpinOptionTypeEnum optionType, bool isSetValue, bool isSetActive,
                int index = -1)
            {
                OptionTypeEnum = optionType;
                IsSetValue = isSetValue;
                IsSetActive = isSetActive;
                if (index >= 0)
                    ToggleIndex = index;

                return this;
            }
        }

        #endregion

        private Action _onCloseListener;
        private Action _onStartListener;
        private Action _onUpdateListener;

        private UIAutoSpinPanelUpdateEvent _uiAutoSpinPanelUpdateEvent;

        #region Unity Lifecycle

        private void Start()
        {
            if (_autoSpinSetting == null)
            {
                LogUtils.LogWarning("AutoSpinSetting is not injected!");
                return;
            }

            GameEventHub.Bind(this);
            _totalSpinsToggles[0].SetIsOnWithoutNotify(true);
        }

        private void OnDestroy()
        {
            GameEventHub.Unbind(this);
        }

        #endregion

        private void SetValue(AutoSpinOptionTypeEnum optionTypeEnum, int value)
        {
            _autoSpinSetting.SetValue(optionTypeEnum, value);
            _uiAutoSpinPanelUpdateEvent.Set(optionTypeEnum, true, false, value).Publish(this);
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
        }

        private void SetOptionActive(AutoSpinOptionTypeEnum optionTypeEnum)
        {
            _autoSpinSetting.SetActive(optionTypeEnum, !GetOptionActive(optionTypeEnum));
            _uiAutoSpinPanelUpdateEvent.Set(optionTypeEnum, false, true).Publish(this);
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
        }

        private bool GetOptionActive(AutoSpinOptionTypeEnum optionTypeEnum)
        {
            return optionTypeEnum switch
            {
                AutoSpinOptionTypeEnum.StopIfFreeGameIsActive => _autoSpinSetting.IsStopIfFreeGameActive,
                _ => false,
            };
        }

        private void SetActive(bool isActive)
        {
            _canvasGroup.interactable = isActive;
            _canvasGroup.alpha = isActive ? 1 : 0;
            _canvasGroup.blocksRaycasts = isActive;
        }

        private void OnStartButtonClick()
        {
            _autoSpinSetting.SetAutoSpinEnabled();
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
            new UIAutoSpinCountEvent().Publish(this);
            var autoSpinStartEvent = new AutoSpinStartEvent()
            {
                TotalSpins = _autoSpinSetting.TotalSpins,
                IsStopFreeGame = _autoSpinSetting.IsStopIfFreeGameActive,
            };
            DOVirtual.DelayedCall(0.6f, () =>
            {
                //先隐藏autospin界面然后延遲0.5秒後開始自動旋轉
                new SpinTriggerEvent().Publish(this);
            });

            autoSpinStartEvent.Publish(this);
        }

        private void RegisterEvents()
        {
            _uiAutoSpinPanelUpdateEvent ??= new UIAutoSpinPanelUpdateEvent();

            // ===== 關閉面板相關事件註冊 =====
            _onCloseListener = GameEventHub.Listen<UIAutoSpinPanelCloseEvent>(this, OnUICloseEvent);
            // 關閉面板的三個按鈕
            _cancelButton.onClick.AddListener(() =>
                {
                    AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
                    new UIAutoSpinPanelCloseEvent().Publish(this);
                }
            );
            _maskButton.onClick.AddListener(() =>
            {
                AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
                new UIAutoSpinPanelCloseEvent().Publish(this);
            });
            _closeButton.onClick.AddListener(() =>
            {
                AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
                new UIAutoSpinPanelCloseEvent().Publish(this);
            });

            // ===== 開始自動旋轉事件註冊 =====
            _onStartListener = GameEventHub.Listen<AutoSpinStartEvent>(this, OnUIStartEvent);
            _startButton.onClick.AddListener(OnStartButtonClick);

            // ===== 更新事件註冊 =====
            _onUpdateListener = GameEventHub.Listen<UIAutoSpinPanelUpdateEvent>(this, OnUIUpdateEvent);

            // ===== Stop If Free Game Is Activated 區塊 =====
            _stopIfFreeGameActiveButton.onClick.AddListener(() =>
                SetOptionActive(AutoSpinOptionTypeEnum.StopIfFreeGameIsActive));

            for (int i = 0; i < _totalSpinsToggles.Length; i++)
            {
                int index = i; // Local copy for closure
                _totalSpinsToggles[i].onValueChanged.AddListener(isOn =>
                {
                    if (isOn)
                    {
                        SetValue(AutoSpinOptionTypeEnum.TotalSpins, index);
                    }
                });
            }
        }

        private void UnregisterEvents()
        {
            _uiAutoSpinPanelUpdateEvent = null;

            // ===== 關閉面板相關事件移除 =====
            _onCloseListener?.Invoke();
            _onCloseListener = null;
            _cancelButton.onClick.RemoveAllListeners();
            _maskButton.onClick.RemoveAllListeners();
            _closeButton.onClick.RemoveAllListeners();

            // ===== 開始自動旋轉事件移除 =====
            _onStartListener?.Invoke();
            _onStartListener = null;
            _startButton.onClick.RemoveAllListeners();

            // ===== 更新事件移除 =====
            _onUpdateListener?.Invoke();
            _onUpdateListener = null;

            // ===== Stop If Free Game Is Activated 區塊 =====
            _stopIfFreeGameActiveButton.onClick.RemoveAllListeners();

            for (int i = 0; i < _totalSpinsToggles.Length; i++)
            {
                _totalSpinsToggles[i].onValueChanged.RemoveAllListeners();
            }
        }

        #region Event Listeners (Dynamic)

        private void OnUICloseEvent(UIAutoSpinPanelCloseEvent e)
        {
            SetActive(false);
            UnregisterEvents();
        }

        private void OnUIStartEvent(AutoSpinStartEvent e)
        {
            OnUICloseEvent(null);
        }

        private void OnUIUpdateEvent(UIAutoSpinPanelUpdateEvent e)
        {
            if (e.IsSetActive)
            {
                _stopIfFreeGameActiveGameObject.SetActive(_autoSpinSetting.IsStopIfFreeGameActive);
            }
            else if (e.ToggleIndex >= 0 && e.OptionTypeEnum == AutoSpinOptionTypeEnum.TotalSpins)
            {
                _totalSpinsToggles[e.ToggleIndex].SetIsOnWithoutNotify(true);
            }
        }

        #endregion

        #region Event Listeners (Static)

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIAutoClickEvent(UIAutoClickEvent e)
        {
            SetActive(true);
            RegisterEvents();
        }

        #endregion
    }
}