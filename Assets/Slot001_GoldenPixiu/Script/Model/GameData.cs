using System;
using System.Collections.Generic;
using Slot.Common;
using VContainer;

namespace Slot001_GoldenPixiu
{
    /// <summary>
    /// 遊戲資料模型 (Model)
    /// 持有遊戲過程中的所有狀態與資料。
    /// </summary>
    ///
    public class LineData
    {
        public List<List<int>> lines;
        public LineData()
        {
            lines = new List<List<int>>();
        }
    }
    public class GameData
    {
        public const int BASE_GAME_INIT_MULTIPLIER = 1;
        public const int FREE_GAME_INIT_MULTIPLIER = 2;
        [Inject] private PlayerBetData _playerBetData;
        [Inject] private GameStateData _gameStateData;
        
        //连线数据
        
        public List<List<int>> lines;
        #region 核心資料
        /// <summary>
        /// 當前的盤面佈局。
        /// </summary>
        public BoardData BoardData { get; set; }

        /// <summary>
        /// 進入免費遊戲前的盤面資料。
        /// </summary>
        public BoardData PreFGBoardData { get; set; }

        /// <summary>
        /// 玩家餘額。
        /// </summary>
        public double Balance
        {
            get => _playerBetData.Balance;
            set => _playerBetData.Balance = value;
        }

        /// <summary>
        /// 當前押注額。
        /// </summary>
        public float Bet => (float)_playerBetData.Bet;
        #endregion

        #region 單次回合狀態 (Spin State)
        /// <summary>
        /// 從伺服器收到的完整旋轉結果。
        /// </summary>
        public SlotResult SlotResult { get; set; }

        /// <summary>
        /// 旋轉結果回傳的餘額 (用於最終校對)。
        /// </summary>
        public double SlotResultBalance { get; set; }

        /// <summary>
        /// 當前 Tumble/Drop 的步驟索引。
        /// </summary>
        public int SpinStep { get; set; }


        /// <summary>
        /// 是否正在阶层报奖
        /// </summary>
        public bool IsInWinLine { get; set; }

        #endregion

        #region 免費遊戲狀態 (Free Game State)
        /// <summary>
        /// 是否正處於免費遊戲模式。
        /// </summary>
        public bool InFreeGame { get; set; }

        /// <summary>
        /// 在免費遊戲模式中，這是第幾次旋轉的步驟索引。
        /// </summary>
        public int FreeGameStep { get; set; }

        /// <summary>
        /// 伺服器回傳的最大免費遊戲次數。
        /// </summary>
        public int MaxFreeSpinCount { get; set; }

        /// <summary>
        /// 在當前回合中新贏得的免費旋轉次數。
        /// </summary>
        public int FreeSpinsWonThisStep { get; set; }

        /// <summary>
        /// 剩餘的總免費旋轉次數。
        /// </summary>
        public int FreeSpinsTotal
        {
            get => _gameStateData.FreeGameSpinTempCount;
            set => _gameStateData.FreeGameSpinTempCount = Math.Min(value, MaxFreeSpinCount);
        }

        /// <summary>
        /// 已經完成的免費旋轉次數。
        /// </summary>
        public int FreeSpinsCompleted
        {
            get => _gameStateData.FreeGameRoundIndex;
            set => _gameStateData.FreeGameRoundIndex = value;
        }

        /// <summary>
        /// 購買免費遊戲的類型。
        /// </summary>
        public BuyType BuyType
        {
            get => _playerBetData.BuyType;
            set => _playerBetData.BuyType = value;
        }
        #endregion

        #region 回合結算資料
        /// <summary>
        /// 上一個完整回合的總贏分。
        /// </summary>
        public double LastTotalWin { get; set; }
        #endregion

        /// <summary>
        /// 遊戲資料初始化的方法。
        /// </summary>
        public void Initialize()
        {
            SlotResult = null;
            SlotResultBalance = 0;
            LastTotalWin = 0;
            SpinStep = 0;
            ResetNewRound();
        }

   

        /// <summary>
        /// 重置遊戲資料到新的一手。
        /// </summary>
        public void ResetNewRound()
        {
            SlotResult = null;
            SlotResultBalance = 0;
            BuyType = BuyType.BUY_NONE;
            SpinStep = 0;
            FreeGameStep = 0;
            InFreeGame = false;
            FreeSpinsTotal = 0;
            FreeSpinsCompleted = 0;
            FreeSpinsWonThisStep = 0;
        }
    }
}
