using System;
using System.Text;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slot.Common.UI
{
    public class BottomBarView : MonoBehaviour
    {
        public PlatformType PlatformType;

        [FoldoutGroup("Balance按鈕相關")]
        [SerializeField, FoldoutGroup("Balance按鈕相關")]
        private TextMeshProUGUI balanceValueText;
        [SerializeField, FoldoutGroup("Balance按鈕相關")]
        private Image balanceCurrencyImage;
        [SerializeField, FoldoutGroup("Balance按鈕相關")]
        private TextMeshProUGUI balanceCurrencyText;
        [FoldoutGroup("Balance按鈕相關")]
        public Button balanceCurrencyButton;

        [FoldoutGroup("Bet按鈕相關")]
        [SerializeField, FoldoutGroup("Bet按鈕相關")]
        private TextMeshProUGUI betCurrencyText;
        [SerializeField, FoldoutGroup("Bet按鈕相關")]
        private TextMeshProUGUI betValueText;
        [FoldoutGroup("Bet按鈕相關")]
        public Button betBtn;

        [FoldoutGroup("Auto按鈕相關")]
        [FoldoutGroup("Auto按鈕相關")]
        public Button AutoBtn;
        [SerializeField, FoldoutGroup("Auto按鈕相關")]
        private Sprite autoOffSprite;
        [SerializeField, FoldoutGroup("Auto按鈕相關")]
        private Sprite autoOnSprite;
        [SerializeField, FoldoutGroup("Auto按鈕相關")]
        private TextMeshProUGUI autoTotalSpinsValueText;
        [FoldoutGroup("Auto按鈕相關")]
        public Button autoSpinStopBtn;
        [SerializeField, FoldoutGroup("Auto按鈕相關")]
        private Image _infinitasImage;

        /// <summary>
        /// 隨著auto一起被開關的物件集合
        /// </summary>
        List<GameObject> _autoActiveGos = new List<GameObject> ();

        [FoldoutGroup("Spin按鈕相關")]
        [FoldoutGroup("Spin按鈕相關")]
        public Button spinBtn;
        [FoldoutGroup("Spin按鈕相關")]
        public Button quickStopBtn;
        [SerializeField, FoldoutGroup("Spin按鈕相關")]
        private Sprite spinIdleSprite;
        [SerializeField, FoldoutGroup("Spin按鈕相關")]
        private Sprite spinStopSprite;

        [SerializeField,FoldoutGroup ("Spin按鈕相關")]
        private SkeletonGraphic _spinBtnAnim;

        [FoldoutGroup("Win相關")]
        [SerializeField, FoldoutGroup("Win相關")]
        //設定WinValueText數字滾動的時間
        private float winValueTweenDuration = 0.25f;
        [SerializeField, FoldoutGroup("Win相關")]
        private TextMeshProUGUI winCurrencyText;
        [SerializeField, FoldoutGroup("Win相關")]
        private TextMeshProUGUI winValueText;
        [SerializeField, FoldoutGroup("Win相關")]
        private CanvasGroup winCanvasGroup;
        [SerializeField, FoldoutGroup("Win相關")]        
        private Image _winBgImage;
        [SerializeField, FoldoutGroup("Win相關")]
        private SkeletonGraphic _winEffectSpine;
        [SerializeField, FoldoutGroup("Win相關")]
        private string _winEffectName = "animation";

        [FoldoutGroup("Turbo相關")]
        [FoldoutGroup("Turbo相關")]
        public Button turboBtn;
        [SerializeField, FoldoutGroup("Turbo相關")]
        private Sprite turboQuickSprite;
        [SerializeField, FoldoutGroup("Turbo相關")]
        private Sprite turboOffSprite;

        [FoldoutGroup("Info相關")]
        [FoldoutGroup("Info相關")]
        public Button _infoButton;

        [FoldoutGroup("純文字相關")]
        [SerializeField, FoldoutGroup("純文字相關")]
        private TextMeshProUGUI transtionText;
        [SerializeField, FoldoutGroup("純文字相關")]
        private TextMeshProUGUI versionText;
        [SerializeField, FoldoutGroup("純文字相關")]
        private TextMeshProUGUI _playTimeText;
        [SerializeField, FoldoutGroup("純文字相關")]
        private TextMeshProUGUI _netText;

        [FoldoutGroup("Menu相關")]
        [FoldoutGroup("Menu相關")]
        public Button _menuButton;

        public string CurrencySymbol;
        private Tween _winValueTween = null;
        private StringBuilder _stringBuilder = new StringBuilder();
        private RectTransform _winValueRefreshRectTransform;

        bool _isAuto = false;

        bool _isNormal = true;

        /// <summary>
        /// 啟用spine後會把btn的target指定到spineGraphic上, 這樣hover動畫才會作用在spine上
        /// 這個時候btn.sprite會為null, 因為btn.sprite是btn.target as來的
        /// 所以用這個bool來記錄目前btn是否有spine動畫
        /// </summary>
        bool _isBtnHasSpine = false;

        string _spinIdleAnimName;
        string _spinStopAnimName;
        string _spinClickAnimName;


        private void Start()
        {
            PrepareAutoActiveGos ();

            //避免圖片空白處接收點擊事件，如果無效，要將 Texture 設定 Read/Write 打勾
            _winValueRefreshRectTransform = winValueText.transform.parent.GetComponent<RectTransform> ();

            // spinBtn.image.alphaHitTestMinimumThreshold = 0.2f;

            quickStopBtn.interactable = false;
            quickStopBtn.image.raycastTarget = false;

            autoTotalSpinsValueText.text = "";
            transtionText.text = "";
            OnSetVerText();
        }

        private void PrepareAutoActiveGos ()
        {
            _autoActiveGos = new List<GameObject> ();
            if (autoSpinStopBtn != null)
            {
                _autoActiveGos.Add (autoSpinStopBtn.gameObject);
            }

            if (autoTotalSpinsValueText != null)
            {
                _autoActiveGos.Add (autoTotalSpinsValueText.gameObject);
            }

            if (_infinitasImage != null)
            {
                _autoActiveGos.Add (_infinitasImage.gameObject);
            }

            _autoActiveGos.ForEach (go => go.SetActive (false));
        }

        public void SetSpinSprite(Sprite idleSprite, Sprite stopSprite)
        {
            if (idleSprite != null)
            {
                spinIdleSprite = idleSprite;

                spinBtn.image.SetNativeSize ();
                spinBtn.image.sprite = spinIdleSprite;
                spinBtn.transform.Find("Arrow").GetComponent<CanvasGroup>().alpha = 1;
                
            }
            
            if (stopSprite != null)
            {
                spinStopSprite = stopSprite;
                
                spinBtn.image.SetNativeSize ();

                quickStopBtn.image.sprite = spinStopSprite;
                
            }
        }

        public void SetSpinSplineAsset (SkeletonDataAsset spinAssetData, string spinIdleAnimName, string spinStopAnimName, string spinClickAnimName)
        {
            //如果該view本身就不支援spine動畫, 就回到圖片更新模式
            if (_spinBtnAnim != null)
            {
                _isBtnHasSpine = true;
                _spinIdleAnimName = spinIdleAnimName;
                _spinStopAnimName = spinStopAnimName;
                _spinClickAnimName = spinClickAnimName;

                _spinBtnAnim.enabled = true;
                _spinBtnAnim.raycastTarget = false;
                _spinBtnAnim.skeletonDataAsset = spinAssetData;
                _spinBtnAnim.Initialize (true);
                _spinBtnAnim.AnimationState.SetAnimation (0, _spinIdleAnimName, true);

                //把原本的圖片隱藏掉
                spinBtn.image.color = new Color (1, 1, 1, 0);
                autoSpinStopBtn.image.color = new Color (1, 1, 1, 0);

                UpdateAnimStatus ();
            }
        }

        public void OnSetBottomUiNormal(bool isAuto, bool isFreeGame)
        {
            _isAuto = isAuto;
            _isNormal = true;

            spinBtn.interactable = true;
            if (PlatformType == PlatformType.COMBO)
            {
                spinBtn.transform.Find("Arrow").GetComponent<CanvasGroup>().alpha = 1;
                spinBtn.image.sprite = spinIdleSprite;
                // spinBtn.image.alphaHitTestMinimumThreshold = 0.2f;
            }

            quickStopBtn.interactable = false;
            quickStopBtn.image.raycastTarget = false;
            
            bool interactable = !isAuto && !isFreeGame;
            AutoBtn.interactable = interactable;
            betBtn.interactable = interactable;
            _infoButton.interactable = interactable;

            UpdateAnimStatus ();
        }

        //鎖定
        public void OnSetBottomUiLock(bool isAuto)
        {
            _isAuto = isAuto;
            _isNormal = false;

            UpdateAnimStatus ();

            if (spinStopSprite != null)
            {
                spinBtn.image.sprite = spinStopSprite;
                spinBtn.transform.Find("Arrow").GetComponent<CanvasGroup>().alpha = 0;
            }

            quickStopBtn.image.raycastTarget = true;
            quickStopBtn.interactable = true;
            betBtn.interactable = false;
            AutoBtn.interactable = false;
            _infoButton.interactable = false;
        }

        List<string> animNames = new List<string> ();

        /// <summary>
        /// 狀態變更時通知動畫更新
        /// </summary>
        private void UpdateAnimStatus () 
        {
            if (!_isBtnHasSpine)
            {
                return;
            }

            animNames.Clear ();

            if (_isNormal) 
            {
                if (_isAuto == false) 
                {
                    animNames.Add (_spinIdleAnimName);
                }
            }
            else 
            {
                if (_isAuto == false)
                {
                    animNames.Add (_spinClickAnimName);
                }

                animNames.Add (_spinStopAnimName);
            }

            ProcessAnimSequence (_spinBtnAnim, animNames);
        }

        /// <summary>
        /// 點擊要先撥放點擊再撥放loop, 所以傳入組合動畫名稱
        /// key用來辨識是哪個anim在處理, 方便debug
        /// </summary>
        private void ProcessAnimSequence (SkeletonGraphic anim, List<string> animNames)
        {
            Spine.AnimationState animationState = anim.AnimationState;
            TrackEntry spinTrack = animationState.GetCurrent (0);

            if (spinTrack != null)
            {
                string oldAnim = spinTrack.Animation.Name;

                if (animNames.Count > 0)
                {
                    if (animNames.Contains (oldAnim) == false)
                    {
                        for (int i = 0; i < animNames.Count; i++)
                        {
                            //最後那個動畫才需要loop
                            bool needLoop = i == animNames.Count - 1;

                            if (i == 0) 
                            {
                                animationState.SetAnimation (0, animNames[i], needLoop);
                            } 
                            else 
                            {
                                animationState.AddAnimation (0, animNames[i], needLoop, 0);
                            }
                        }
                    }
                }
            }
            else
            {
                LogUtils.LogError ($"BottomBarView UpdateAnimStatus 初始化未成功");
            }
        }

        //改變投注額
        public void OnChangeBet(double bet)
        {
            betValueText.text = GetFormattedCurrencyString(bet, Bottom_Define.MoneyFormat);
        }

        public void OnChangeBet(string bet)
        {
            betValueText.text = bet;
        }

        //變更錢包
        public void OnChangeBalance(double balance, ECurrency currency, CurrencySettingSO currencySetting, bool CheckChangeCurrency)
        {
            balanceValueText.text = GetFormattedCurrencyString(balance, Bottom_Define.MoneyFormat);

            if (balanceCurrencyButton != null )
            {
                bool setDefault = PlatformType == PlatformType.NONE || PlatformType == PlatformType.COMBO;
                if (setDefault)
                {
                    balanceCurrencyButton.image.sprite = currencySetting.GetCurrencyChangeSprite(
                        currency,
                        !CheckChangeCurrency);
                }

                currencySetting.SetCurrencyUI(currency, balanceCurrencyImage, balanceCurrencyText, balanceValueText, false, setDefault);
            }

            // 一併設定 Win、Bet 的貨幣符號
            if (winCurrencyText != null)
                winCurrencyText.text = currencySetting.GetCurrencyText(currency);
            if (betCurrencyText != null)
                betCurrencyText.text = currencySetting.GetCurrencyText(currency);
    
            //Currency currencyData = currencySetting.GetCurrency(currency);
            // 儲存幣值資料 ??
            // EventManager.SetData(Bottom_EventData.GameCurrencyData, currencyData);
        }

        //更新 Turbo 按鈕圖
        public void SetTurboSprite(Bottom_TurboType turboType)
        {
            var image = turboBtn.image;
            switch (turboType)
            {
                case Bottom_TurboType.TurboOff:
             
                    image.sprite = turboOffSprite;
                    break;
                case Bottom_TurboType.TurboQuick:
            
                    image.sprite = turboQuickSprite;
                    break;
            }
        }

        //設定贏分
        public void OnSetWin(double currentWin, double targetWin)
        {
            bool isShowWin = targetWin > 0;
            winCanvasGroup.alpha = isShowWin ? 1 : 0;

            if (!isShowWin)
            {
                SetNetText(0);
                return;
            }
            
            //用Tween做出winValueText有數字往上滾動的效果
            _winValueTween?.Complete();

            _winValueTween = DOTween.To(() => currentWin, x => currentWin = x, targetWin, winValueTweenDuration)
                .OnUpdate(() => winValueText.text = GetFormattedCurrencyString(currentWin, Bottom_Define.MoneyFormat))
                .OnComplete(() =>
                {
                    if (isShowWin && gameObject.activeSelf)
                    {
                        new UISetWinEndEvent().Publish(this);
                    }
                })
                .SetEase(Ease.Linear);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_winValueRefreshRectTransform);
        }

        //增加贏分
        public void OnAddWin(double currentWin, double targetWin)
        {
            bool isShowWin = targetWin > 0;
            winCanvasGroup.alpha = isShowWin ? 1 : 0;

            if (!isShowWin)
            {
                return;
            }

            //用Tween做出winValueText有數字往上滾動的效果
            _winValueTween?.Complete();

            _winValueTween = DOTween.To(() => currentWin, x => currentWin = x, targetWin, winValueTweenDuration)
                .OnUpdate(() => winValueText.text = GetFormattedCurrencyString(currentWin, Bottom_Define.MoneyFormat))
                .SetEase(Ease.Linear);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_winValueRefreshRectTransform);

            _winValueTween.OnComplete(() => new UIAddWinTweenEndEvent().Publish(this));
        }

        //重置贏分
        public void OnResetWin()
        {
            winValueText.text = "0.00";
            winCanvasGroup.alpha = 0;
        }

        //更新自動轉次數
        public void OnUpdateAutoCount(bool isAuto, bool isFG, int autoSpinCount)
        {
            autoTotalSpinsValueText.text = "";
            if (_infinitasImage != null)
                _infinitasImage.enabled = false;

            // if (PlatformType == PlatformType.COMBO)
            // {
            //     spinBtn.transform.Find("Arrow").gameObject.SetActive(true);
            // }

            //如果現在是auto開啟, auto btn要換圖
            if (isAuto)
            {
                if (autoOnSprite != null)
                    AutoBtn.image.sprite = autoOnSprite;

                //判斷有沒有開啟這個選項, 並且不在FG中
                if (autoSpinCount >= 0 && !isFG)
                {
                    if (autoSpinCount == int.MaxValue)
                    {
                        if (_infinitasImage != null && !_infinitasImage.enabled)
                            _infinitasImage.enabled = true; 
                    }
                
                    autoTotalSpinsValueText.text = autoSpinCount == int.MaxValue ? string.Empty : (autoSpinCount-1).ToString();
                    if (PlatformType == PlatformType.COMBO)
                    {
                        spinBtn.transform.Find("Arrow").GetComponent<CanvasGroup>().alpha = 0;
                    }
                }
            }
            else
            {
                AutoBtn.image.sprite = autoOffSprite;
            }

            bool oldActive = autoSpinStopBtn.gameObject.activeSelf;
            bool newActive = !isFG && isAuto;

            if (newActive != oldActive) 
            {
                _autoActiveGos.ForEach (go => go.SetActive (newActive));

                autoTotalSpinsValueText.gameObject.SetActive (newActive);
            }
        }


        //更新 Trans Text
        public void OnUpdateTransText(string transText)
        {
            string txnId = transText;
            transtionText.text = txnId == "" ? "" : $"Transaction {txnId}";  // TODO: 多語系
        }

        //更新 Ver Text
        public void OnSetVerText()
        {
            versionText.text = ServiceUtils.GetVersionText();
        }

        //鎖定 Spin 鈕
        public void OnSpinLock(bool isLock)
        {
            spinBtn.interactable = !isLock;
            quickStopBtn.gameObject.SetActive(!isLock && spinBtn.image == spinStopSprite);
        }

        public void OnSpinClick()
        {
     
        }

        public void SetWinSpine(SkeletonDataAsset skeletonDataAsset)
        {
            if (_winEffectSpine == null)
                return;

            _winEffectSpine.skeletonDataAsset = skeletonDataAsset;
            _winEffectSpine.Initialize(true);
        }

        public async UniTask PlayWinAnimation()
        {
            if (_winEffectSpine.SkeletonDataAsset == null)
                return;

            _winEffectSpine.gameObject.SetActive(true);
            _winEffectSpine.AnimationState.ClearTracks();
            await _winEffectSpine.AnimationState.SetAnimation(0, _winEffectName, false).AsyncWaitForCompletion();
            _winEffectSpine.gameObject.SetActive(false);
        }

        //按下停止自動轉按鈕
        public void OnAutoStopClick()
        {
            _autoActiveGos.ForEach (go => go.SetActive (false));

            _isAuto = false;
        }

        public void OnSetCurrencyButton(bool enabled)
        {
            // 20250903 關閉換幣功能，未來要開啟再打開
            return;
            if (balanceCurrencyButton == null) return;
            balanceCurrencyButton.enabled = enabled;
        }

        public void SetSpinIdleSprite(Sprite sprite)
        {
            spinIdleSprite = sprite;

            spinBtn.image.sprite = spinIdleSprite;
            spinBtn.transform.Find("Arrow").GetComponent<CanvasGroup>().alpha = 1;
        }

        public void SetPlayTimeText(string playTime)
        {
            if (_playTimeText == null)
                return;

            _playTimeText.text = playTime;
        }

        public void SetNetText(double value)
        {
            if (_netText == null)
                return;

            _netText.text = GetFormattedCurrencyString(value, Bottom_Define.MoneyFormat, CurrencySymbol);
        }

        public void SetWinPosY(float posY)
        {
            if (winCanvasGroup == null)
                return;

            if (winCanvasGroup.TryGetComponent<RectTransform>(out var rectTransform))
            {
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, posY);
            }
        }

        public void SetWinBgImage(Sprite sprite)
        {
            if (_winBgImage == null || sprite == null)
                return;

            _winBgImage.sprite = sprite;
        }

        public void SetWinSpine(SkeletonDataAsset skeletonDataAsset, string animationName)
        {
            _winEffectSpine.skeletonDataAsset = skeletonDataAsset;
            _winEffectSpine.Initialize(true);

            if (!string.IsNullOrEmpty(animationName))
                _winEffectName = animationName;
        }
        
        public void SetAutoTurboActive(bool isAutoActive, bool isTurboActive)
        {
            AutoBtn.interactable = isAutoActive;
            AutoBtn.image.enabled = isAutoActive;
            turboBtn.interactable = isTurboActive;
            turboBtn.image.enabled = isTurboActive;
        }

        /// <summary>
        /// 使用 StringBuilder 來轉換貨幣字串，減少 GC
        /// </summary>
        private string GetFormattedCurrencyString(double value, string format, string currencySymbol = "")
        {
            _stringBuilder.Clear();
            if (!string.IsNullOrEmpty(currencySymbol))
            {
                _stringBuilder.Append(currencySymbol).Append(" ");
            }
            _stringBuilder.Append(ServiceUtils.ToCurrentString(value, format));
            
            return _stringBuilder.ToString();
        }
    }
}
