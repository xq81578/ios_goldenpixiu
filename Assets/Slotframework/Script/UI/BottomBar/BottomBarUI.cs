using DG.Tweening;
using Spine.Unity;
using Sirenix.OdinInspector;
using Slot.Common;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization.Components;

namespace Slot.Common.UI
{
    public class BottomBarUI : MonoBehaviour
    {
        [SerializeField]
        private BottomBarView _bottomBarView;

        [FoldoutGroup("貨幣圖案相關")]
        [SerializeField, FoldoutGroup("貨幣圖案相關")]
        private CurrencySettingSO _currencySetting;
        
        [FoldoutGroup("View相關")]
        [SerializeField, FoldoutGroup("View相關")]
        private PlatformAssetMapping _platformAssetMapping;

        [SerializeField, FoldoutGroup("Spin按鈕相關")]
        private GameObject _spinFGGameObject;
        [SerializeField, FoldoutGroup("Spin按鈕相關")]
        private TextMeshProUGUI _spinFGValueText;

        [FoldoutGroup("Marquee相關")]
        [SerializeField, FoldoutGroup("Marquee相關")]
        private CanvasGroup _marqueeCanvasGroup;
        [SerializeField, FoldoutGroup("Marquee相關")]
        private Transform _marqueeGroup;
        [SerializeField, FoldoutGroup("Marquee相關")]
        private float _marqueeDuration = 5f;
        [SerializeField, FoldoutGroup("Marquee相關")]
        private float _marqueeDelay = 1f;
        [SerializeField, FoldoutGroup("Marquee相關")]
        private int _marqueeIdx = -1;
        [SerializeField, FoldoutGroup("Marquee相關")]
        private List<Image> _marqueeNormalImages = new();
        private float _marqueeMaskWidth;
        private Tween _marqueeTween = null;

        [SerializeField, FoldoutGroup("Free Spin")]
        private Image _freeSpinImage;

        public GameObject SpinFGGameObject => _spinFGGameObject;
        public PlatformType PlatformType
        {
            get
            {
                if (_bottomBarView != null)
                    return _bottomBarView.PlatformType;
                return PlatformType.NONE;
            }
        }

        private void Start()
        {
            _spinFGGameObject.gameObject.SetActive(false);
            InitMarquee();
        }

        public void SetSpinSprite(Sprite idleSprite, Sprite stopSprite, Sprite freeGameSprite)
        {
            _bottomBarView.SetSpinSprite(idleSprite, stopSprite);

            if (freeGameSprite != null)
            {
                _freeSpinImage.sprite = freeGameSprite;
            }
        }

        public void SetSpinSplineAsset (SkeletonDataAsset _spinAssetData, string _spinIdleAnimName, string _spinStopAnimName, string _spinClickAnimName)
        {
            _bottomBarView.SetSpinSplineAsset (_spinAssetData, _spinIdleAnimName, _spinStopAnimName, _spinClickAnimName);
        }

        public async UniTask SetButtonBarView(PlatformType platformType)
        {
            var gameObject = await _platformAssetMapping.GetGameObject(platformType, transform);
            if (gameObject == null)
                return;

            gameObject.transform.SetAsFirstSibling();
            SetButtonBarViewInternal(gameObject);
        }
        
        private void SetButtonBarViewInternal(GameObject newBottomBarPrefab)
        {
            BottomBarView newBottomBarView = newBottomBarPrefab.GetComponent<BottomBarView>();

            if (_bottomBarView != null)
            {
                RemoveButtonsEvent(_bottomBarView);
                _bottomBarView.gameObject.SetActive(false);
            }

            _bottomBarView = newBottomBarView;
        }

        public void OnSpinClick()
        {
            _bottomBarView.OnSpinClick();
        }

        public void OnSetBottomUiNormal(bool isAuto, bool isFreeGame)
        {
            _bottomBarView.OnSetBottomUiNormal(isAuto, isFreeGame);
        }

        public void OnSetBottomUiLock(bool isAuto)
        {
            _bottomBarView.OnSetBottomUiLock (isAuto);
        }

        public void UpdateAutoCount(bool isAuto, bool isFreeGame, int autoSpinCount)
        {
            _bottomBarView.OnUpdateAutoCount(isAuto, isFreeGame, autoSpinCount);
        }

        public void UpdateFreeGameCount(int freeGameCount)
        {
            _spinFGValueText.text = freeGameCount.ToString();
        }

        public void SetAutoTurboActive(bool isAutoActive, bool isTurboActive)
        {
            _bottomBarView.SetAutoTurboActive(isAutoActive, isTurboActive);
        }

        public void SetTurboSprite(Bottom_TurboType turboType)
        {
            _bottomBarView.SetTurboSprite(turboType);
        }

        public void SetAutoSpinStop()
        {
            _bottomBarView.OnAutoStopClick();
        }

        public void OnChangeBet(string bet)
        {
            _bottomBarView.OnChangeBet(bet);
        }

        public void OnChangeBalance(double balance, ECurrency currency, bool isChangeCurrency)
        {
            _bottomBarView.OnChangeBalance(balance, currency, _currencySetting, isChangeCurrency);
        }

        public void OnSetWin(double currentWin, double targetWin, bool isAdd)
        {
            if (isAdd)
            {
                _bottomBarView.OnAddWin(currentWin, targetWin);
                return;
            }

            _bottomBarView.OnSetWin(currentWin, targetWin);
        }

        public void OnUpdateTransText(string transText)
        {
            _bottomBarView.OnUpdateTransText(transText);
        }

        public async UniTask PlayWinAnimation()
        {
            await _bottomBarView.PlayWinAnimation();
        }

        //顯示跑馬燈
        public void ShowMarquee(bool isShow, bool hasWinValue = false)
        {
            if (_marqueeCanvasGroup == null || _marqueeGroup.childCount == 0
            || (isShow && _marqueeCanvasGroup.alpha == 1 && _marqueeIdx >= 0))
                return;

            if (hasWinValue)
            {
                isShow = false;
            }

            if (isShow)
            {
                _marqueeCanvasGroup.alpha = 1;
                PlayMarquee();
            }
            else
            {
                _marqueeCanvasGroup.alpha = 0;
                _marqueeTween?.Kill();
            }
        }

        public void ActiveBottomBarView()
        {
            _bottomBarView.gameObject.SetActive(true);
            _bottomBarView.OnResetWin();
            ShowMarquee(true);
            AddButtonsEvent(_bottomBarView);
        }

        public void InitNetText(ECurrency currencyEnum)
        {
            string currencySymbol = _currencySetting.GetCurrencyText(currencyEnum);
            if (_bottomBarView.CurrencySymbol != currencySymbol)
            {
                _bottomBarView.CurrencySymbol = currencySymbol;
            }
            SetNetText(0);
        }

        public void SetNetText(double value)
        {
            _bottomBarView.SetNetText(value);
        }

        public void SetPlayTimeText(string playTime)
        {
            _bottomBarView.SetPlayTimeText(playTime);
        }

        public void SetWinBgImage(Sprite winBgSprite)
        {
            _bottomBarView.SetWinBgImage(winBgSprite);
        }

        public void SetWinSpine(SkeletonDataAsset winSpineDataAsset, string animationName)
        {
            _bottomBarView.SetWinSpine(winSpineDataAsset, animationName);
        }

        //播放跑馬燈
        private void PlayMarquee()
        {
            if (_marqueeCanvasGroup.alpha == 0 || _marqueeNormalImages.Count == 0) return;

            _marqueeIdx++;

            // 不是第一個跑馬燈，要先關閉前一個
            if (_marqueeIdx > 0)
            {
                _marqueeNormalImages[_marqueeIdx - 1].gameObject.SetActive(false);
                _marqueeNormalImages[_marqueeIdx - 1].enabled = false;
            }

            // 已經播到最後一個，要回播第一個
            if (_marqueeIdx > _marqueeGroup.childCount - 1)
            {
                _marqueeIdx = 0;
            }

            _marqueeNormalImages[_marqueeIdx].gameObject.SetActive(true);
            _marqueeNormalImages[_marqueeIdx].enabled = true;

            //跑馬燈沒超出範圍，原地顯示不跑動
            if (_marqueeNormalImages[_marqueeIdx].GetComponent<RectTransform>().rect.width < _marqueeMaskWidth)
            {
                _marqueeNormalImages[_marqueeIdx].transform.localPosition = Vector2.zero;
                _marqueeTween = _marqueeNormalImages[_marqueeIdx].transform.DOLocalMoveX(0, 0).SetDelay(_marqueeDuration + _marqueeDelay).SetEase(Ease.Linear);
            }
            else
            {
                //等待一秒後才開始跑動
                _marqueeNormalImages[_marqueeIdx].transform.localPosition = new Vector2(((_marqueeNormalImages[_marqueeIdx].transform.GetComponent<RectTransform>().rect.width - _marqueeMaskWidth) / 2) + 50, 0);
                _marqueeTween = _marqueeNormalImages[_marqueeIdx].transform.DOLocalMoveX(-(_marqueeNormalImages[_marqueeIdx].transform.GetComponent<RectTransform>().rect.width + _marqueeMaskWidth) / 2, _marqueeDuration).SetDelay(_marqueeDelay).SetEase(Ease.Linear);
            }
            _marqueeTween.OnComplete(PlayMarquee);
        }

        private void InitMarquee()
        {
            _marqueeMaskWidth = _marqueeGroup.transform.GetComponent<RectTransform>().rect.width;
            for (int i = 0; i < _marqueeGroup.childCount; i++)
            {
                Image image = _marqueeGroup.GetChild(i).GetComponent<Image>();
                image.enabled = false;

                // 沒有掛圖片跟多語系
                if (image == null ||
                    image.sprite == null && image.gameObject.GetComponent<LocalizeSpriteEvent>().AssetReference.IsEmpty)
                {
                    continue;
                }

                _marqueeNormalImages.Add(image);
            }
        }
        
        #region Button Click Events
        private void AddButtonsEvent(BottomBarView view)
        {
            view.spinBtn.onClick.AddListener(OnUISpinClickEvent);
            view.quickStopBtn.onClick.AddListener(OnUISpinClickEvent);
            view.autoSpinStopBtn.onClick.AddListener(OnAutoStopButtonClick);
            if (view.balanceCurrencyButton != null)
            {
                view.balanceCurrencyButton.onClick.AddListener(OnWalletButtonClick);
            }
            view.turboBtn.onClick.AddListener(OnTurboButtonClick);
            view.AutoBtn.onClick.AddListener(OnAutoButtonClick);
            view.betBtn.onClick.AddListener(OnBetButtonClick);
            view._menuButton.onClick.AddListener(OnMenuButtonClick);
            view._infoButton.onClick.AddListener(OnInfoButtonClick);
        }

        private void RemoveButtonsEvent(BottomBarView view)
        {
            view.spinBtn.onClick.RemoveListener(OnUISpinClickEvent);
            view.quickStopBtn.onClick.RemoveListener(OnUISpinClickEvent);
            view.autoSpinStopBtn.onClick.RemoveListener(OnAutoStopButtonClick);
            if (view.balanceCurrencyButton != null)
            {
                view.balanceCurrencyButton.onClick.RemoveListener(OnWalletButtonClick);
            }
            view.turboBtn.onClick.RemoveListener(OnTurboButtonClick);
            view.AutoBtn.onClick.RemoveListener(OnAutoButtonClick);
            view.betBtn.onClick.RemoveListener(OnBetButtonClick);
            view._menuButton.onClick.RemoveListener(OnMenuButtonClick);
            view._infoButton.onClick.RemoveListener(OnInfoButtonClick);
        }

        private void OnUISpinClickEvent()
        {
            new SpinTriggerEvent().Publish(this);
        }

        private void OnAutoStopButtonClick()
        {
            new UIAutoSpinStopClickEvent().Publish(this);
        }

        private void OnMenuButtonClick()
        {
            new UIMenuClickEvent().Publish(this);
        }

        private void OnWalletButtonClick()
        {
            new UIWalletClickEvent().Publish(this);
        }

        private void OnBetButtonClick()
        {
            new UIBetClickEvent().Publish(this);
        }

        private void OnInfoButtonClick()
        {
            new InfoUIActiveEvent(true).Publish(this);
            new ClientClickEvent(CommonDefine.EClientClick.ClickInfo).Publish(this);
        }

        private void OnTurboButtonClick()
        {
            new UITurboClickEvent().Publish(this);
        }

        private void OnAutoButtonClick()
        {
            new UIAutoClickEvent().Publish(this);
        }

        public void SetWinPosY(float posY)
        {
            _bottomBarView.SetWinPosY(posY);
            RectTransform marqueeRectTransform = _marqueeCanvasGroup.GetComponent<RectTransform>();
            marqueeRectTransform.anchoredPosition = new Vector2(marqueeRectTransform.anchoredPosition.x, posY);
        }
        #endregion
    }
}