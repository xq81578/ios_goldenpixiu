using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Slot.Common
{
[CreateAssetMenu(fileName = "PayTableData", menuName = "ScriptableObjects/PayTableData")]
    public class PayTableSO : ScriptableObject
    {
        public int LineBet = 1;
        public int PayLine = 100;
        public List<BetRatio> BetRatios;
        public List<SymbolOdds> SymbolOdds;  // Index代表Symbol，Value代表Odds

        /// <summary>
        /// 整个游戏统一的显示模式（数量+赔付 或 仅赔付）。
        /// 如无特殊需求，请仅在这里设置，不要逐个修改 SymbolOdds.DisplayMode。
        /// </summary>
        public PaytableDisplayMode DefaultDisplayMode = PaytableDisplayMode.CountAndPayout;

        /// <summary>
        /// 整个游戏统一的展示条目（例如 6/5/4/3）。
        /// 若为空，则优先使用 SymbolOdds.DisplayEntries，最后才按 Odds 自动推断。
        /// </summary>
        public List<SymbolPayEntry> DefaultDisplayEntries = new();

        public double GetBetRatio(BuyType buyType)
        {
            return BetRatios.Find(x => x.BuyType == buyType)?.Ratio ?? 0;
        }
        public double GetBetRatio(int buyType)
        {
            return GetBetRatio((BuyType)buyType);
        }

        public double GetTotalBet(double bet, BuyType buyType)
        {
            return bet * GetBetRatio(buyType);
        }

        public double GetTotalBet(double bet, int buyType)
        {
            return GetTotalBet(bet, (BuyType)buyType);
        }

        public double GetLineBet(double totalBet, BuyType buyType)
        {
            return totalBet * LineBet / PayLine / GetBetRatio(buyType);
        }

        public double GetLineBet(double totalBet, int buyType)
        {
            return GetLineBet(totalBet, (BuyType)buyType);
        }

        /// <summary>
        /// 取得符号在 UI 上的显示模式（带连线数量，或仅显示赔率）。
        /// 当前使用全局 DefaultDisplayMode，保持整款游戏风格统一。
        /// </summary>
        public PaytableDisplayMode GetDisplayMode(int symbolId)
        {
            return DefaultDisplayMode;
        }

        /// <summary>
        /// 取得指定 symbol 在 UI 上要显示的所有条目（每条包含 Label 和 对应的连线数量）。
        /// 优先级：
        /// 1. 单个符号自定义的 DisplayEntries
        /// 2. 全局 DefaultDisplayEntries
        /// 3. 按 Odds 非 0 自动推断（Label = 连线数）
        /// </summary>
        public IReadOnlyList<SymbolPayEntry> GetDisplayEntries(int symbolId)
        {
            var symbol = SymbolOdds[symbolId];

            // 1) 符号自己的配置
            if (symbol.DisplayEntries != null && symbol.DisplayEntries.Count > 0)
            {
                return symbol.DisplayEntries;
            }

            // 2) 全局默认配置
            if (DefaultDisplayEntries != null && DefaultDisplayEntries.Count > 0)
            {
                return DefaultDisplayEntries;
            }

            // 3) 兼容旧数据：从 Odds 自动推断
            _tempEntries.Clear();
            if (symbol.Odds != null)
            {
                for (int i = 0; i < symbol.Odds.Count; i++)
                {
                    if (symbol.Odds[i] == 0)
                        continue;

                    int consecutiveCount = i ;
                    _tempEntries.Add(new SymbolPayEntry
                    {
                        Label = consecutiveCount.ToString(),
                        ConsecutiveCount = consecutiveCount
                    });
                }
            }

            return _tempEntries;
        }

        public int GetSymbolOdds(int symbolId, int consecutiveCount)
        {
            // Odds 的索引从 0 开始，连线数量是 1~N
            return SymbolOdds[symbolId].Odds[consecutiveCount ];  //避免資料錯誤，這裡不做錯誤處理
        }

        public double GetLinePayout(double lineBet, int odds)
        {
            return lineBet * odds;
        }

        public double GetLineWin(double linePayout, int ways = 1)
        {
            return linePayout * ways;
        }

        public double GetLineWin(double lineBet, int odds, int ways = 1)
        {
            return lineBet * odds * ways;
        }

        /// <summary>
        /// 给 UI 使用的快捷方法：根据玩家当前总投注和购买类型，计算某符号在指定连线数量下的赔付金额。
        /// </summary>
        public double GetPayoutForUI(double totalBet, BuyType buyType, int symbolId, int consecutiveCount, int ways = 1)
        {
            double lineBet = GetLineBet(totalBet, buyType);
            int odds = GetSymbolOdds(symbolId, consecutiveCount);
            return GetLineWin(lineBet, odds, ways);
        }

        // 仅供 GetDisplayEntries 内部使用，避免频繁分配新 List。
        private readonly List<SymbolPayEntry> _tempEntries = new();
    }

    [Serializable]
    public enum PaytableDisplayMode
    {
        CountAndPayout, // 显示 “数量 + 赔付金额”
        PayoutOnly      // 只显示赔付金额
    }

    [Serializable]
    public class SymbolPayEntry
    {
        /// <summary>
        /// UI 显示用的数量标签，例如 \"6\"、\"5\"、\"25+\"。
        /// </summary>
        public string Label;

        public int ConsecutiveCount;
    }

    [Serializable]
    public class SymbolOdds
    {
        public List<int> Odds = new();

        /// <summary>
        /// UI 需要展示的条目。若为空，则根据 Odds 非 0 的档位自动生成。
        /// </summary>
        public List<SymbolPayEntry> DisplayEntries = new();

        /// <summary>
        /// UI 显示规则：是“数量 + 赔率”，还是仅显示赔率。
        /// </summary>
        public PaytableDisplayMode DisplayMode = PaytableDisplayMode.CountAndPayout;
    }

    [Serializable]
    public class BetRatio
    {
        public BuyType BuyType;
        public float Ratio = 1;
    }
}
