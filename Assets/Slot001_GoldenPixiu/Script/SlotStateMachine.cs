using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;
using Slot.Common;
using Slot.Common.Bottom;
using StateMachine;
using UnityEngine;
using VContainer;
using DG.Tweening;
using Slot.Common.UI;
using Slot.Common.UI.Mediator;

namespace Slot001_GoldenPixiu
{
    public enum SlotStateEnum
    {
        Init,
        Idle,
        Spin,
        Rotate,
        StopSpin,
        CheckWin,
        ShowWin,
        CheckFreeGame,
        FreeGame,
        FreeGameSummary,
        EndFreeGame,
        EndGame
    }
    public class SlotStateMachine : MonoBehaviour
    {
        [SerializeField, InfoBox("自動旋轉的間隔")]
        private float _autoSpinInterval = 0.4f;
        [SerializeField, FoldoutGroup("盤面設定")]
        private BoardDataSO _initBoardData;
        [SerializeField, FoldoutGroup("盤面設定")]
        private BoardDataSO _fgBoardData;
        [SerializeField, FoldoutGroup("轉輪帶設定")]
        private ReelStripGroupSO _mgReelStripGroupData;
        [SerializeField, FoldoutGroup("轉輪帶設定")]
        private ReelStripGroupSO _fgReelStripGroupData;

        [SerializeField]
        public TextAsset LinesFile;

        #region Inject (依賴注入)
        [Inject] private BaseGameService _baseService;
        [Inject] private ReelControllerMediator _reelController;
        [Inject] private GamePlayUIMediator _gamePlayUI;
        [Inject] private GameData _gameData;
        [Inject] private GameLogic _gameLogic;
        [Inject] private FlowPresenter _flowPresenter;
        [Inject] private GameStateData _gameStateData;
        [Inject] private AutoSpinSetting _autoSpinSetting;
        [Inject] private PerformanceSettingSO _performanceSetting;
        [Inject] private RewardPresentationPanelMediator _rewardPresentationPanel;
        #endregion

        private GameService GameService => _baseService as GameService;
        private bool IsTurbo => _gameStateData.TurboType != Bottom_TurboType.TurboOff;

        #region fsm control
        private StateMachine<SlotStateEnum> _fsm;
        private float _idleTimer = 0.0f;
        private bool _isSpinTriggered = false;
        private bool _isAuto = false;
        private bool _isTurbo = false;
        private bool _isForceStop = false;
        private bool _isWaitingResponse = false;
        private TumbleResult _freeGameTumbleResult;
        #endregion

        #region 测试使用的属性
        private bool _isGet = false;
        #endregion

        private void Start()
        {
            _fsm = new StateMachine<SlotStateEnum>(this);
            _gameData.lines = JsonConvert.DeserializeObject<LineData>(LinesFile.text).lines;
        }

        private void Update()
        {
            _fsm.Driver.Update.Invoke();
        }

        private void OnEnable()
        {
            GameEventHub.Bind(this); // 啟用監聽
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this); // 關閉監聽
        }


        // #if !RELEASE_BUILD
        //         private void OnGUI()
        //         {
        //             if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        //             {
        //                 GUILayout.Label("Current State: " + _fsm.State, new GUIStyle(GUI.skin.label) { normal = { textColor = Color.yellow }, fontSize = 16 });
        //                 if (_gameData.InFreeGame)
        //                 {
        //                     GUILayout.Label("Free Spin: " + _gameData.FreeSpinsCompleted + "/" + _gameData.FreeSpinsTotal, new GUIStyle(GUI.skin.label) { normal = { textColor = Color.yellow }, fontSize = 16 });
        //                 }
        //                 if (Input.GetKeyDown(KeyCode.Space))
        //                 {
        //                     new SpinTriggerEvent().Publish(this);
        //                 }
        //             }
        //         }
        // #endif


        [OnGameEvent(SubscriberPriority.High)]
        private void OnGameUIInit(GameUIInitEvent evt)
        {
            // UI 初始化
            new GameUIReadyEvent().Publish(this);
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnGameReady(GameReadyEvent evt)
        {
            _fsm.ChangeState(SlotStateEnum.Init);
            _flowPresenter.ChangeSjSlider(0.0f);
        }

        private double _currentTime = 0.0f;

        #region 遊戲操作
        [OnGameEvent(SubscriberPriority.High)]
        public void OnSpin(SpinTriggerEvent spinEvent)
        {
            if (Time.time - _currentTime < 0.4f)
                return;

            var state = _fsm.State;
            // 僅在可開始旋轉或急停時處理，避免在 CheckWin / ShowWin 等狀態狂點造成異步流程與資料競態
            bool canStartFromIdle = state == SlotStateEnum.Idle && !_gameData.InFreeGame;
            bool canForceStop = state == SlotStateEnum.Rotate || state == SlotStateEnum.StopSpin;
            if (!canStartFromIdle && !canForceStop)
                return;

            _currentTime = Time.time;

            if (canStartFromIdle)
                _isSpinTriggered = true;
            else
                ForceStop();
        }

        public void ForceStop()
        {
            Debug.Log($"急停触发 - 当前状态: {_fsm.State}");

            if (_fsm.State == SlotStateEnum.Rotate || _fsm.State == SlotStateEnum.StopSpin)
            {
                Debug.Log("急停条件满足，执行急停");
                _reelController.IsForceStop = true;
                _isForceStop = true;

                if (_fsm.State == SlotStateEnum.Rotate)
                {
                    _fsm.ChangeState(SlotStateEnum.StopSpin);
                }
            }
            else
            {
                Debug.LogWarning($"急停被忽略 - 当前状态 {_fsm.State} 不支持急停");
            }
        }
        #endregion

        #region StateMachine States
        private void Init_Enter()
        {
            AudioManager.PlayOneTrackByName("mu_main_background");
            _gameData.Initialize();
            SetBoard(_initBoardData.RandomBoard());
            _fsm.ChangeState(SlotStateEnum.Idle);
        }

        private void Idle_Enter()
        {
            _idleTimer = 0f;
            _isAuto = _autoSpinSetting.UpdateAutoSpinState();
            if (!_gameData.InFreeGame)
            {
                new UIBottomNormalEvent().Publish(this);
            }
        }

        private void Idle_Update()
        {
            if (_isSpinTriggered && !_gameData.InFreeGame)
            {
                _isSpinTriggered = false;
                _fsm.ChangeState(SlotStateEnum.Spin);
                return;
            }

            _idleTimer += Time.deltaTime;
            if (_idleTimer >= _autoSpinInterval)
            {
                _idleTimer = 0f; // 重置計時器

                if (_isAuto)
                {
                    _fsm.ChangeState(SlotStateEnum.Spin);
                }
            }
        }

        private void Spin_Enter()
        {
            if (_gameData.InFreeGame)
            {
                _gameData.FreeSpinsCompleted++;
                new UIBottomLockEvent().Publish(this);  // 鎖定UI，如果在FreeGame會更新_gameData.FreeSpinsCompleted
                _fsm.ChangeState(SlotStateEnum.Rotate);
                return;
            }

            new UIAutoSpinCountEvent().Publish(this); //刷新自动數量

            _flowPresenter.PlayPXNomormalAnimation();
            _flowPresenter.ShowAniLightPanel(false);

            new UIBottomLockEvent().Publish(this);  // 鎖定UI
            _autoSpinSetting.MinusAutoSpinCount();

            var buyType = _gameData.BuyType;
            double totalBet = _gameLogic.GetTotalBet(_gameData.Bet, buyType);
            _gameData.ResetNewRound();
            _gamePlayUI.ResetNewRound();
            bool isSuccess = SendSpinRequest(totalBet, buyType);
            if (!isSuccess)
            {
                return;
            }
            _fsm.ChangeState(SlotStateEnum.Rotate);
        }

        private void Rotate_Enter()
        {
            var groupData = _gameData.InFreeGame ? _fgReelStripGroupData : _mgReelStripGroupData;
            // Rotate_Enter 時將 _isTurbo 設為 IsTurbo，確保 StopSpin_Enter 時 _isTurbo 狀態正確
            _isTurbo = IsTurbo;
            _reelController.StartRotation(groupData, _gameData.Bet, _isTurbo);
        }

        private void Rotate_Update()
        {
            if (_isWaitingResponse) return;
            if (_reelController.CanStop)
            {
                _fsm.ChangeState(SlotStateEnum.StopSpin);
            }
        }

        private async void StopSpin_Enter()
        {
            // 尚未收到伺服器結果時不可進入 CheckWin，否則 GetTotalWin 等會對 null SlotResult 解引用
            if (_gameData?.SlotResult == null)
            {
                Debug.LogWarning("GameData or SlotResult is null, recovering to Idle");
                new UIBottomNormalEvent().Publish(this);
                _fsm.ChangeState(SlotStateEnum.Idle);
                return;
            }

            var tumbleResult = _gameLogic.GetCurrentTumbleResult(_gameData);
            if (tumbleResult == null || tumbleResult.TumbleSymbol == null || tumbleResult.TumbleSymbol.Count == 0)
            {
                Debug.LogWarning("TumbleResult invalid, recovering to Idle");
                new UIBottomNormalEvent().Publish(this);
                _fsm.ChangeState(SlotStateEnum.Idle);
                return;
            }

            _gameData.BoardData = _gameLogic.Trans2BoardData(tumbleResult);

            List<int> endPos = _gameLogic.GetCurrentEndPos(_gameData);
            var groupData = _gameData.InFreeGame ? _fgReelStripGroupData : _mgReelStripGroupData;
            var combReels = _gameLogic.GetCombReelsByEndBoard(_gameData.BoardData, endPos, groupData, _gameData.Bet);


            // 确保在Turbo模式下也能正确传递参数
            await _reelController.StopRotation(_gameData.BoardData, combReels, endPos, tumbleResult.PreReel, _isForceStop || _isTurbo, _gameData.InFreeGame);
            _fsm.ChangeState(SlotStateEnum.CheckWin);
        }

        private async void StopSpin_Exit()
        {
            _isForceStop = false;
            var tumbleResult = _gameLogic.GetCurrentTumbleResult(_gameData);

            // 添加空值检查，防止ArgumentNullException
            if (tumbleResult == null)
            {
                Debug.LogWarning("TumbleResult is null, skipping scatter count check");
                return;
            }

            //每把判断奖金符号数量数量>1转动结束后设置收集进度（延迟0.2秒等旋转动画停止后再显示）
            float delay = _gameLogic.GetScatterCount(tumbleResult) < 5 ? 1.0f : 2.0f;
            await UniTask.Delay((int)(delay * 1000));
            if (_gameLogic.GetScatterCount(tumbleResult) > 0) await _flowPresenter.ChangeSjSlider(-1f);

        }


        private async void CheckWin_Enter()
        {
            if (_gameData?.SlotResult == null)
            {
                Debug.LogWarning("CheckWin: SlotResult is null, recovering to Idle");
                new UIBottomNormalEvent().Publish(this);
                _fsm.ChangeState(SlotStateEnum.Idle);
                return;
            }

            //判断和展示奖金符号是否大于等于5个的情况，如果大于等于5个，则显示收集奖金符号上的数字动画，并且增加底部分数
            await ShowScatterWinEffect();
            if (_gameData?.SlotResult == null)
            {
                Debug.LogWarning("CheckWin: SlotResult cleared after scatter effect, recovering to Idle");
                new UIBottomNormalEvent().Publish(this);
                _fsm.ChangeState(SlotStateEnum.Idle);
                return;
            }

            LogUtils.Log($"<color=收集完成--------------------->ScatterCount:</color>");
            double twin = _gameLogic.GetTotalWin(_gameData);
            if (twin > 0)
            {
                //这里延迟1秒是用于等待收集效果到貔貅手中聚宝盆的效果,不然貔貅动了收集就位置不对了
                await UniTask.WaitForSeconds(1.0f);
                //貔貅赢了之后播放的动画
                _flowPresenter.PlarPXWinAnimation(_gameData.Bet, twin);
                //显示光柱动画
                _flowPresenter.ShowAniLightPanel(true);

            }

            double spinWin = _gameLogic.GetCurrentSpinWin(_gameData);

            if (_gameData.InFreeGame)
            {
                _reelController.ShowFGWinEffect();
            }
            else
            {
                _reelController.ShowWinEffect();
            }

            if (spinWin > 0)
            {

                if (!_gameData.InFreeGame) //免费游戏的底部赢分动画不在这里显示
                {
                    _flowPresenter.AddBottomWin(_gameLogic.GetCurrentSpinWin(_gameData));
                    await UniTask.WaitForSeconds(_performanceSetting.MGLineWinShowTime);
                }

                _fsm.ChangeState(SlotStateEnum.ShowWin);
            }
            else
            {
                _fsm.ChangeState(SlotStateEnum.CheckFreeGame);
            }
        }


        private async void ShowWin_Enter()
        {



            double spinWin = _gameLogic.GetCurrentSpinWin(_gameData);
            LogUtils.Log($"<color=yellow>SpinWin: {spinWin} ， bet : {_gameData.Bet} </color>");
            if (spinWin > 0)
            {
                //阶层报奖
                // await _flowPresenter.ShowTotalWin(_gameData.Bet, spinWin); 
                _gameData.IsInWinLine = true; //标记正在阶层报奖
                await _rewardPresentationPanel.ShowWinCelebration(_gameData.Bet, spinWin);
                _gameData.IsInWinLine = false; //取消阶层报奖标记

            }
            _fsm.ChangeState(SlotStateEnum.CheckFreeGame);
        }

        private void CheckFreeGame_Enter()
        {

            if (_gameLogic.CheckFreeGame(_gameData) && !_gameData.InFreeGame)
            {
                int originFreeSpineTotal = _gameData.FreeSpinsTotal;
                _gameData.MaxFreeSpinCount = _gameData.SlotResult.FGResult.FGSpinCount;
                int obtainedFreeSpinCount = _gameData.MaxFreeSpinCount;

                _gameData.FreeSpinsTotal += obtainedFreeSpinCount;
                _gameData.FreeSpinsWonThisStep = _gameData.FreeSpinsTotal - originFreeSpineTotal;

                if (_gameData.FreeSpinsWonThisStep > 0)
                {
                    _fsm.ChangeState(SlotStateEnum.FreeGame);
                    return;
                }
            }
            if (_gameData.InFreeGame)
            {
                var state = _gameData.FreeSpinsCompleted >= _gameData.FreeSpinsTotal ? SlotStateEnum.FreeGameSummary : SlotStateEnum.Idle;
                _fsm.ChangeState(state);
            }
            else
            {
                _fsm.ChangeState(SlotStateEnum.EndGame);
            }
        }
        private void CheckFreeGame_Exit()
        {
            _gameData.SpinStep = 0;
            if (_gameData.InFreeGame)
            {
                _gameData.FreeGameStep++;
            }
        }

        private async void FreeGame_Enter()
        {

            //停止背景音乐
            AudioManager.StopBGM();

            //记录免费游戏数据
            _freeGameTumbleResult = _gameLogic.GetCurrentTumbleResult(_gameData);

            // 播放鈴聲(2秒) > 頓點(反應時間0.3秒) >  特殊獎宣告面板(4秒) >　進入FreeGame動畫 > FreeGame
            bool isRetrigger = _gameData.InFreeGame; // 如果在進入此狀態前已是 FreeGame，代表是 retrigger
            _gameData.InFreeGame = true;

            //免费游戏进度条满充
            _flowPresenter.ChangeSjSlider(1.0f).Forget();
            await _flowPresenter.PlayFreeGameIntro();

            if (!isRetrigger)
            {
                _gameData.PreFGBoardData = _gameData.BoardData;

            }
            //打开提示面板
            // await _flowPresenter.OpenObtainFreeSpinsPanel(_gameData.FreeSpinsWonThisStep, isRetrigger);


            if (!isRetrigger)
            {

                //播放转场动画 并设置 免费游戏状态  new FreeGameEnterEvent().Publish(this);
                await _flowPresenter.PlayFreeGameShow();
                // _rewardPresentationPanel.ShowFreeSpins(8, isRetrigger);
            }

            //切换背景，底框等
            _flowPresenter.ShowFreeGameImageBg(_gameData.InFreeGame);


            //播放免费游戏背景音乐
            AudioManager.PlayOneTrackByName("mu_free_background");


            _fsm.ChangeState(SlotStateEnum.Idle);
        }

        private async void FreeGameSummary_Enter()
        {
            await _flowPresenter.ShowSettleTotalWin(_gameLogic.GetTotalWin(_gameData));
            _fsm.ChangeState(SlotStateEnum.EndFreeGame);
        }

        private async void EndFreeGame_Enter()
        {
            _gameData.InFreeGame = false;
            _flowPresenter.ExitFreeGame();

            //底框和背景
            _flowPresenter.ShowFreeGameImageBg(_gameData.InFreeGame);
            _flowPresenter.PlayPXNomormalAnimation();
            //免费游戏收集进度条
            _flowPresenter.ChangeSjSlider(0.0f).Forget();

            //所有符号变为原来的状态
            _gameData.BoardData = _gameLogic.Trans2BoardData(_freeGameTumbleResult);
            _reelController.InitializeBoard(_gameData.BoardData);

            _fsm.ChangeState(SlotStateEnum.EndGame);
        }

        private void EndGame_Enter()
        {
            _gameData.LastTotalWin = _gameLogic.GetTotalWin(_gameData);
            SetBalance(_gameData.SlotResultBalance);
            // GameServerHandler.Instance.Send("GetBalance", new JObject());
            _fsm.ChangeState(SlotStateEnum.Idle);
        }
        #endregion

        #region Private Methods
        private void SetBalance(double balance)
        {
            // _gameData.Balance = balance;
            new GameChangeBalanceEvent().Publish(this);
        }
        private void SetBoard(BoardData boardData)
        {
            _gameData.BoardData = boardData;
            // 傳遞給 ReelController 來設置盤面
            _reelController.InitializeBoard(_gameData.BoardData);

        }


        private async UniTask ShowScatterWinEffect()
        {
            //配合奖金符号收集效果延迟调用显示底部赢分动画，免费游戏时收集数字到底部跑马灯，非免费游戏粒子动效收集到进度条效果
            var tumbleResult = _gameLogic.GetCurrentTumbleResult(_gameData);

            // 添加空值检查，防止ArgumentNullException
            if (tumbleResult == null)
            {
                Debug.LogWarning("TumbleResult is null, skipping scatter win effect");
                return;
            }

            int snum = _gameLogic.GetScatterCount(tumbleResult);
            if (snum >= 5) //奖金符号大于等于5个的时候特殊处理
            {

                if (_gameData.InFreeGame)
                { //只有免费游戏赢的情况下才挨个递增底部的赢分
                    DOVirtual.DelayedCall(2.5f, async () =>
                    {
                        //底部赢分动效
                        await SetScatterWinToBottomScoreWin();
                    });
                }

                //显示奖金符号超过5个的情况下的显示效果
                await _reelController.ShowScatterWinEffect();
            }
            else if (snum > 0 && !_gameData.InFreeGame) //奖金符号小于5个的时候,并且非免费游戏时候，高亮符号后，只收集到进度条
            {

                await _reelController.ShowScatterWinEffect();
            }
        }

        private async UniTask SetScatterWinToBottomScoreWin()
        {

            foreach (var reel in _gameData.BoardData.Reels)
            {
                foreach (var cell in reel.Cells)
                {
                    if (cell.Id == (int)SymbolEnum.SS)
                    {
                        new UIAddWinEvent(cell.WildMoney).Publish(this);
                        AudioManager.StopEffectByName("se_regularwin");
                        AudioManager.PlayEffectByName("se_regularwin");
                        await UniTask.Delay(((int)(0.3 * 1000)));
                    }
                }
            }

        }


        #endregion

        #region 发送spin数据
        private bool SendSpinRequest(double bet, BuyType buyType)
        {
            //判读余额是否足够
            if (_gameData.Balance < bet)
            {
                Debug.LogWarning("余额不足，无法发送spin数据");
                DialogMediator.ShowDialog(
                    CommonDefine.DialogTableName, CommonDefine.DialogKey_SystemTitle,
                    CommonDefine.DialogTableName, CommonDefine.DialogKey_ErrorBalance,
                    new ActionButton("OK", () => { }), true
                );
                _autoSpinSetting.CancelAutoSpin();
                _fsm.ChangeState(SlotStateEnum.Idle);
                return false;
            }
            //发送spin数据
            _isWaitingResponse = true;
            GameService.SendSpin(bet, buyType, OnSpinResponse);

            //测试用，用于从服务器获取满意数据
            // TestSendSpinRequest();
            return true;
        }

        private void OnSpinResponse(JToken response)
        {

            SlotResult data = JsonConvert.DeserializeObject<SlotResult>(response.ToString());

            string jsonData = JsonConvert.SerializeObject(data);
            _gameData.SlotResult = JsonConvert.DeserializeObject<SlotResult>(jsonData);
            _gameData.SlotResultBalance = ServiceUtils.ToClientBalance(data.Balance);
            _isWaitingResponse = false;
        }
        #endregion

        #region 测试从服务器获取满意的数据
        private void TestSendSpinRequest()
        {
            GameService.SendSpin(1.0f, 0, TestOnSpinResponse);
        }
        private void TestOnSpinResponse(JToken response)
        {

            int ncount = 0;
            SlotResult data = JsonConvert.DeserializeObject<SlotResult>(response.ToString());
            foreach (var reel in data.MGResult.MGTumbleList[0].TumbleSymbol)
            {
                foreach (var symbol in reel)
                {
                    if (symbol == 17)
                    {
                        ncount++;
                    }
                }

            }

            if (ncount != 5)
            {
                TestSendSpinRequest();
                return;
            }
            LogUtils.Log("数据满足:" + ncount);

        }
        #endregion


    }
}