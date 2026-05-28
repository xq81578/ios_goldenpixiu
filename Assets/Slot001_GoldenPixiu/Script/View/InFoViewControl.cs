using DG.Tweening;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Components;

#if !DISABLE_SRDEBUGGER
using SRDebugger;
#endif

//该类专门用于处理横屏的跑马灯
namespace Slot.Common.Bottom
{
    public class InFoViewControl : MonoBehaviour
    { 
        [SerializeField, FoldoutGroup("Marquee相關")]
        private Transform marqueeGroup;
        

        [SerializeField, FoldoutGroup("Marquee相關")]
        private float marqueeDuration = 3f;
        
        [SerializeField, FoldoutGroup("Marquee相關")]
        private float marqueeDelay = 1f;

        private bool _isSpin;
        private double _winValue;
        private Tween _marqueeTween = null;
        private int _marqueeIdx = -1;
        private float _marqueeMaskWidth;
        private List<Image> _marqueeNormalImages = new List<Image>();

        private void Start()
        {
            _marqueeMaskWidth = marqueeGroup.transform.GetComponent<RectTransform>().rect.width;
            for (int i = 0; i < marqueeGroup.childCount; i++)
            {
                Image image = marqueeGroup.GetChild(i).GetComponent<Image>();
                image.gameObject.SetActive(false);

                // 沒有掛圖片跟多語系
                if (image == null ||
                    image.sprite == null && image.gameObject.GetComponent<LocalizeSpriteEvent>().AssetReference.IsEmpty)
                {
                    continue;
                }

                _marqueeNormalImages.Add(image);
            }

            PlayMarquee();
            
        }

        //播放跑馬燈
        private void PlayMarquee()
        {
            
            _marqueeIdx++;

            // 不是第一個跑馬燈，要先關閉前一個
            if (_marqueeIdx > 0)
            {
                _marqueeNormalImages[_marqueeIdx - 1].gameObject.SetActive(false);
            }

            // 已經播到最後一個，要回播第一個
            if (_marqueeIdx > marqueeGroup.childCount - 1)
            {
                _marqueeIdx = 0;
            }

            _marqueeNormalImages[_marqueeIdx].gameObject.SetActive(true);

            //跑馬燈沒超出範圍，原地顯示不跑動
            if (_marqueeNormalImages[_marqueeIdx].GetComponent<RectTransform>().rect.width < _marqueeMaskWidth)
            {
                _marqueeNormalImages[_marqueeIdx].transform.localPosition = Vector2.zero;
                _marqueeTween = _marqueeNormalImages[_marqueeIdx].transform.DOLocalMoveX(0, 0).SetDelay(marqueeDuration + marqueeDelay).SetEase(Ease.Linear);
            }
            else
            {
                //等待一秒後才開始跑動
                _marqueeNormalImages[_marqueeIdx].transform.localPosition = new Vector2(((_marqueeNormalImages[_marqueeIdx].transform.GetComponent<RectTransform>().rect.width - _marqueeMaskWidth) / 2) + 50, 0);
                _marqueeTween = _marqueeNormalImages[_marqueeIdx].transform.DOLocalMoveX(-(_marqueeNormalImages[_marqueeIdx].transform.GetComponent<RectTransform>().rect.width + _marqueeMaskWidth) / 2, marqueeDuration).SetDelay(marqueeDelay).SetEase(Ease.Linear);
            }
            _marqueeTween.OnComplete(PlayMarquee);
        }
    }
}