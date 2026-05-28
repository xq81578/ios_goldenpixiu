using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using Slot.Common;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Slot.Common.UI
{
    /// <summary>
    /// 可序列化的 BuyType 和 LocalizedString 鍵值對
    /// </summary>
    [System.Serializable]
    public class BuyTypeLocalizedStringPair
    {
        [SerializeField]
        public BuyType Key;
        [SerializeField]
        public LocalizedString Value;

        public BuyTypeLocalizedStringPair() { }

        public BuyTypeLocalizedStringPair(BuyType key, LocalizedString value)
        {
            Key = key;
            Value = value;
        }
    }

    [RequireComponent(typeof(CanvasGroup))]
    public class InfoPanel : MonoBehaviour
    {
        [Inject]
        private GameInfoSO _gameInfoSO;
        [Inject]
        private PlatformData _platformData;
        
        [SerializeField]
        private InfoDynamicOdds _infoDynamicOdds;
        [SerializeField]
        private Sprite _spinSprite;
        [SerializeField]
        private Sprite _stopSprite;

        [SerializeField]
        private Transform _commonViewParentTransform;
        [SerializeField]
        [ListDrawerSettings(DefaultExpandedState = false)]
        private List<BuyTypeLocalizedStringPair> _customRTPLocalizedStringPairs = new();
        
        private Dictionary<BuyType, LocalizedString> _customRTPLocalizedStrings;
        [SerializeField]
        private PlatformAssetMapping _platformAssetMapping;

        [SerializeField]
        private ScrollRect _scrollRect;
        [SerializeField]
        private Button _closeButton;

        private InfoCommonView _infoCommonView;
        private CanvasGroup _canvasGroup;

        private void OnEnable()
        {
            GameEventHub.Bind(this);
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
        }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

            // 將可序列化的 List 轉換為 Dictionary
            _customRTPLocalizedStrings = new Dictionary<BuyType, LocalizedString>();
            foreach (var pair in _customRTPLocalizedStringPairs)
            {
                _customRTPLocalizedStrings[pair.Key] = pair.Value;
            }

            if (_gameInfoSO != null)
                return;

            var scope = LifetimeScope.Find<BootLifeTimeScope>();
            if (scope != null)
                scope.Container.Inject(this);
        }

        private async void Start()
        {
            _closeButton.onClick.AddListener(OnCloseButtonClick);
            if (_gameInfoSO == null)
                return;

            await UniTask.WaitUntil(() => _gameInfoSO.IsInit);
            SetCommonView(_gameInfoSO.ClientOptions.Platform);
        }

        private void OnDestroy()
        {
            _closeButton.onClick.RemoveListener(OnCloseButtonClick);
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private async void SetCommonView(PlatformType platformType)
        {
            if (_infoCommonView != null)
            {
                _infoCommonView.gameObject.SetActive(false);
                _infoCommonView = null;
            }

            GameObject gameObject = await _platformAssetMapping.GetGameObject(platformType, _commonViewParentTransform);
            if (gameObject == null)
                return;

            gameObject.SetActive(true);

            if (!gameObject.TryGetComponent<InfoCommonView>(out var commonView))
                return;

            _infoCommonView = commonView;
            SetCommonViewData();
            SetBetRange();
            await SetRTP();
            RefreshLayout();
        }

        private void SetCommonViewData()
        {
            _infoCommonView.SetAutoInfoActive(_gameInfoSO.ClientOptions.IsAutoActive);
            _infoCommonView.SetTurboInfoActive(_gameInfoSO.ClientOptions.IsTurboActive);
            bool showHomeInfo = !string.IsNullOrEmpty(_platformData.HomeUrl);
            _infoCommonView.SetHomeInfoActive(showHomeInfo);
            bool showHistoryInfo = !string.IsNullOrEmpty(_gameInfoSO.GetRecordUrlWithAccount(_platformData.Account));
            _infoCommonView.SetHistoryInfoActive(showHistoryInfo);
            _infoCommonView.SetSpinSprite(_spinSprite, _stopSprite);
        }

        private async UniTask SetRTP()
        {
            if (_infoCommonView == null){
                return;//兼容没有infoCommonView的游戏
            }
            await _infoCommonView.SetLocalize(
                _gameInfoSO.ClientOptions.RTP
                , _gameInfoSO.ClientOptions.IsBuyBonusActive
                , _customRTPLocalizedStrings);
        }

        private void SetBetRange()
        {
            _infoCommonView.SetBetRange(
                _gameInfoSO.BetRange[0].ToString()
                , _gameInfoSO.BetRange[^1].ToString());
        }

        private void OnCloseButtonClick()
        {
            new InfoUIActiveEvent(false).Publish(this);
        }

        private void SetActive(bool setActive)
        {
            _closeButton.interactable = setActive;
            _canvasGroup.alpha = setActive ? 1 : 0;
            _canvasGroup.blocksRaycasts = setActive;
            _canvasGroup.interactable = setActive;
            _scrollRect.enabled = setActive;
            _scrollRect.inertia = setActive;

            if (setActive)
            {
                _scrollRect.verticalNormalizedPosition = 1;
              
                _infoDynamicOdds?.Refresh();
            }
        }

        private async void OnLocaleChanged(Locale locale)
        {
            await UniTask.WaitForEndOfFrame();
            await SetRTP();
            RefreshLayout();
        }

        private async void RefreshLayout()
        {
            // 无 InfoCommonView 时仍要重建 ScrollRect（换语言后文案长度变化等）
            if (_infoCommonView != null)
                _infoCommonView.LayoutEnable(true);

            await UniTask.DelayFrame(10);
            if (_scrollRect != null && _scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
            await UniTask.WaitForEndOfFrame();

            if (_infoCommonView != null)
                _infoCommonView.LayoutEnable(false);
        }

        [OnGameEvent]
        private void OnInfoUIActiveEvent(InfoUIActiveEvent e)
        {
            SetActive(e.IsActive);
        }

        [OnGameEvent]
        private void OnChangePlatformEvent(SetPlatformIdEvent e)
        {
            if (e.PlatformType == PlatformType.NONE)
                return;
            
            SetCommonView(e.PlatformType);
        }

        [OnGameEvent]
        private void OnCurrencyChangeEvent(GameServiceReadyEvent e)
        {
            if (!_gameInfoSO.IsInit || _infoCommonView == null)
                return;

            SetBetRange();
            RefreshLayout();
        }
    }
}
