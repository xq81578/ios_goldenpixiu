using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slot.Common.UI
{
    public class RewardPresentationUI : MonoBehaviour
    {
        [Header("Base UI Elements")]
        [SerializeField]
        protected CanvasGroup _canvasGroup;
        [SerializeField]
        protected Button _closeButton;
        [SerializeField]
        protected TextMeshProUGUI _valueText;

        [Header("Spine Settings")]
        [SerializeField]
        protected SkeletonGraphic _spine;
        [SerializeField]
        protected string _animationIn;
        [SerializeField]
        protected string _animationLoop;
        [SerializeField]
        protected float _fadeDuration = 0.2f;

        [Header("Bone Follower Settings")]
        [SerializeField]
        protected BoneFollowerGraphic _boneFollowerGraphic;
        [SerializeField]
        protected string _boneName;
        [SerializeField]
        private bool _followXYPosition = false;
        [SerializeField]
        private bool _followLocalScale = false;

        private Action _uiHideCompleteCallback;
        protected const string NumberFormat = "#,##0.00";

        protected virtual void Awake()
        {
            SetUIVisibility(false).Forget();
        }

        protected virtual void OnDestroy()
        {
            _closeButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Set callback for close button click
        /// </summary>
        /// <param name="callback"></param>
        public void SetCloseButtonClickCallback(Action callback)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => callback());
        }

        /// <summary>
        /// Set action to be called when the UI is closed
        /// </summary>
        /// <param name="callback"></param>
        public void SetUIHideCompleteCallback(Action callback)
        {
            _uiHideCompleteCallback = callback;
        }

        /// <summary>
        /// Show the reward presentation UI with the specified value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [Button]
        public virtual async UniTask Show(string value)
        {
            _closeButton.interactable = false;
            var task = SetUIVisibility(true);
            _valueText.text = value;

            if (_spine != null && _spine.skeletonDataAsset != null && !string.IsNullOrEmpty(_animationIn))
            {
                _spine.AnimationState.ClearTracks();
                _spine.AnimationState.SetAnimation(0, _animationIn, false);
                if (!string.IsNullOrEmpty(_animationLoop))
                    _spine.AnimationState.AddAnimation(0, _animationLoop, true, 0);
            }

            await task;
            _closeButton.interactable = true;
        }

        public virtual async UniTask Show(double value)
        {
            await Show(value.ToString(NumberFormat));
        }

        /// <summary>
        /// Hide the reward presentation UI
        /// </summary>
        /// <returns></returns>
        public virtual async UniTask Hide()
        {
            SetUIVisibility(false).Forget();            
            _uiHideCompleteCallback?.Invoke();
        }

        /// <summary>
        /// Load Spine animation data
        /// </summary>
        /// <param name="skeletonDataAsset"></param>
        public virtual void LoadSpine(SkeletonDataAsset skeletonDataAsset)
        {
            _spine.enabled = true;
            _spine.skeletonDataAsset = skeletonDataAsset;
            _spine.Initialize(true);
            SetBoneFollower();
        }

        /// <summary>
        /// Set UI visibility with fade effect
        /// </summary>
        /// <param name="isVisible"></param>
        /// <returns></returns>
        [Button]
        protected virtual async UniTask SetUIVisibility(bool isVisible)
        {
            if (isVisible)
                gameObject.SetActive(true);

            _canvasGroup.blocksRaycasts = isVisible;
            _canvasGroup.interactable = isVisible;
            float startAlpha = _canvasGroup.alpha;
            float endAlpha = isVisible ? 1f : 0f;
            float elapsedTime = 0f;

            while (elapsedTime < _fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / _fadeDuration);

                _canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
                float c = isVisible ? _canvasGroup.alpha : _canvasGroup.alpha / 2f;
                _spine.color = new Color(c, c, c, c);

                await UniTask.Yield();
            }

            // 確保最終值精準
            _canvasGroup.alpha = endAlpha;
            _spine.color = new Color(endAlpha, endAlpha, endAlpha, endAlpha);

            if (!isVisible)
                gameObject.SetActive(false);

            if (!isVisible && _spine != null && _spine.AnimationState != null)
                _spine.AnimationState.ClearTracks();
        }

        /// <summary>
        /// Set up the BoneFollowerGraphic to follow the specified bone
        /// </summary>
        [Button]
        protected virtual void SetBoneFollower()
        {
            if (_spine.skeletonDataAsset == null || string.IsNullOrEmpty(_boneName))
                return;

            _boneFollowerGraphic.enabled = true;
            _boneFollowerGraphic.SetBone(_boneName);
            _boneFollowerGraphic.SkeletonGraphic = _spine;

            _boneFollowerGraphic.followLocalScale = _followLocalScale;
            _boneFollowerGraphic.followXYPosition = _followXYPosition;
            // 未來有需要再以變數控制
            _boneFollowerGraphic.followBoneRotation = false;
            _boneFollowerGraphic.followZPosition = false;
            _boneFollowerGraphic.followParentWorldScale = false;
            _boneFollowerGraphic.followSkeletonFlip = false;
        }
    }
}
