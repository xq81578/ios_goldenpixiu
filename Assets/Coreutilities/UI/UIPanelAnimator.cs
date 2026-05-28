using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

namespace Core.UI
{
    /// <summary>
    /// UI介面動畫
    /// </summary>
    public class UIPanelAnimator : MonoBehaviour
    {
        [Header("整體")]
        [SerializeField]
        private CanvasGroup _canvasGroup;
        
        [Header("黑底")]
        [SerializeField]
        private Image _background;

        [Header("介面")]
        [SerializeField]
        private Transform _panel;

        [Header("整體動畫時間")]
        [SerializeField]
        private float _duration = 0.3f;

        [Header("彈出縮放大小")]
        [SerializeField]
        private float _popupScale = 1.2f;
        
        [Header("彈出縮回時間")]
        [SerializeField]
        private float _popupDuration = 0.1f;
    
        [Header("彈出曲線")]
        [SerializeField]
        private Ease _popupEase = Ease.InSine;

        private void Start()
        {
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _background.color = Color.clear;
        }

        //開啟後動態
        //黑底漸層
        //介面先放大後再縮回
        public void PopUp(Action callback = null)
        {
            _panel.localScale = Vector3.zero;
            _canvasGroup.alpha = 1;
            _canvasGroup.blocksRaycasts = true;
            _background.DOFade(0.7f, _duration + _popupDuration).SetEase(_popupEase);
            _panel.DOScale(_popupScale, _duration).SetEase(_popupEase).OnComplete(() => 
            {
                _panel.DOScale(1f, _popupDuration).OnComplete(() => 
                {
                    callback?.Invoke();
                    _canvasGroup.interactable = true;
                });
            });
        }

        //關閉
        public void Close()
        {
            _background.color = Color.clear;
            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }

}
