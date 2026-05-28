using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slot001_GoldenPixiu
{
    public class Symbol : MonoBehaviour
    {
        [SerializeField]
        private SymbolDataSO _symbolRefData;
        [SerializeField, FoldoutGroup("Symbol設定")]
        private SkeletonGraphic _symbolSpine;
        [SerializeField, FoldoutGroup("Symbol設定")]
        private SkeletonGraphic _symbolBgSpine;
        [SerializeField, FoldoutGroup("Symbol設定")]
        private Image _symbolImage;
   
        [SerializeField, FoldoutGroup("Symbol設定")]
        private TextMeshProUGUI _symbolMoney;
        [SerializeField, FoldoutGroup("Symbol設定")]
        private TextMeshProUGUI _symbolMoneyGr;
        [SerializeField, FoldoutGroup("Symbol設定")]
        private TextMeshProUGUI _symbolMoneyBlue;
        [SerializeField, FoldoutGroup("Symbol設定")]
        private TextMeshProUGUI _symbolMoneyGreen;
        [SerializeField, FoldoutGroup("Symbol設定")]
        private TextMeshProUGUI _symbolMoneyPurple;

        //金币晃动动画参数
        [SerializeField, FoldoutGroup("金币晃动动画参数")][InfoBox("金币晃动动画时间")]
        private float _moneyShakeDuration = 0.3f;
        [SerializeField, FoldoutGroup("金币晃动动画参数")][InfoBox("金币晃动动画距离")]
        private float _moneyShakeDistance = 10.0f;
        [SerializeField, FoldoutGroup("金币晃动动画参数")][InfoBox("金币晃动动画次数")]
        private int _moneyShakeCount = 20;
        [SerializeField, FoldoutGroup("金币晃动动画参数")][InfoBox("金币晃动动画角度")]
        private float _moneyShakeRotation = 90f;




        public CellData CellData => _cellData;
        public bool IsCleared { get; private set; } = false;
        private SymbolData _symbolData;  //UI設定
        [ShowInInspector, ReadOnly]
        private CellData _cellData;  //數據資料
        private Color _normalMaskColor = new(1f, 1f, 1f);
        private Color _blackMaskColor = new(0.3f, 0.3f, 0.3f);
        private bool _isGray = false;
        
        private Tween winAnimation;


#if !RELEASE_BUILD
        // Cavas 的 RenderMode 需要設置為 ScreenSpace - Camera
        private void OnGUI()
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                // 將RectTransform的中心點轉換為螢幕座標
                Vector3 worldCenter = transform.position; // Transform 的世界座標中心點
                Camera cam = Camera.main;

                // 將中心點轉換為螢幕座標
                Vector3 screenCenter = cam.WorldToScreenPoint(worldCenter);
                GUIStyle style = new() { fontSize = 60, normal = { textColor = Color.blue } };
                Vector2 offset = new(36, 36);

                // 固定 GUI 的繪製位置，以中心點為基準，並確保 Y 軸翻轉
                // 調整 Rect 的位置讓它相對中心點有一個固定的偏移
                Rect rect = new(screenCenter.x - offset.x, Screen.height - screenCenter.y - offset.y, 200, 100);

                // 使用Label顯示文字，無論圖片大小，GUI 都會在中心點固定位置顯示
                GUI.Label(rect, _cellData.Name, style);
            }
        }
#endif

        public void Initialize(CellData data)
        {
            _symbolData = _symbolRefData.GetSymbolData(data.Id) as SymbolData;
            if (_symbolData == null)
            {
                LogUtils.LogError($"Can't find symbol data: {data.Id}");
                return;
            }
            _cellData = data;

            _symbolBgSpine.gameObject.SetActive(false);
            _symbolSpine.skeletonDataAsset = _symbolData.SymbolSpine;
            _symbolSpine.Initialize(true);
            SetNormal(true);


        }

        public int GetSymbolID()
        {
            return _symbolData.Id;
        }

        [Button]
        public void SetNormal(bool isinit = false)
        {
            SetNormalSprite();
            _symbolSpine.transform.localPosition = new Vector3(0f, 0f, 0);
            _symbolImage.enabled = true;
            _symbolSpine.gameObject.SetActive(false);
            _symbolBgSpine.gameObject.SetActive(false);
            winAnimation.Kill();
            transform.localScale = Vector3.one;
            if (_symbolData.Id == (int)SymbolEnum.NN)
            {
                // _symbolImage.gameObject.SetActive(false);
            }
            else
            {
                _symbolImage.gameObject.SetActive(true);
            }

            if (_cellData.IsScatter)
            {
                
                SetMoneyText(_cellData.WildMoney.ToString());
                _symbolMoneyGr.text = _cellData.WildMoney.ToString();
                SetMoneyActive(true);
                
                //由于动效资源没有中心对齐设置_symbolSpine的坐标为(2.5,1)，这样与静态图交替显示时才不会突兀
                _symbolSpine.transform.localPosition = new Vector3(2.5f, 1f, 0);

            }
            else //除了奖金符号外的其他符号
            {
                
                SetMoneyActive(false);
                
                _symbolMoneyGr.gameObject.SetActive(false);
                if (!isinit && !_isGray)
                {
                    SetAnimationLoop();
                }
                
            }
        }
        
        public void SetAnimationLoop()
        {
            if (_cellData.Id == 0) 
            {
                return;
            }
            _symbolImage.enabled = false;
            _symbolSpine.gameObject.SetActive(true);
            var aniName = _cellData.IsWild||_cellData.IsScatter? "loop":"loop2"; 
            _symbolSpine.AnimationState.AddAnimation(0, aniName, true, 0);
        }

        //为了保持所有符号呼吸一致提供的方法
        public void SetAnimationLoopfor()
        {
            if (_cellData.Id == 0) 
            {
                return;
            }
            _symbolImage.enabled = false;
            _symbolSpine.gameObject.SetActive(true);
            
           
            var aniName = _cellData.IsWild||_cellData.IsScatter? "loop":"loop2"; 

            //将"loop"到aniName的混合时间设置为0，即无混合,可以消除一些意外的动画效果(光影回退)
            _symbolSpine.AnimationState.Data.SetMix("loop", aniName, 0f); 
            _symbolSpine.AnimationState.SetAnimation(0, "loop", false);
            _symbolSpine.AnimationState.AddAnimation(0, aniName, true,0);
           
        }
        
        public void SetAnimationNormal()
        {
            if (_cellData.IsScatter && ResolutionManager.Instance.CheckIsOn(this.transform)) //奖金符号特殊处理
            {
                PlayScatterFx();
                AudioManager.PlayEffectByName("se_scatter_drop", checkRepeat: true, checkRepeatTime: 0.2f);
                // AudioManager.PlayEffectByName("ALL_SFX_ScatterDrop", checkRepeat: false,checkRepeatTime: 0f);
                
            }
            else if (_cellData.IsWild) //万能符号处理
            {
                PlayWildFx();
            }
            else if(_cellData.Id != 0) //其他符号处理
            {
                _symbolSpine.gameObject.SetActive(true);
                _symbolImage.enabled = false;
                var aniName = "land";
                _symbolSpine.AnimationState.SetAnimation(0, aniName, false);
                aniName = "loop2";
                _symbolSpine.AnimationState.AddAnimation(0, aniName, true, 0);
               
                
            }
        }

        [Button]
        //金额晃动动画
        public async Task PlayMoneyShakeFx()
        {
            //判断SetActive是否为true
            if (_symbolMoney.gameObject.activeSelf)
            {
                await _symbolMoney.transform.DOShakePosition(_moneyShakeDuration, _moneyShakeDistance, _moneyShakeCount, _moneyShakeRotation, false, true).AsyncWaitForCompletion();
            }else if (_symbolMoneyBlue.gameObject.activeSelf)
            {
                await _symbolMoneyBlue.transform.DOShakePosition(_moneyShakeDuration, _moneyShakeDistance, _moneyShakeCount, _moneyShakeRotation, false, true).AsyncWaitForCompletion();
            }else if (_symbolMoneyGreen.gameObject.activeSelf)
            {
                await _symbolMoneyGreen.transform.DOShakePosition(_moneyShakeDuration, _moneyShakeDistance, _moneyShakeCount, _moneyShakeRotation, false, true).AsyncWaitForCompletion();
            }else if (_symbolMoneyPurple.gameObject.activeSelf)
            {
                await _symbolMoneyPurple.transform.DOShakePosition(_moneyShakeDuration, _moneyShakeDistance, _moneyShakeCount, _moneyShakeRotation, false, true).AsyncWaitForCompletion();
            }
        }

        public void SetMoneyActive(bool active)
        {
            
            _symbolMoney.gameObject.SetActive(false);
            _symbolMoneyBlue.gameObject.SetActive(false);
            _symbolMoneyGreen.gameObject.SetActive(false);
            _symbolMoneyPurple.gameObject.SetActive(false);
            
            if (active){//根据当前的金额显示对应的颜色

                if (_cellData.WildMoney >= 1000)
                {
                    _symbolMoney.gameObject.SetActive(true);
                }
                else if (_cellData.WildMoney >= 100)
                {
                    _symbolMoneyPurple.gameObject.SetActive(true);
                }
                else if (_cellData.WildMoney >= 10)
                {
                    _symbolMoneyBlue.gameObject.SetActive(true);
                }
                else
                {
                    _symbolMoneyGreen.gameObject.SetActive(true);
                }
            }
            

        }

        public void SetMoneyText(string money)
        {
            _symbolMoney.text = money;
            _symbolMoneyBlue.text = money;
            _symbolMoneyGreen.text = money;
            _symbolMoneyPurple.text = money;
            
        }

        [Button]
        public void SetBlur()
        {
            SetBlurSprite();
            _symbolImage.enabled = true;
            _symbolSpine.gameObject.SetActive(false);
        }

        public void RotateEnd()
        {
            SetNormal();

            SetAnimationNormal();

        }


        public async UniTask DoMove(Vector3 endValue, float duration, Ease ease = Ease.Linear, bool isBlur = false, bool playEndEffect = false)
        {
            float smokeTriggerTime = duration - 0.5f;
            bool hasPlayedSmoke = false;
            Tween moveTween = null;
            moveTween = transform
                .DOLocalMove(endValue, duration)
                .SetEase(ease)
                .OnStart(() =>
                {
                    if (isBlur)
                    {
                        SetBlur();
                    }
                })
                .OnUpdate(() =>
                {
                    if (playEndEffect && !hasPlayedSmoke && moveTween.Elapsed() >= smokeTriggerTime)
                    {
                        hasPlayedSmoke = true;
                    }
                });
            await moveTween.AsyncWaitForCompletion().AsUniTask();
        }



        public void PlayScatterFx()
        {
            var aniName = "land";
            _symbolSpine.AnimationState.SetAnimation(0, aniName, false);
            aniName = "loop";
            _symbolSpine.AnimationState.AddAnimation(0, aniName, true, 0);
            _symbolSpine.gameObject.SetActive(true);
            _symbolImage.enabled = false;
            SetMoneyActive(true);
            _symbolMoney.enabled = true;
            _symbolMoneyGr.gameObject.SetActive(false);

            //动画完成后调用 SetNormal();
            // _symbolSpine.AnimationState.Complete += OnAnimationComplete;
        }
        
        public void PlayWildFx()
        {
            var aniName = "land";
            _symbolSpine.AnimationState.SetAnimation(0, aniName, false);
            aniName = "draw";
            _symbolSpine.AnimationState.AddAnimation(0, aniName, false, 0);
            aniName = "loop";
            _symbolSpine.AnimationState.AddAnimation(0, aniName, true, 0);
            _symbolSpine.gameObject.SetActive(true);
            _symbolImage.enabled = false;
           
        }

        private void OnAnimationComplete(Spine.TrackEntry trackEntry)
        {
            _symbolSpine.AnimationState.Complete -= OnAnimationComplete;
            SetNormal();
        }

        public async UniTask PlayScatterWinFx()
        {
            // var scatterWinName = $"win1x";
            // PlaySymbolAnimation(scatterWinName, true);
            _symbolImage.enabled = false;
            _symbolSpine.gameObject.SetActive(true);
            _symbolSpine.AnimationState.SetAnimation(0, "win", false);
            _symbolSpine.AnimationState.AddAnimation(0, "win", false,0);
            _symbolSpine.AnimationState.AddAnimation(0, "win", false,0);
            _symbolSpine.AnimationState.AddAnimation(0, "loop", true, 0);
            
            
        }

        public void StopScatterFx()
        {
            _symbolImage.enabled = true;
            _symbolSpine.gameObject.SetActive(false);
        }

   


        private string GetClearAnimationName()
        {
            var clearName = "winnoframe";
       
            return clearName;
        }

        private void PlaySymbolAnimation(string animationName, bool isLoop = true)
        {
            _symbolImage.enabled = false;
            _symbolSpine.AnimationState.SetAnimation(0, animationName, isLoop);
            _symbolSpine.gameObject.SetActive(true);
        }

        private void PlayClearSymbolAnimation()
        {
            var clearName = GetClearAnimationName();
            PlaySymbolAnimation(clearName, false);
        }


        private void SetNormalSprite()
        {
            _symbolImage.sprite = _symbolData.NormalSprite;
            _symbolImage.SetNativeSize();
        }

        private void SetBlurSprite()
        {
            _symbolImage.sprite = _symbolData.BulrSprite;
            _symbolImage.SetNativeSize();
        }

    
   
        // 变更颜色
        public void ChangeColor(bool isGray = false)
        {
          
            _isGray = isGray;
            Color color = isGray? Color.gray : Color.white;
            Image[] textures = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < textures.Length; ++i)
            {
                Image texture = textures[i];
                texture.DOColor(color, 0.1f);
            }

            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; ++i)
            {
                TMP_Text text = texts[i];
                // 字体修改alpha
                if (color == Color.gray)
                    color = new Color(color.r, color.g, color.b, 0.4f);

                text.DOColor(color, 0.1f);
            }

            SkeletonGraphic[] skeles = GetComponentsInChildren<SkeletonGraphic>(true);
            for (int i = 0; i < skeles.Length; ++i)
            {
                SkeletonGraphic skeleton = skeles[i];
                skeleton.DOColor(color, 0.1f);
            }
            
            SetNormal();
        }

        //设置奖金符号上的 数字的颜色
        public void SetTextColor(bool isGray = false)
        {
            // ------------------------这是之前的数字统一变为一个灰色
            // //隐藏带颜色的数字文本
            // SetMoneyActive(!isGray);
            // //显示灰色数字文本
            // _symbolMoneyGr.gameObject.SetActive(isGray);

            // ------------------------现在改为设置颜色数字的透明度
            // 隐藏带颜色的数字文本
            _symbolMoney.alpha = isGray? 0.5f : 1.0f;
            _symbolMoneyBlue.alpha = isGray? 0.5f : 1.0f;
            _symbolMoneyGreen.alpha = isGray? 0.5f : 1.0f;
            _symbolMoneyPurple.alpha = isGray? 0.5f : 1.0f;

        }


        public void PlayWinAnimator()
        {
            // winAnimation = transform.DOScale(1.1f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
            string aniname = "win";
            if (_cellData.IsWild)
            {
                aniname = "draw";
                // winAnimation = transform.DOScale(1.1f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
            }
            
            _symbolBgSpine.gameObject.SetActive(true);
            _symbolBgSpine.AnimationState.SetAnimation(0, "light2", false);
            _symbolSpine.AnimationState.SetAnimation(0, aniname, true);
        }

        
        public void ShoScatterWinwWin(int wincount = 3,bool ischangeColoer = true)
        {
            //wincount ：代表执行win特效几次 ischangeColoer：代表是否变更颜色
            if (_cellData.IsScatter)
            {
                DOVirtual.DelayedCall(0.5f, () => {
                    _symbolImage.enabled = false;
                    _symbolSpine.gameObject.SetActive(true);
                    _symbolSpine.AnimationState.SetAnimation(0, "win", false);
                    for (int i = 0; i < wincount-1; i++)
                    {
                        _symbolSpine.AnimationState.AddAnimation(0, "win", false, 0);
                    }
                    if(wincount == 3){_symbolSpine.AnimationState.AddAnimation(0, "win_loop", true, 0);}
                    else
                    { //不是三次的情况就变为静态图片展示
                        
                        _symbolImage.enabled = true;
                        //淡出_symbolSpine
                        _symbolSpine.DOFade(0.0f, 0.8f).OnComplete(() =>
                        {
                            _symbolSpine.gameObject.SetActive(false);
                            _symbolSpine.DOFade(1, 0).SetUpdate(true);
                        });
                        
                        
                    }
                    
                });
            }
            else if (ischangeColoer)
            {
                ChangeColor(true);
            }
        }


    }
}
