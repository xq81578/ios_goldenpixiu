using System;
using System.Collections.Generic;
using System.Linq;
using Slot.Common;
using UnityEngine;
using VContainer;

namespace Slot001_GoldenPixiu
{
    /// <summary>
    /// 遊戲邏輯服務 (Service)
    /// 封裝所有核心遊戲規則與計算，使其與表現層分離。
    /// </summary>
    public class GameLogic
    {
        [Inject] private PayTableSO _payTable;

        public double GetTotalBet(double bet, BuyType buyType)
        {
            return _payTable.GetTotalBet(bet, buyType);
        }

        /// <summary>
        /// 獲取當前 Tumble 步驟的結果。
        /// </summary>
        public TumbleResult GetCurrentTumbleResult(GameData data)
        {
            // 添加空值检查，防止快速点击时的异常
            if (data?.SlotResult == null)
            {
                Debug.LogWarning("SlotResult is null, returning default TumbleResult");
                return new TumbleResult();
            }
            
            if (data.InFreeGame)
            {
                if (data.SlotResult.FGResult?.FGTumbleList == null || data.FreeGameStep < 0 || data.FreeGameStep >= data.SlotResult.FGResult.FGTumbleList.Count)
                {
                    Debug.LogWarning($"FGResult data is invalid, returning default TumbleResult. FreeGameStep: {data.FreeGameStep}, FGTumbleList count: {data.SlotResult.FGResult?.FGTumbleList?.Count}");
                    return new TumbleResult();
                }
                return data.SlotResult.FGResult.FGTumbleList[data.FreeGameStep];
            }
            else
            {
                if (data.SlotResult.MGResult?.MGTumbleList == null || data.SlotResult.MGResult.MGTumbleList.Count == 0)
                {
                    Debug.LogWarning("MGResult data is invalid, returning default TumbleResult");
                    return new TumbleResult();
                }
                return data.SlotResult.MGResult.MGTumbleList[0];
            }
        }

        /// <summary>
        /// 獲取當前這一手 Spin 的總贏分。
        /// </summary>
        public double GetCurrentSpinWin(GameData data)
        {
            if (data?.SlotResult == null)
                return 0;
            if (data.InFreeGame)
            {
                var fg = data.SlotResult.FGResult?.FGTumbleList;
                if (fg == null || data.FreeGameStep < 0 || data.FreeGameStep >= fg.Count)
                    return 0;
                return ServiceUtils.ToClientBalance(fg[data.FreeGameStep].Win);
            }
            var mg = data.SlotResult.MGResult?.MGTumbleList;
            if (mg == null || mg.Count == 0)
                return 0;
            return ServiceUtils.ToClientBalance(mg[0].Win);
        }


        /// <summary>
        /// 此次結果總贏分
        /// </summary>
        public double GetTotalWin(GameData data)
        {
            if (data?.SlotResult == null)
                return 0;
            return ServiceUtils.ToClientBalance(data.SlotResult.TotalWin);
        }

        /// <summary>
        /// 随机一个當手停輪位置。
        /// </summary>
        public List<int> GetCurrentEndPos(GameData data)
        {
            List<int> endPos = new List<int>();
            for (int i = 0; i < data.SlotResult.MGResult.MGTumbleList[0].TumbleSymbol.Count; i++)
            {
                endPos.Add(_random.Next(0, 20));
            }
            return endPos;
        }
    
        public BoardData Trans2BoardData(TumbleResult tumbleResult)
        {
            BoardData boardData = new();
            int reelCount = tumbleResult.TumbleSymbol.Count;
            // 创建一个全局的ScoreSymbol副本，确保所有卷轴共享同一个奖金金额列表
            var scoreSymbols = new List<long>(tumbleResult.ScoreSymbol);
            // 反向处理卷轴，确保从右到左正确分配奖金金额
            for (int i = reelCount - 1; i >= 0; i--)
            {
                boardData.Reels.Insert(0, new ReelData(tumbleResult.TumbleSymbol[i], scoreSymbols));
            }
            return boardData;
        }
        
        
        private static readonly System.Random _random = new();
        public List<ReelData> GetCombReelsByEndBoard(BoardData boardData,  List<int> endPositions, ReelStripGroupSO groupData ,float betRatio)
        {
            List<ReelData> combReels = new();
              int reelCount = boardData.Reels.Count; 
            
            
            
            for (int i = 0; i < reelCount; i++)
            {
                int randomNum = _random.Next(50, 100);
                combReels.Add(ReelData.GetCombReelData(groupData, i,randomNum,betRatio)); 
                for (int j = 0; j < boardData.Reels[i].Cells.Count; j++)  // 根據真實盤面，替換轉輪帶的資料(是否金框、size等等)
                {
                    int endPos = endPositions[i] + j;
                    if (endPos >= combReels[i].Cells.Count)
                    {
                        endPos %= combReels[i].Cells.Count;
                    }
                    combReels[i].Cells[endPos] = boardData.Reels[i].Cells[j];
                }
            }
            return combReels;
        }



        /// <summary>
        /// 獲取盤面上 Scatter 符號的數量。
        /// </summary>
        public int GetScatterCount(TumbleResult tumbleResult)
        {
            // 添加空值检查，防止ArgumentNullException
            if (tumbleResult?.TumbleSymbol == null)
            {
                Debug.LogWarning("TumbleResult or TumbleSymbol is null, returning 0 scatter count");
                return 0;
            }
            
            int index = (int)SymbolEnum.SS;
            return tumbleResult.TumbleSymbol.Sum(col => col?.Count(symbol => symbol == index) ?? 0);
        }

        /// <summary>
        /// 根據 Scatter 數量判斷是否贏得免費遊戲。
        /// </summary>
        public bool IsScatterWin(int scatterCount)
        {
            return CalculateFreeSpinsWon(scatterCount) > 0;
        }
        
        public bool CheckFreeGame(GameData data)
        {
            
            return data.SlotResult.FGResult.FGSpinCount > 0;
        }


        /// <summary>
        /// 根據 Scatter 數量計算贏得的免費旋轉次數。
        /// </summary>
        public int CalculateFreeSpinsWon(int scatterCount)
        {
            return  8;
        }

        /// <summary>
        /// 獲取當前盤面所有贏分 (單次 Tumble)，含乘倍。
        /// </summary>
        public double GetAddWin(TumbleResult tumbleResult)
        {
            return ServiceUtils.ToClientBalance(tumbleResult.Win);
        }


    }
}
