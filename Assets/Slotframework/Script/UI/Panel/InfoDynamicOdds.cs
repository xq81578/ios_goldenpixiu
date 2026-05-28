using System.Collections.Generic;
using Newtonsoft.Json;
using Slot.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Slot.Common.UI
{
    /// <summary>
    /// 动态赔付显示组件。
    /// 典型用法：挂在某个 symbol 的赔付表 Text 上，根据 PayTableSO + PlayerBetData
    /// 实时计算当前 bet 下的赔付金额，并将整块文本一次性写入。
    ///
    /// 支持两种显示规则：
    /// 1. 数量 + 赔付金额（6    1.60 / 5    1.20 / ...）
    /// 2. 仅赔付金额（不显示数量列）
    /// 具体显示规则由 PayTableSO.SymbolOdds.DisplayMode 决定。
    /// </summary>
    public class InfoDynamicOdds : MonoBehaviour
    {
  

        [SerializeField]
        private List<TMP_Text> _oddsText;

        [SerializeField]
        private int _ways = 1;        // ways slots 时可>1，普通线型保持 1


        [Inject]
        private PlayerBetData _playerBetData;
        [Inject]
        private PayTableSO _payTableSO;

        private void Awake()
        {
      
        }

        private void OnEnable()
        {
            // Refresh();
        }

        /// <summary>
        /// 在 bet 或语言发生变化后，可从外部调用此方法刷新显示。
        /// </summary>
        public void Refresh()
        {
            if (_payTableSO == null)
            {
                Debug.LogWarning("[InfoDynamicOdds] PayTableSO 未设置，无法显示赔率。");
                return;
            }

            if (_playerBetData == null)
            {
                Debug.LogWarning("[InfoDynamicOdds] PlayerBetData 未注入，无法根据当前 Bet 计算赔付。");
                return;
            }

            if (_oddsText == null)
            {
                Debug.LogWarning("[InfoDynamicOdds] Odds Text 未绑定。");
                return;
            }

            for (int k = 0; k < _oddsText.Count; k++)
            {
                IReadOnlyList<SymbolPayEntry> entries = _payTableSO.GetDisplayEntries(k);
                PaytableDisplayMode mode = _payTableSO.GetDisplayMode(k);

                if (entries == null || entries.Count == 0)
                {
                    // 该 Symbol 无赔付配置，清空对应文本但继续处理其它 Symbol
                    _oddsText[k].text = string.Empty;
                    continue;
                }

                double totalBet = _playerBetData.Bet;
                BuyType buyType = _playerBetData.BuyType;

                // 先计算所有赔付字符串和最大长度，用于后续按长度调整空格
                var amountStrings = new List<string>(entries.Count);
                int maxLen = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    int consecutiveCount = entries[i].ConsecutiveCount;
                    double payout = _payTableSO.GetPayoutForUI(totalBet, buyType, k, consecutiveCount, _ways);
                    string payoutStr = payout.ToString("0.00");
                    amountStrings.Add(payoutStr);
                    if (payoutStr.Length > maxLen)
                        maxLen = payoutStr.Length;
                }

                var sb = new System.Text.StringBuilder();

                // 根据上一行长度与当前长度的差值动态增减空格
                int currentSpaces = 4;
                int prevLen = -1;

                for (int i = 0; i < entries.Count; i++)
                {
                    SymbolPayEntry entry = entries[i];
                    string label = entry.Label ?? string.Empty;

                    string payoutStr = amountStrings[i];

                    if (mode == PaytableDisplayMode.CountAndPayout)
                    {
                        if (prevLen >= 0)
                        {
                            int lenDiff = prevLen - payoutStr.Length;
                            currentSpaces += (lenDiff*2);
                            if (currentSpaces < 1)
                                currentSpaces = 1;
                        }

                        prevLen = payoutStr.Length;

                        sb.Append(label);
                        sb.Append(new string(' ', currentSpaces));
                        sb.AppendLine(payoutStr);
                    }
                    else
                    {
                        // 仅显示赔付金额，例如 "1.60"
                        sb.AppendLine(payoutStr);
                    }
                }

                _oddsText[k].text = sb.ToString();
            }
        }
    }
}

