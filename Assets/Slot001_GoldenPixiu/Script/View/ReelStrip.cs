using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Spine.Unity;
using System.Linq;

namespace Slot001_GoldenPixiu
{
    public class ReelStrip : MonoBehaviour
    {
        [SerializeField, FoldoutGroup("符號設定")]
        [InfoBox("轉輪空間")]
        private int _reelSpaceSize = 5;
        [SerializeField, FoldoutGroup("符號設定")]
        [InfoBox("符號起始位置")]
        private Vector2 _startPosition;
        [SerializeField, FoldoutGroup("符號設定")]
        [InfoBox("符號大小")]
        private Vector2 _symbolSize; // 現在是二維向量，支援寬度和高度的單獨設置
        [SerializeField, FoldoutGroup("符號設定")]
        [InfoBox("符號偏移")]
        private Vector2 _offset;
        [SerializeField]
        [InfoBox("符號排列方向")]
        private ScrollDirection _arrangeDirection = ScrollDirection.TopToBottom;
        [SerializeField]
        private RectMask2D _mask;
        [SerializeField]
        [InfoBox("掉落時間")]
        private float _dropDuration = 0.15f;
        [SerializeField]
        [InfoBox("結束時顯示幾個轉輪帶符號")]
        private int _endShowReelStripCount = 5;
        [SerializeField]
        [InfoBox("移動結束時是否有回彈及煙霧效果")]
        private bool _hasMoveEndEffect = true;
        [SerializeField, FoldoutGroup("瞇牌")]
        private float _revealDuration = 2f;
        [SerializeField, FoldoutGroup("瞇牌")]
        private GameObject _preRevealFx;

        [SerializeField, FoldoutGroup("收集奖金符号")]
        private List<GameObject> _panels = new();
        [SerializeField, FoldoutGroup("收集奖金符号")]
        private float _moveDuration = 0.3f;

        [SerializeField, FoldoutGroup("收集奖金符号数字动效枚举")]
        private DG.Tweening.Ease _moveEndEffectType = DG.Tweening.Ease.OutSine;
        
        

        private GenericPool<Symbol> _symbolPool;
        private List<Symbol> _symbols = new();
        private ReelData _currentReelStrip;
        private float _rotateSpeed = 30.0f;
        private bool _isRotating = false;
        private bool _isMoving = false;
        private int _bottomOutOfSize = 0;
        private bool _preReveal = false;
        private bool _isFreeGame = false;

        public float RotateSpeed { get => _rotateSpeed; set => _rotateSpeed = value; }

        public void Initialization(GenericPool<Symbol> symbolPool)
        {
            _symbolPool = symbolPool;
        }

        public void InitializeSymbols(ReelData reelData)
        {
            DestroyAllSymbols();

            for (int i = 0; i < reelData.Cells.Count; i++)
            {
                CellData cellData = reelData.Cells[i];

                var symbol = _symbolPool.GetObject(true, transform);
                symbol.Initialize(cellData);
                symbol.transform.localPosition = CalculateSymbolPosition(i);
                _symbols.Add(symbol);
                symbol.SetAnimationLoop();
                symbol.ChangeColor();
            }
            _bottomOutOfSize = 0;
        }

        //设置所有符号呼吸一致
        public void SetAnimationLoop()
        {
            foreach (var symbol in _symbols)
            {
                symbol.SetAnimationLoopfor();
            }
            
        }

        #region 轉輪控制 開始/停止/消除/掉落/填充符號

        public void StartRotation(ReelData reelData)
        {
            if (_isRotating)
            {
                return; // 如果已經在旋轉，則不再啟動新的旋轉
            }
            _currentReelStrip = reelData;
            _isRotating = true;
            _mask.enabled = true;
            RotateSymbols().Forget();
        }

        public async UniTask StopRotation(ReelData endReelStrip, int endPositions, bool isForceStop, bool preReveal = false, bool playSound = true,bool isFreeGame = false) 
        {
            _isFreeGame = isFreeGame;
            if (!_isRotating)
            {
                return; // 如果不在旋轉，則不進行停止操作
            }
            await ShowPreRevealFx(preReveal);
            _isRotating = false;
            _currentReelStrip = endReelStrip;
            await RotateToEndPosition(endPositions, isForceStop);
            if (playSound)
            {
                
                if(ResolutionManager.Instance.CheckIsOn(this.transform))
                {
                    LogUtils.Log("播放停止音效---");
                    // AudioManager.StopEffectByName("se_reel_stop");
                    // AudioManager.PlayEffectByName("se_reel_stop", checkRepeat: true, checkRepeatTime: 0.1f);
                }
            }
            foreach (var symbol in _symbols) // 切換回正常圖片
            {
                symbol.RotateEnd();
            }
            if (_hasMoveEndEffect)
            {
                await MoveEndBounceEffect();
            }
            _mask.enabled = false;
            ShowPreRevealFx(false,true).Forget();
        }

        public async UniTask ForeceStopRotation(ReelData endReelStrip, int endPositions,bool isFreeGame = false)
        {
            LogUtils.Log("停止操作---------------------------");
            _isFreeGame = isFreeGame;
            _isRotating = false;
            _currentReelStrip = endReelStrip;
            _mask.enabled = true;
            await RotateToEndPosition(endPositions, true);
            foreach (var symbol in _symbols) // 切換回正常圖片
            {
                symbol.RotateEnd();
            }
            if (_hasMoveEndEffect)
            {
                await MoveEndBounceEffect();
            }
            _mask.enabled = false;
        }


        [Button]
        //奖金符号>=5个时，奖金符号显示完成后的金额收集效果
        public async UniTask MoveWinScorePanels()
        {
            for (int i = 0; i < _symbols.Count; i++)
            {
                var panel = _panels[i];
                var symbol = _symbols[i];

                if (symbol.CellData.Id != (int)SymbolEnum.SS) 
                {
                    panel.gameObject.SetActive(false);
                    continue;
                }
                panel.gameObject.SetActive(true);
                //panel上的文本全部隐藏
                TMP_Text[] figureText = panel.GetComponentsInChildren<TMP_Text>(true);
                foreach (var text in figureText) text.gameObject.SetActive(false);

                //隐藏粒子效果
                SkeletonGraphic[] sks = panel.GetComponentsInChildren<SkeletonGraphic>(true);
                for (int j = 0; j < sks.Length; j++)
                {
                    sks[j].gameObject.SetActive(false);
                }

                //符号上的金额晃动动画
                await symbol.PlayMoneyShakeFx();

                //符号上的 数字变灰色
                symbol.SetTextColor(true);

                //根据金额决定哪个颜色的文本显示
                double wildMoney = symbol.CellData.WildMoney;
                foreach (var text in figureText)
                {
                    bool isActive = false;
                    
                    // 根据不同的WildMoney值和文本名称设置不同的文本显示状态
                    if (wildMoney >= 1000 && text.name == "FigureText")
                    {
                        isActive = true;
                    }
                    else if (wildMoney >= 100 && wildMoney < 1000 && text.name == "FigureTextPurple")
                    {
                        isActive = true;
                    }
                    else if (wildMoney >= 10 && wildMoney < 100 && text.name == "FigureTextBlue")
                    {
                        isActive = true;
                    }
                    else if (wildMoney >= 0 && wildMoney < 10 && text.name == "FigureTextGreen")
                    {
                        isActive = true;
                    }
                    
                    text.text = wildMoney.ToString();
                    text.gameObject.SetActive(isActive);
                }
                

                //获取panel当前世界坐标
                var panelWorldPos = panel.transform.position;
                //生成一个x位于屏幕中心y距离底部50的世界坐标
                float py = Screen.width / Screen.height >= 1?-447:-492;
                var targetWorldPos = new Vector3(0, py, panelWorldPos.z);
                
                //动画将panel的坐标设置为targetWorldPos，并且在完成后将坐标重置为panelWorldPos
                Tween moveTween = null;
                Vector3 originalScale = panel.transform.localScale; // 记录原始缩放值
                Vector3 targetScale = Vector3.one*0.7f; // 目标缩放值，例如缩小到零

                //播放收集音效
                if (ResolutionManager.Instance.CheckIsOn(this.transform))
                {
                    AudioManager.StopEffectByName("se_scatter_number_collect");
                    AudioManager.PlayEffectByName("se_scatter_number_collect");
                }

                moveTween = panel.transform
                .DOLocalMove(targetWorldPos, 0.6f)
                .SetEase(_moveEndEffectType)
                .OnStart(() =>
                {
                    
                    
                })
                .OnUpdate(() =>
                {
                    // 变移动边缩小
                    panel.transform.localScale = Vector3.Lerp(originalScale, targetScale, moveTween.ElapsedPercentage());
                })
                .OnComplete(() =>
                {
                    panel.transform.localScale = originalScale; // 重置缩放值
                    panel.transform.position = panelWorldPos;
                    panel.gameObject.SetActive(false);
                    
                });
                
                moveTween.AsyncWaitForCompletion().AsUniTask().Forget();
                
            }
           
        }
        //非免费游戏时奖金符号>=5个的情况下粒子动效收集到左侧进度条
        public async UniTask MovePrToProgressBar()
        {
            for (int i = 0; i < _symbols.Count; i++)
            {
                var panel = _panels[i];
                var symbol = _symbols[i];

                if (symbol.CellData.Id != (int)SymbolEnum.SS) 
                {
                    panel.gameObject.SetActive(false);
                    continue;
                }
                panel.gameObject.SetActive(true);
                

                //获取panel上的名称为FigureText组
                TMP_Text[] figureText = panel.GetComponentsInChildren<TMP_Text>(true);

                //隐藏数字
                foreach (var text in figureText)
                {
                    text.gameObject.SetActive(false);
                }
                

                //显示粒子效果
                SkeletonGraphic[] sks = panel.GetComponentsInChildren<SkeletonGraphic>(true);
                for (int j = 0; j < sks.Length; j++)
                {
                    sks[j].gameObject.SetActive(true);
                }

                //获取panel当前世界坐标
                var panelWorldPos = panel.transform.position;
                //生成飞向貔貅手中聚宝盆位置的坐标
                float px = Screen.width / Screen.height >= 1?514:-98;
                float py = Screen.width / Screen.height >= 1?-161:667;
                var targetWorldPos = new Vector3(px, py, panelWorldPos.z);
                
                //动画将panel的坐标设置为targetWorldPos，并且在完成后将坐标重置为panelWorldPos
                Tween moveTween = null;
                moveTween = panel.transform
                .DOLocalMove(targetWorldPos, 0.6f)
                .SetEase(Ease.InOutSine)
                .OnStart(() =>
                {
                   
                    
                })
                .OnUpdate(() =>
                {
                
                })
                .OnComplete(() =>
                {
                    panel.transform.position = panelWorldPos;
                    panel.gameObject.SetActive(false);
                    
                });
                
                moveTween.AsyncWaitForCompletion().AsUniTask().Forget();
                await UniTask.Delay(((int)(0.1*1000)));

                
            }
           
        }

        public void SetAllNormalSymbol()
        {
            foreach (var symbol in _symbols)
            {
                symbol.ChangeColor();
                symbol.SetNormal();
            }
        }
        #endregion

        #region 特效演出
        public async Task PlayScatterWinFx()
        {
            foreach (var symbol in _symbols)
            {
                if (symbol.CellData.IsScatter)
                {
                    await symbol.PlayScatterWinFx();
                }
            }
        }

        public void StopScatterFx()
        {
            foreach (var symbol in _symbols)
            {
                if (symbol.CellData.IsScatter)
                {
                    symbol.StopScatterFx();
                }
            }
        }

        public async UniTask ShowPreRevealFx(bool isShow,bool ishide = false)
        {
            
            _preReveal = isShow;
            if (_preRevealFx != null)
            {
                _preRevealFx.SetActive(isShow);
            }
            if (isShow)
            {
                SetAllNormalSymbol(); // 先將所有符號恢復正常狀態
                await UniTask.Delay((int)(_revealDuration * 1000));
            }
        }

        
   
        #endregion

        private Vector3 CalculateSymbolPosition(int index)
        {
            return _arrangeDirection switch
            {
                ScrollDirection.TopToBottom =>
                    new Vector3(_startPosition.x, _startPosition.y - index * (_symbolSize.y + _offset.y), 0),
                ScrollDirection.BottomToTop =>
                    new Vector3(_startPosition.x, _startPosition.y + index * (_symbolSize.y + _offset.y), 0),
                ScrollDirection.LeftToRight =>
                    new Vector3(_startPosition.x + index * (_symbolSize.x + _offset.x), _startPosition.y, 0),
                ScrollDirection.RightToLeft =>
                    new Vector3(_startPosition.x - index * (_symbolSize.x + _offset.x), _startPosition.y, 0),
                _ => Vector3.zero,
            };
        }

        private void CalibrationSymbolPosition()
        {
            for (int i = 0; i < _symbols.Count; i++)
            {
                Symbol symbol = _symbols[i];

                symbol.transform.localPosition = CalculateSymbolPosition(i);
            }
        }

    

        #region 增加/刪除符號
        private void AddNewSymbol(CellData cellData, int queueIndex = 0)
        {
            var newSymbol = _symbolPool.GetObject(false, transform);

            // 計算新符號的位置
            int index = -(queueIndex + 1);
            Vector3 newPosition = CalculateSymbolPosition(index);
            newSymbol.transform.localPosition = newPosition;
            newSymbol.transform.SetAsFirstSibling();

            newSymbol.Initialize(cellData);
            newSymbol.gameObject.SetActive(true);
            _symbols.Insert(0, newSymbol);
        }

        private void DestroySymbol(int index)
        {
            _symbolPool.ReturnObject(_symbols[index]);
            _symbols.RemoveAt(index);
        }

        private void DestroySymbol(List<int> indexs)
        {
            indexs.Sort((a, b) => b.CompareTo(a)); // 從大到小排序，避免刪除時影響其他為刪除索引的位置

            foreach (int index in indexs)
            {
                DestroySymbol(index);
            }
        }

        private void DestroyAllSymbols()
        {
            foreach (Symbol symbol in _symbols)
            {
                _symbolPool.ReturnObject(symbol);
            }
            _symbols.Clear();
        }
        #endregion

        private async UniTaskVoid RotateSymbols()
        {
            // 确保_currentReelStrip不为null
            if (_currentReelStrip == null || _currentReelStrip.Cells == null || _currentReelStrip.Cells.Count == 0)
                return;
                
            int symbolIndex = _currentReelStrip.Cells.Count - 1;

            while (_isRotating)
            {
                // 确保索引有效
                if (symbolIndex < 0 || symbolIndex >= _currentReelStrip.Cells.Count)
                {
                    symbolIndex = _currentReelStrip.Cells.Count - 1;
                }
                
                await AddAndMoveOneSymbol(_currentReelStrip.Cells[symbolIndex], true);
                symbolIndex--;
                if (symbolIndex < 0)
                {
                    symbolIndex = _currentReelStrip.Cells.Count - 1;
                }
            }


        }

        private async UniTask RotateToEndPosition(int endPositions, bool isForceStop)
        {
            // 等待上一次移动完成
            await UniTask.WaitUntil(() => !_isMoving);  

            // 确保_currentReelStrip不为null
            if (_currentReelStrip == null || _currentReelStrip.Cells == null || _currentReelStrip.Cells.Count == 0)
                return;

            int size = _reelSpaceSize;
            int count = 1;
            int tempIndex = endPositions;
            while (size > 0)
            {
                count++;
                tempIndex++;
                if (tempIndex >= _currentReelStrip.Cells.Count)
                {
                    tempIndex %= _currentReelStrip.Cells.Count;
                }
                size -= 1;
            }
            int endShowCount = count;
            if (_endShowReelStripCount > count && !isForceStop)
            {
                endShowCount = _endShowReelStripCount;  // 要给玩家看到多少个真实转轮带符号
            }

            int symbolIndex = endPositions + endShowCount - 1;
            if (symbolIndex >= _currentReelStrip.Cells.Count)
            {
                symbolIndex %= _currentReelStrip.Cells.Count;
            }

            for (int i = 0; i < endShowCount; i++)
            {
                // 确保索引有效
                if (symbolIndex < 0 || symbolIndex >= _currentReelStrip.Cells.Count)
                {
                    symbolIndex = _currentReelStrip.Cells.Count - 1;
                }
                
                await AddAndMoveOneSymbol(_currentReelStrip.Cells[symbolIndex], isBlur: true);
                symbolIndex--;
                if (symbolIndex < 0)
                {
                    symbolIndex = _currentReelStrip.Cells.Count - 1;
                }
            }

            // 校准符号位置
            CalibrationSymbolPosition();
        }

        private async UniTask AddAndMoveOneSymbol(CellData cellData, bool isBlur = false)
        {
            // 新增一个Symbol，并且持续滚到直到Symbol完全显示
            AddNewSymbol(cellData);
            await MoveSymbols(isBlur);
            // 确保列表不为空再销毁最后一个符号
            if (_symbols.Count > 0) 
            {
                DestroySymbol(_symbols.Count - 1);
            }
        }

        [Button]
        private async UniTask MoveSymbols(bool isBlur = false, bool playEndEffect = false)
        {
            _isMoving = true;
            float moveDuration = 1.0f / _rotateSpeed;
            isBlur = isBlur && !_preReveal;

            List<UniTask> moveTasks = new();
            foreach (Symbol symbol in _symbols)
            {
                Vector3 targetPosition = symbol.transform.localPosition;

                switch (_arrangeDirection)
                {
                    case ScrollDirection.TopToBottom:
                        targetPosition.y -= _symbolSize.y + _offset.y;
                        break;
                    case ScrollDirection.BottomToTop:
                        targetPosition.y += _symbolSize.y + _offset.y;
                        break;
                    case ScrollDirection.LeftToRight:
                        targetPosition.x += _symbolSize.x + _offset.x;
                        break;
                    case ScrollDirection.RightToLeft:
                        targetPosition.x -= _symbolSize.x + _offset.x;
                        break;
                }
                var task = symbol.DoMove(targetPosition, moveDuration, Ease.Linear, isBlur, playEndEffect);
                moveTasks.Add(task);
            }
            await UniTask.WhenAll(moveTasks);
            _isMoving = false;
        }

        [Button]
        private async UniTask MoveEndBounceEffect()
        {
            _isMoving = true;
            float moveDuration = 0.2f;
            float overshootDistance = 20f; // 固定的超出距離

            if(!_isFreeGame){AddNewSymbol(_currentReelStrip.Cells[0]);} //免费游戏不需要最上面的符号不需要回弹消失的效果

            List<UniTask> moveTasks = new();
            foreach (Symbol symbol in _symbols)
            {
                Vector3 originalPosition = symbol.transform.localPosition;
                Vector3 overshootPosition = originalPosition;

                switch (_arrangeDirection)
                {
                    case ScrollDirection.TopToBottom:
                        overshootPosition.y -= overshootDistance; // 固定超出距離
                        break;
                    case ScrollDirection.BottomToTop:
                        overshootPosition.y += overshootDistance;
                        break;
                    case ScrollDirection.LeftToRight:
                        overshootPosition.x += overshootDistance;
                        break;
                    case ScrollDirection.RightToLeft:
                        overshootPosition.x -= overshootDistance;
                        break;
                }

                var s = DOTween.Sequence();
                s.Append(symbol.transform.DOLocalMove(overshootPosition, moveDuration * 0.7f).SetEase(Ease.OutCubic));
                s.Append(symbol.transform.DOLocalMove(originalPosition, moveDuration * 0.3f).SetEase(Ease.InOutBounce));
                var moveTask = s.AsyncWaitForCompletion().AsUniTask();

                moveTasks.Add(moveTask);
            }

            await UniTask.WhenAll(moveTasks);
            if(!_isFreeGame){DestroySymbol(0);}
            _isMoving = false;
        }


        public void ChangeSymbolColor(bool isGray = false)
        {
            foreach (var symbol in _symbols) // 切換回正常圖片
            {
                symbol.ChangeColor(isGray);
            }
        }
        
        
        public void ShowLine(List<int> lineIndexs)
        {
            // 显示目前要显示的连线Symbol

            foreach (int lineIndex in lineIndexs)
            {
                Symbol symbol = _symbols[lineIndex];
                symbol.ChangeColor(); // 将符合的项目显示为白色
                symbol.PlayWinAnimator();
              
            }

        }

        public void ShoScatterWinwWin(int wincount = 3,bool ischangeColoer = true)
        {
            foreach (Symbol symbol in _symbols)
            {
                symbol.ShoScatterWinwWin(wincount,ischangeColoer);
            }

        }
    }
}