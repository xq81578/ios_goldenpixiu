// BGResult 副遊戲結果

using System;
using System.Collections.Generic;
using Best.HTTP.JSON;
using Newtonsoft.Json.Linq;

namespace Slot001_GoldenPixiu
{
    // 0: 無表演, 1: .........
    public enum EPerformanceType
    {
        NONE = 0,
    }

    [Serializable]
    public class TumbleResult
    {
        public List<List<int>> TumbleSymbol ; // 当次盘面结果
        public List<int> LineSymbol; // 连线元素 下标表示第几条线
        public List<int> LineCount; // 连线个数 下标表示第几条线
        public List<ulong> LineWin; // 连线赢分 下标表示第几条条线
        public List<long> ScoreSymbol;  //奖金符号 金额
        public ulong Win = 0; // 当次盘面赢分
        public EPerformanceType PerformanceType = EPerformanceType.NONE;
        public List<List<int>> PerformanceSymbol=new List<List<int>>(); // 表演盘面
        public List<int> PreReel = new List<int>();  //预中效果
        
        
    }

    [Serializable]
    public class MGResult
    {
        public ulong MainWin; //小游戏赢分
        public List<TumbleResult> MGTumbleList;
    }
    
    [Serializable]
    public class FGResult
    {
      
        public List<TumbleResult>  FGTumbleList; 
        public ulong FreeWin=0 ; // 免费游戏贏分
        public int FGSpinCount => FGTumbleList != null ? FGTumbleList.Count : 0; //免费游戏次数
    }

    [Serializable]
    public class SlotResult
    {
        // public int Code = -1; // 回應代碼 (參考 ErrorCode.go)
        public ulong Balance;
        // public string TxnId;
        public ulong TotalWin; // 總贏分
        // public int GameMode; // 遊戲模式
        // public int BuyType = 0; // 購買類型 (0: 未買, 1: Buy ExtraBet, 2: Buy FreeSpins, 3: Buy SuperFreeSpins)
        public JToken Extra;
        // MGResult
        public MGResult MGResult ;
        // FGResult
        public FGResult FGResult; // 免費遊戲 Spin 結果列表
    }
}