using System;
using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Slot.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

namespace Slot.Common
{
    public class LoadingManager : MonoBehaviour
    {
        [SerializeField] private SlicedFilledImage _loadingBar;
        [SerializeField] private TextMeshProUGUI _loadingText;
        [SerializeField] private HorizontalScrollSnap _horizontalScrollSnap;
        [SerializeField] private UI_InfiniteScroll _infiniteScroll;
        [SerializeField] private Transform _contentTextsRoot;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _prevButton;
        [SerializeField] private int _changeDuration = 5;
        [SerializeField] private float _testLoadingDuration = 2f;
        [SerializeField] private float _testLoadingCompleteDelay = 0.2f;
        [SerializeField] private TextMeshProUGUI _progressInfoText;
        [SerializeField] private TextMeshProUGUI _VersionText;
        [SerializeField] private float _tempTime;

        private Tween _tween;
        private Action _onGameReady;
        private Action _onLoadingUIContinueClick;
#if !RELEASE_BUILD
        [SerializeField] private bool _useFakeLoading = false;
#endif
        [SerializeField] private Image _brandLogoImage;
        [SerializeField] private Sprite _comboLogoSprite;
        [SerializeField] private Sprite _llgLogoSprite;
        private Vector3[] _defaultPositions = new Vector3[3]; //存放轮播图的三个坐标(上一张、当前、下一张)
        private Transform[] _contentRoots; //记录所有要轮播的内容
        private int _currentIndex = 0; //当前轮播的索引

        //轮播的时候的两个对象的动画，用于提前结束动画
        private Tween tween1;
        private Tween tween2;


        private void OnEnable()
        {
            GameEventHub.Bind(this);
            _continueButton.onClick.AddListener(OnClickContinue);
            _nextButton.onClick.AddListener(OnNextButton);
            _prevButton.onClick.AddListener(OnPrevButton);
            // _horizontalScrollSnap.OnSelectionChangeStartEvent.AddListener(OnSelectionChangeStart);
            // _horizontalScrollSnap.OnSelectionPageChangedEvent.AddListener(OnSelectionPageChanged);
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
            _continueButton.onClick.RemoveListener(OnClickContinue);
            _nextButton.onClick.RemoveListener(OnNextButton);
            _prevButton.onClick.RemoveListener(OnPrevButton);
            // _horizontalScrollSnap.OnSelectionChangeStartEvent.RemoveListener(OnSelectionChangeStart);
            // _horizontalScrollSnap.OnSelectionPageChangedEvent.RemoveListener(OnSelectionPageChanged);
        }

        private void Awake()
        {
            _loadingBar.fillAmount = 0;
            _loadingText.text = "0%";
            _continueButton.gameObject.SetActive(false);

            _infiniteScroll.Init();

#if !RELEASE_BUILD
            if (_progressInfoText != null)
            {
                _progressInfoText.gameObject.SetActive(true);
                _progressInfoText.text = "";
            }
#endif
        }

        private void Start()
        {
            LogUtils.Log("[LoadingManager] Start");

            ChangeContentText(0);
            _VersionText.text = ServiceUtils.GetVersionText();
            _onGameReady = GameEventHub.Listen<GameReadyEvent>(this, (e) => OnLoadingBundleDone(e).Forget());
            _onLoadingUIContinueClick =
                GameEventHub.Listen<UILoadingContinueClickEvent>(this, (e) => OnClickContinueEvent());

            InitializeCarouselPositions();


#if !RELEASE_BUILD
            if (_useFakeLoading)
            {
                FakeLoading();
            }
#endif
        }


        private void Update()
        {
            if (StartupWebGate.IsStartupWebVisible)
                return;

            // _tempTime += Time.deltaTime;
            // //每 5 秒換下一張圖
            // if (_tempTime > _changeDuration)
            // {
            //     _tempTime = 0;
            //     _nextButton.onClick.Invoke();
            // }
        }

        private void InitializeCarouselPositions()
        {
            if (_infiniteScroll != null)
            {
                //禁用Horizontal Layout Group组件
                _horizontalScrollSnap.enabled = false;
                ScrollRect scrollRect = _infiniteScroll.GetComponent<ScrollRect>();
                if (scrollRect != null && scrollRect.content != null)
                {
                    Transform content = scrollRect.content;

                    // 强制重新计算布局，确保使用当前激活Canvas的尺寸
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform);

                    // 确保使用当前激活的Canvas中的子元素
                    if (content.childCount > 0)
                    {
                        Transform centerChild = content.GetChild(_currentIndex);
                        RectTransform rectTransform = centerChild as RectTransform;

                        float actualWidth = rectTransform.rect.width;
                        _defaultPositions[0] = new Vector3(centerChild.localPosition.x - actualWidth,
                            centerChild.localPosition.y, centerChild.localPosition.z);
                        _defaultPositions[1] = new Vector3(centerChild.localPosition.x, centerChild.localPosition.y,
                            centerChild.localPosition.z);
                        _defaultPositions[2] = new Vector3(centerChild.localPosition.x + actualWidth,
                            centerChild.localPosition.y, centerChild.localPosition.z);

                        // 重新设置所有子元素的位置
                        int childCount = content.childCount;
                        _contentRoots = new Transform[childCount];
                        for (int i = 0; i < childCount; i++)
                        {
                            Transform child = content.GetChild(i);
                            _contentRoots[i] = child;
                        }
                    }
                }
            }
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void UpdateLoadingProgress(LoadingProgressEvent e)
        {
            float progress = e.Progress;
            UpdateLoadingProgress(progress);
        }

#if !RELEASE_BUILD
        [OnGameEvent(SubscriberPriority.High)]
        private void UpdateProgressInfo(GameFlowProgressInfoEvent e)
        {
            _progressInfoText.text = e.ProgressInfo;
        }

        private void FakeLoading()
        {
            LogUtils.Log("[LoadingManager] FakeLoading Start");

            _tween = DOTween.To(() => _loadingBar.fillAmount, x => _loadingBar.fillAmount = x, 1, _testLoadingDuration)
                .SetEase(Ease.Linear).OnUpdate(() =>
                {
                    _loadingText.text = (_loadingBar.fillAmount * 100).ToString("F0") + "%";
                }).OnComplete(() =>
                {
                    _loadingText.text = "100%";
                    _tween = DOVirtual.DelayedCall(_testLoadingCompleteDelay, () =>
                    {
                        _loadingBar.transform.parent.gameObject.SetActive(false);
                        _continueButton.gameObject.SetActive(true);
                    });
                });
        }
#endif


        private void ChangeContentText(int index)
        {
            for (int i = 0; i < _contentTextsRoot.childCount; i++)
            {
                _contentTextsRoot.GetChild(i).gameObject.SetActive(false);
            }

            if (_contentTextsRoot.childCount <= index)
            {
                LogUtils.LogWarning("[LoadingManager] Loading Content Text is Not Setting. index: " + index);
                return;
            }

            _contentTextsRoot.GetChild(index).gameObject.SetActive(true);
        }

        private void OnClickContinue()
        {
            _tween?.Complete();
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button, checkRepeat: true);
            new ClientClickEvent(CommonDefine.EClientClick.ClickContinue).Publish(this);
            new ClientClickEvent(CommonDefine.EClientClick.EnterGameScene).Publish(this);
            new UILoadingContinueClickEvent().Publish(this);
        }

        private void OnClickContinueEvent()
        {
            _onLoadingUIContinueClick();
            Destroy(gameObject);
        }

        private void UpdateLoadingProgress(float progress)
        {
            _loadingBar.fillAmount = progress;
            _loadingText.text = (progress * 100).ToString("F0") + "%";
        }

        private async UniTask OnLoadingBundleDone(GameReadyEvent e)
        {
            if (e.isWaitGameUIReady)
            {
                await UniTask.Delay(500);
            }

            LogUtils.Log("[LoadingManager] Bundle download done, show continue button.");
            _loadingText.text = "100%";
            _loadingBar.fillAmount = 1.0f;
            // 隱藏Loading條
            _loadingBar.transform.parent.gameObject.SetActive(false);
            // 顯示Continue按鈕
            _continueButton.gameObject.SetActive(true);
            _onGameReady();
        }

        private void OnNextButton()
        {
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button, checkRepeat: true);
            StartSwitchContent(1);
        }

        private void OnPrevButton()
        {
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button, checkRepeat: true);
            StartSwitchContent(2);
        }

        private void StartSwitchContent(int pv)
        {
            ScrollRect scrollRect = _infiniteScroll.GetComponent<ScrollRect>();
            RectTransform content = scrollRect.content as RectTransform;
            if (content.anchoredPosition != Vector2.zero)
            {
                content.anchoredPosition = Vector2.zero;
            }

            //停止之前的动画
            if (tween1 != null)
                tween1.Complete();
            if (tween2 != null)
                tween2.Complete();


            if (pv == 1) //下一页
            {
                //当前页滑动到_defaultPositions[0]
                Transform currentContent = _contentRoots[_currentIndex];
                tween1 = currentContent.transform.DOLocalMove(_defaultPositions[0], 0.3f).SetEase(Ease.OutSine);
                //下一页从_defaultPositions[2]开始滑动到_defaultPositions[1]
                _currentIndex += 1;
                _currentIndex = _currentIndex >= _contentRoots.Length ? 0 : _currentIndex;
                _contentRoots[_currentIndex].localPosition = _defaultPositions[2];
                tween2 = _contentRoots[_currentIndex].transform.DOLocalMove(_defaultPositions[1], 0.3f)
                    .SetEase(Ease.OutSine);
            }
            else if (pv == 2) //上一页
            {
                //当前页滑动到_defaultPositions[2]
                Transform currentContent = _contentRoots[_currentIndex];
                tween1 = currentContent.transform.DOLocalMove(_defaultPositions[2], 0.3f).SetEase(Ease.OutSine);
                //上一页从_defaultPositions[0]开始滑动到_defaultPositions[1]
                _currentIndex -= 1;
                _currentIndex = _currentIndex < 0 ? _contentRoots.Length - 1 : _currentIndex;
                _contentRoots[_currentIndex].localPosition = _defaultPositions[0];
                tween2 = _contentRoots[_currentIndex].transform.DOLocalMove(_defaultPositions[1], 0.3f)
                    .SetEase(Ease.OutSine);
            }

            ChangeContentText(_currentIndex);
        }

        /// <summary>
        /// 設置品牌 Logo
        /// </summary>
        /// <param name="logoSprite"></param>
        public void SetBrandLogo(Sprite logoSprite)
        {
            if (logoSprite == null)
                return;

            _brandLogoImage.sprite = logoSprite;
            _brandLogoImage.SetNativeSize();
        }

        private Sprite GetLogoSprite(PlatformType platformType)
        {
            return platformType switch
            {
                _ => _comboLogoSprite,
            };
        }


        [OnGameEvent(SubscriberPriority.High)]
        private void OnChangePlatformEvent(SetPlatformIdEvent e)
        {
            Sprite sprite = GetLogoSprite(e.PlatformType);
            SetBrandLogo(sprite);
        }

        private void OnSelectionChangeStart()
        {
            _tempTime = 0;
        }

        private void OnSelectionPageChanged(int index)
        {
            //據圖片切換說明內文，配合多語系表
            ChangeContentText(_horizontalScrollSnap.CurrentPage);
        }
    }
}