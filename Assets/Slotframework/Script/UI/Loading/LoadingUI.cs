using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slot.Common.UI
{
    public class LoadingUI : MonoBehaviour
    {
        [Header("Loading Bar")]
        [SerializeField]
        private GameObject _loadingBarRoot;
        [SerializeField]
        private SlicedFilledImage _loadingBar;
        [SerializeField]
        private TextMeshProUGUI _loadingText;
        [SerializeField]
        private TextMeshProUGUI _progressInfoText;

        [SerializeField]
        private RectTransform _scrollContentRoot;
        [SerializeField]
        private List<RectTransform> _scrollContentItems;
        [SerializeField]
        private Transform _scrollContentTextRoot;
        [SerializeField]
        private List<TextMeshProUGUI> _scrollContentTexts;
        [SerializeField]
        private Button _nextButton;
        [SerializeField]
        private Button _prevButton;

        [Header("Continue Button"), Space(10)]
        [SerializeField]
        private Button _continueButton;

        [Header("Version Text"), Space(10)]
        [SerializeField]
        private TextMeshProUGUI _versionText;
        [SerializeField]
        private Image _brandLogoImage;

        private LoadingUIContentSwitchEvent _loadingUIContentSwitchEvent;

        private void OnEnable()
        {
            _loadingUIContentSwitchEvent = new();
            _continueButton.onClick.AddListener(() => new UILoadingContinueClickEvent().Publish(this));
            _nextButton.onClick.AddListener(() => _loadingUIContentSwitchEvent.SetNext(true).Publish(this));
            _prevButton.onClick.AddListener(() => _loadingUIContentSwitchEvent.SetNext(false).Publish(this));
        }

        private void Awake()
        {
            _continueButton.gameObject.SetActive(false);
            _loadingBarRoot.SetActive(true);
            _loadingBar.gameObject.SetActive(true);
            _loadingBar.fillAmount = 0;
            _loadingText.text = "0%";
            _progressInfoText.text = "";

            GetAllContentItems();
            SetContentTextActive(0);
        }

        private void OnDisable()
        {
            _continueButton.onClick.RemoveAllListeners();
            _nextButton.onClick.RemoveAllListeners();
            _prevButton.onClick.RemoveAllListeners();
            _loadingUIContentSwitchEvent = null;
        }

        /// <summary>
        /// 設置進度條進度，0~1
        /// </summary>
        /// <param name="progress"></param>
        public void SetLoadingBarProgress(float progress)
        {
            _loadingBar.fillAmount = progress;
            _loadingText.text = (progress * 100).ToString("F0") + "%";
        }

        /// <summary>
        /// 設置進度資訊文字
        /// </summary>
        /// <param name="text"></param>
        public void SetLoadingBarProgressInfoText(string text)
        {
            _progressInfoText.text = text;
        }

        /// <summary>
        /// 輪播內容切換
        /// 切換到下一個或上一個內容，根據 next 參數決定方向。
        /// </summary>
        /// <param name="next">為 true 時切換到下一個，false 時切換到上一個。</param>
        public async void ScrollContentSwitch(bool next, int index, float duration)
        {
            SwitchButtonsInteractable(false);
            await PlayScrollContentSwitchAnimation(next, index, duration);
            if (this != null)
            {
                SwitchButtonsInteractable(true);
            }
        }

        /// <summary>
        /// 顯示繼續按鈕，隱藏進度條
        /// </summary>
        public void ShowContinueButton()
        {
            _continueButton.gameObject.SetActive(true);
            _loadingBarRoot.SetActive(false);
        }

        /// <summary>
        /// 設置版本文字
        /// </summary>
        /// <param name="version"></param>
        public void SetVersionText(string version)
        {
            _versionText.text = version;
        }

        /// <summary>
        /// 播放輪播內容切換動畫
        /// </summary>
        /// <param name="next"></param>
        /// <param name="index"></param>
        private async UniTask PlayScrollContentSwitchAnimation(bool next, int index, float duration)
        {
            int lastIndex = next ? index - 1 : index + 1;
            lastIndex = ((lastIndex % _scrollContentItems.Count) + _scrollContentItems.Count) % _scrollContentItems.Count;
            index = ((index % _scrollContentItems.Count) + _scrollContentItems.Count) % _scrollContentItems.Count;
            
            RectTransform currentItem = _scrollContentItems[index];
            RectTransform lastItem = _scrollContentItems[lastIndex];

            float startX = _scrollContentRoot.rect.width;

            currentItem.anchoredPosition = new Vector2(next ? startX : -startX, 0);

            Vector2 currentStart = currentItem.anchoredPosition;
            Vector2 lastStart = lastItem.anchoredPosition;

            Vector2 currentEndPos = new(0, 0);
            Vector2 lastEndPos = new(next ? -startX : startX, 0);

            currentItem.anchoredPosition = currentStart;
            lastItem.anchoredPosition = lastStart;

            float d = duration;
            float t = 0f;

            while (t < d && this != null)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / d);
                // ease-out cubic：一開始快，接近尾端減速
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                currentItem.anchoredPosition = Vector2.LerpUnclamped(currentStart, currentEndPos, eased);
                lastItem.anchoredPosition = Vector2.LerpUnclamped(lastStart, lastEndPos, eased);
                await UniTask.Yield();
            }

            if (this == null)
            {
                return;
            }

            currentItem.anchoredPosition = currentEndPos;
            lastItem.anchoredPosition = lastEndPos;
        }

        /// <summary>
        /// 設置輪播內容文字項目顯示
        /// </summary>
        /// <param name="index"></param>
        public void SetContentTextActive(int index)
        {
            index = ((index % _scrollContentTexts.Count) + _scrollContentTexts.Count) % _scrollContentTexts.Count;

            for (int i = 0; i < _scrollContentTexts.Count; i++)
            {
                _scrollContentTexts[i].enabled = i == index;
            }
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

        /// <summary>
        /// 獲取輪播內容文字項目
        /// </summary>
        private void GetAllContentItems()
        {
            _scrollContentTexts = new List<TextMeshProUGUI>();

            foreach (Transform child in _scrollContentTextRoot)
            {
                if (child.TryGetComponent<TextMeshProUGUI>(out var text))
                {
                    _scrollContentTexts.Add(text);
                }
            }

            _scrollContentItems = new List<RectTransform>();

            foreach (RectTransform child in _scrollContentRoot)
            {
                _scrollContentItems.Add(child);
            }
        }

        private void SwitchButtonsInteractable(bool interactable)
        {
            _nextButton.interactable = interactable;
            _prevButton.interactable = interactable;
        }
    }
}
