using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using Spine.Unity;
using System.Threading.Tasks;
// using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Slot.Common;

namespace Slot001_GoldenPixiu
{
    public class ReelController : MonoBehaviour
    {
        [SerializeField] private GenericPool<Symbol> _symbolPool;
        [FoldoutGroup("轉輪節奏參數")] private float _rotateSpeed = 40.0f;
        [FoldoutGroup("轉輪節奏參數")] private float _stopInterval = 0.8f;

        [FoldoutGroup("轉輪節奏參數"), InfoBox("一手的轉輪持續時間")]
        private float _spinDuration = 0.4f;

        [FoldoutGroup("瞇牌"), InfoBox("幾個Scatter開始瞇牌")]
        private int _revealScatterCount = 3;

        [FoldoutGroup("瞇牌"), InfoBox("瞇牌時旋轉速度")]
        private float _revealRotateSpeed = 20.0f;

        [SerializeField] private List<ReelStrip> _reelStrips;
        [SerializeField] private GamePlayUI _gamePlayUI;

        private bool _canStop = true;
        private bool _isShowWin = false; // 是否显示赢线

        //赢线飘分
        [SerializeField] private TMPro.TextMeshProUGUI _score1;
        [SerializeField] private TMPro.TextMeshProUGUI _score2;
        [SerializeField] private TMPro.TextMeshProUGUI _score3;
        [SerializeField] private TMPro.TextMeshProUGUI _score4;

        //十条赢线
        private List<List<int>> _deFaultWinLines = new List<List<int>>();

        protected List<List<int>> winLines = new List<List<int>>(); // 本轮盘面的连线
        protected int winLineIndex = 0; // 连线轮播的index
        protected float _winLineInterval = 1.5f;

        protected PerformanceSettingSO _performanceSetting;

        public bool CanStop => _canStop;

        public bool IsForceStop = false;

        public bool IsScatterWin = false; // 是否奖金符号赢的类型(>=5个奖金符号触发奖金符号赢特效)

        #region Inject (依賴注入)

        private GameData _gameData;
        private GameLogic _gameLogic;

        #endregion

        private void Awake()
        {
            foreach (ReelStrip segment in _reelStrips)
            {
                segment.Initialization(_symbolPool);
            }

            _deFaultWinLines.Add(new List<int>() { 0, 0, 0 });
            _deFaultWinLines.Add(new List<int>() { 0, 1, 0 });
            _deFaultWinLines.Add(new List<int>() { 0, 1, 1 });
            _deFaultWinLines.Add(new List<int>() { 1, 1, 0 });
            _deFaultWinLines.Add(new List<int>() { 1, 1, 1 });
            _deFaultWinLines.Add(new List<int>() { 1, 2, 1 });
            _deFaultWinLines.Add(new List<int>() { 1, 2, 2 });
            _deFaultWinLines.Add(new List<int>() { 2, 2, 1 });
            _deFaultWinLines.Add(new List<int>() { 2, 2, 2 });
            _deFaultWinLines.Add(new List<int>() { 2, 3, 2 });

        }

        public void Init(float rotateSpeed, float stopInterval, float spinDuration, int revealScatterCount,
            float revealRotateSpeed, GameData gameData, GameLogic gameLogic, PerformanceSettingSO performanceSetting)
        {
            _rotateSpeed = rotateSpeed;
            _stopInterval = stopInterval;
            _spinDuration = spinDuration;
            _revealScatterCount = revealScatterCount;
            _revealRotateSpeed = revealRotateSpeed;
            _gameData = gameData;
            _gameLogic = gameLogic;
            _performanceSetting = performanceSetting;

        }

        public void InitializeBoard(BoardData boardData)
        {
            // 根據資料設置每個 ReelStrip 的符號
            for (int i = 0; i < _reelStrips.Count; i++)
            {
                if (i < boardData.Reels.Count)
                {
                    _reelStrips[i].InitializeSymbols(boardData.Reels[i]);
                }
            }

        }

        public async UniTaskVoid StartRotation(List<ReelData> combReels, bool isTurbo = false)
        {
            HiddentScore();
            _canStop = false;
            _isShowWin = false;
            foreach (ReelStrip reel in _reelStrips)
            {
                reel.SetAllNormalSymbol();
            }

            if (isTurbo)
            {
                // 在Turbo模式下也需要正确初始化旋转状态
                for (int i = 0; i < _reelStrips.Count; i++)
                {
                    _reelStrips[i].RotateSpeed = _rotateSpeed;
                    _reelStrips[i].StartRotation(combReels[i]);
                }
                _canStop = true;
                return;
            }

            for (int i = 0; i < _reelStrips.Count; i++)
            {
                _reelStrips[i].RotateSpeed = _rotateSpeed;
                _reelStrips[i].StartRotation(combReels[i]);
                // await UniTask.Delay((int)(0.3 * 1000)); //每轮之间的间隔
            }

            await UniTask.Delay((int)(_spinDuration * 1000));
            _canStop = true;
        }

        #region 停止旋转
        public async UniTask StopRotation(BoardData boardData, List<ReelData> combReels, List<int> endPositions, List<int> preList, bool isFreeGame = false)
        {
            List<UniTask> tasks = new();

            // 处理强制停止的情况   !preList.Contains(1)有预中的情况不让快速停止
            if (IsForceStop && !preList.Contains(1))
            {
                for (int i = 0; i < _reelStrips.Count; i++)
                {
                    _reelStrips[i].RotateSpeed = _rotateSpeed;
                    tasks.Add(_reelStrips[i].ForeceStopRotation(combReels[i], endPositions[i], isFreeGame));
                }

                await UniTask.WhenAll(tasks);
                if (ResolutionManager.Instance.CheckIsOn(this.transform))
                {
                    // AudioManager.StopEffectByName("se_reel_stop");
                    // AudioManager.PlayEffectByName("se_reel_stop", checkRepeat: true, checkRepeatTime: 0.1f);
                }


                //停止轮转音效
                AudioManager.StopEffectByName("se_drop");

                //让所有符号统一呼吸节奏
                for (int i = 0; i < _reelStrips.Count; i++)
                {
                    _reelStrips[i].SetAnimationLoop();
                }
                return;
            }

            //定义停轮顺序，将中间轮放在最后，便于控制停轮顺序，有预中的情况下，从左到右停轮，没有预中的情况下，按照0,2,1停轮
            int[] stopIndex = preList.Contains(1) ? new int[] { 0, 1, 2 } : new int[] { 0, 2, 1 };

            // 正常停止流程
            foreach (int i in stopIndex)
            {
                bool isForceStop = IsForceStop;
                bool playSound = !isForceStop; // 强制停止就不个别播放音效，由Controller统一播放，避免重复播放
                bool preReveal = preList[i] == 1;//IsPrveal(boardData.Reels[i]) && i != 0;//preList[i] == 1; // 瞇牌效果

                if (preReveal)
                {
                    // AudioManager.PlayEffectByName("se_squat", checkRepeat: true);
                    //播放预中音效
                    AudioManager.StopEffectByName("ALL_SFX_PreTease");
                    AudioManager.PlayEffectByName("ALL_SFX_PreTease");
                    new UIMarqueeSpecialEvent().Publish(this);
                    _reelStrips[i].RotateSpeed = _revealRotateSpeed;
                    await UniTask.WhenAll(tasks);
                    tasks.Clear();
                    await _reelStrips[i].StopRotation(combReels[i], endPositions[i], isForceStop, preReveal, playSound, isFreeGame);
                    AudioManager.StopEffectByName("ALL_SFX_PreTease");
                }
                else
                {
                    if (i == 1 && !preList.Contains(1))
                    {  //中间的列在整个没有预中的时候需要延迟0.2停止
                        await UniTask.Delay((int)(0.15f * 1000));
                    }
                    var task = _reelStrips[i]
                        .StopRotation(combReels[i], endPositions[i], isForceStop, preReveal, playSound, isFreeGame);
                    tasks.Add(task);

                }

                // 非强制停止且非眯牌情况下需要间隔停止
                if (!isForceStop && !preReveal)
                {
                    await UniTask.Delay((int)(_stopInterval * 1000));
                }
            }

            await UniTask.WhenAll(tasks);

            // 如果在等待过程中变为强制停止状态，需要播放停止音效
            if (IsForceStop)
            {
                if (ResolutionManager.Instance.CheckIsOn(this.transform))
                {
                    // AudioManager.StopEffectByName("se_reel_stop");
                    // AudioManager.PlayEffectByName("se_reel_stop", checkRepeat: true, checkRepeatTime: 0.1f);
                }
                
            }

            //让所有符号统一呼吸节奏
            for (int i = 0; i < _reelStrips.Count; i++)
            {
                _reelStrips[i].SetAnimationLoop();
            }


        }
        #endregion 停止旋转

        //判断是否所有的符号都是奖金符号
        private bool IsPrveal(ReelData reelData)
        {

            int fid = reelData.Cells[0].Id;
            if (fid == (int)SymbolEnum.NN) { return false; }
            foreach (var cell in reelData.Cells) { if (cell.Id != fid) { return false; } }
            return true;
        }


        //收集奖金符号金额之前的奖金符号特效
        public async Task PlayScatterWinFx()
        {
            foreach (ReelStrip reel in _reelStrips)
            {
                await reel.PlayScatterWinFx();

            }
        }

        //免费游戏奖金符号收集赢分效果，免费游戏收集数字到底部，非免费游戏收集粒子到进度条
        public async Task MoveWinScorePanels()
        {
            bool isplaymic = false;

            foreach (ReelStrip reel in _reelStrips)
            {
                if (_gameData.InFreeGame)
                {
                    await reel.MoveWinScorePanels();
                }
                else
                {
                    if (!isplaymic && ResolutionManager.Instance.CheckIsOn(this.transform))
                    {
                        isplaymic = true;
                        //播放收集音效
                        AudioManager.StopEffectByName("se_scatter_collect");
                        AudioManager.PlayEffectByName("se_scatter_collect");
                    }
                    await reel.MovePrToProgressBar();
                }
            }
        }

        public void StopScatterFx()
        {
            foreach (ReelStrip reel in _reelStrips)
            {
                reel.StopScatterFx();
            }
        }
        public void ChangeSymbolColor(bool isGray = false)
        {
            foreach (ReelStrip reel in _reelStrips)
            {
                reel.ChangeSymbolColor(isGray);
            }
        }

        #region 连线相关逻辑

        /// <summary>
        /// 检查结果是否有连线
        /// </summary>
        public void CheckWin()
        {

            winLines.Clear();
            //检查奖金符号是否>=5个大于等于5个需要显示奖金符号赢的特效
            CheckScatterWin();

            //这款游戏 免费游戏不需要连线
            if (_gameData.InFreeGame)
            {
                return;
            }
            List<List<int>> lines = _gameData.lines;

            var curTumbleResult = _gameLogic.GetCurrentTumbleResult(_gameData);


            // 从旋转结果检查赢线
            for (int i = 0; i < curTumbleResult.LineWin.Count; i++)
            {
                if (curTumbleResult.LineWin[i] > 0)
                {
                    int count = curTumbleResult.LineCount[i];
                    List<int> addLine = new List<int>();
                    for (int j = 0; j < count; j++)
                    {
                        addLine.Add(lines[i][j]);
                    }

                    bool alreadyExists = winLines.Any(existingLine => existingLine.SequenceEqual(addLine));
                    if (!alreadyExists)
                    {
                        winLines.Add(addLine);
                    }
                }
            }
            winLineIndex = 0;




        }

        public void CheckScatterWin() //检测奖金符号是否大于等于5个赢
        {
            int ncount = 0;
            var symbols = _gameLogic.GetCurrentTumbleResult(_gameData).TumbleSymbol;

            foreach (var item in symbols)
            {
                foreach (var number in item)
                {
                    if (number == 17)
                    {
                        ncount++;
                    }
                }
            }
            IsScatterWin = ncount >= 5;
        }

        //显示奖金符号超过5个的赢的特效(高亮3次->金币收集效果)
        public async UniTask ShowScatterWinEffect(bool issj = true)
        {
            if (IsScatterWin)
            {
                for (int i = 0; i < _reelStrips.Count; ++i)
                {
                    // 高亮3次，其他符号压暗处理
                    _reelStrips[i].ShoScatterWinwWin(3, true);
                }

                await UniTask.Delay(600);

                //播放三次高亮的音效
                for (int i = 0; i < 3; ++i)
                {
                    AudioManager.StopEffectByName("se_scatter_win");
                    AudioManager.PlayEffectByName("se_scatter_win");
                    await UniTask.Delay(300);
                }

                // await UniTask.Delay(1800);

                if (issj)
                {
                    //免费游戏收集数字到底部，非免费游戏收集粒子到进度条
                    await MoveWinScorePanels();
                }


            }
            else
            { //非免费游戏，有奖金符号但是不够5个的情况

                for (int i = 0; i < _reelStrips.Count; ++i)
                {
                    //高亮1次，不压暗其他符号，只收集进度条
                    _reelStrips[i].ShoScatterWinwWin(1, false);
                }
                await UniTask.Delay(600);

                //收集进度条
                await MoveWinScorePanels();
            }
        }


        /// <summary>
        /// 显示赢线
        /// </summary>
        public void ShowWin()
        {


            if (winLines.Count == 0)
                return;

            ChangeSymbolColor(true); // 将符号变为灰色

            List<List<int>> winSymbols = Enumerable.Range(0, _reelStrips.Count)
                .Select(_ => new List<int>())
                .ToList();

            // 决定要播放的连线
            List<List<int>> linesToAdd =
                (winLineIndex == 0) ? winLines : new List<List<int>> { winLines[winLineIndex - 1] };
            winLineIndex++; // 递增索引

            // 将选定的线加入winSymbols
            foreach (var line in linesToAdd)
            {
                for (int j = 0; j < line.Count; ++j)
                {
                    winSymbols[j].Add(line[j]);
                }
            }

            // 当索引超过范围时重置为0
            if (winLineIndex > winLines.Count)
            {
                winLineIndex = 0;

                //检擦一下是否有sc符号赢分的情况(sc>=5 代表赢分，播放赢线的时候也需要播放scatterWin特效)
                if (IsScatterWin)
                {
                    ChangeSymbolColor(false); // 将符号变为正常色
                    HiddentScore();
                    //播放scatterWin特效
                    ShowScatterWinEffect(false).Forget();
                    _winLineInterval = _performanceSetting.LineWinSwitchTime;
                    return;
                }
            }

            //播放连线音效
            if (!_gameData.IsInWinLine)
            {
                AudioManager.StopEffectByName("se_getwin");
                AudioManager.PlayEffectByName("se_getwin");
            }

            // 显示连线
            for (int i = 0; i < _reelStrips.Count; ++i)
            {
                _reelStrips[i].ShowLine(winSymbols[i]);
            }

            ShowWinLinScore(linesToAdd);

            _winLineInterval = _performanceSetting.LineWinSwitchTime;
            _isShowWin = true;
        }

        private void ShowWinLinScore(List<List<int>> linesToAdd)
        {
            //    linesToAdd 里面存着的就是还要播放的线
            HiddentScore();

            if (linesToAdd.Count == 0 || linesToAdd.Count > 1)
            { //一条都没有或者有多条的时候不显示分数
                return;
            }

            var line = linesToAdd[0];
            int index = -1;
            foreach (var lines in _deFaultWinLines)
            {
                if (lines.SequenceEqual(line))
                {
                    index = _deFaultWinLines.IndexOf(lines);
                }
            }
            //取出对应的赢分金额
            var win = _gameData.SlotResult.MGResult.MGTumbleList[0].LineWin[index];

            var winstring = "+" + ServiceUtils.ToCurrentString(ServiceUtils.ToClientBalance(win), Bottom_Define.MoneyFormat);
            var reels2ct = line[1]; //第二列的数字决定要显示哪个数字

            _score1.gameObject.SetActive(reels2ct == 0);
            _score2.gameObject.SetActive(reels2ct == 1);
            _score3.gameObject.SetActive(reels2ct == 2);
            _score4.gameObject.SetActive(reels2ct == 3);
            _score1.text = winstring;
            _score2.text = winstring;
            _score3.text = winstring;
            _score4.text = winstring;

            //_score向上移动50px动画
            ScoreAnimal(_score1);
            ScoreAnimal(_score2);
            ScoreAnimal(_score3);
            ScoreAnimal(_score4);

        }
        private void ScoreAnimal(TMPro.TextMeshProUGUI score)
        {

            Tween moveTween = null;
            var panelWorldPos = score.transform.position;
            score.transform.position = new Vector3(panelWorldPos.x, panelWorldPos.y - 50, panelWorldPos.z);
            moveTween = score.transform
            .DOMove(panelWorldPos, 0.3f)
            .SetEase(Ease.Linear)
            .OnStart(() =>
            {


            })
            .OnUpdate(() =>
            {

            })
            .OnComplete(() =>
            {
                score.transform.position = panelWorldPos;
            });

        }

        private void HiddentScore()
        {
            _score1.gameObject.SetActive(false);
            _score2.gameObject.SetActive(false);
            _score3.gameObject.SetActive(false);
            _score4.gameObject.SetActive(false);
        }

        /// <summary>
        /// 显示所有赢线
        /// </summary>
        public void ShowAllWinLine()
        {
            if (winLines.Count > 0)
            {
                ChangeSymbolColor(true);
                List<List<int>> winSymbols = Enumerable.Range(0, _reelStrips.Count)
                    .Select(_ => new List<int>())
                    .ToList();

                // 添加所有赢线
                foreach (var line in winLines)
                {
                    for (int j = 0; j < line.Count; ++j)
                    {
                        winSymbols[j].Add(line[j]);
                    }
                }

                //播放连线音效
                if (!_gameData.IsInWinLine)
                {
                    AudioManager.StopEffectByName("se_getwin");
                    AudioManager.PlayEffectByName("se_getwin");
                }

                // 显示所有赢线
                for (int i = 0; i < _reelStrips.Count; ++i)
                {
                    _reelStrips[i].ShowLine(winSymbols[i]);

                }
            }
        }

        // 更新连线动画
        public void UpdateWin()
        {
            _winLineInterval -= Time.deltaTime;
            // 超过显示时间 展示下一组连线
            if (winLines.Count >= 1 && _winLineInterval < 0)
            {
                ShowWin();
            }
        }


        private void Update()
        {
            if (_isShowWin)
            {
                UpdateWin();
            }
        }

        #endregion
    }
}