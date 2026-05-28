using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Slot001_GoldenPixiu
{
    public enum SymbolEnum : int
    {
        NN = 0,
        M1 = 10,
        M2 = 11,
        M3 = 12,
        M4 = 13,
        M5 = 14,
        M6 = 15,
        WW = 16,
        SS = 17,

    }

    [Serializable]
    public class BoardData
    {
        public List<ReelData> Reels = new();
        
        public int[] GetScatterAccumulation()
        {
            int[] accumulation = new int[Reels.Count - 1];
            int finalReelCellCount = Reels[^1].Cells.Count;
            int count = 0;
            for (int i = 0; i < Reels.Count - 1; i++)
            {
                for (int j = 0; j < Reels[i].Cells.Count; j++)
                {
                    if (Reels[i].Cells[j].IsScatter)
                    {
                        count++;
                    }
                }
                int checkIndex = finalReelCellCount - 1 - i;
                if (checkIndex > 0 && Reels[^1].Cells[checkIndex].IsScatter)
                {
                    count++;
                }
                accumulation[i] = count;
            }
            return accumulation;
        }

    
    }

    [Serializable]
    public class ReelData
    {
        public List<CellData> Cells;

        public ReelData()
        {
            Cells = new List<CellData>();
        }

        public ReelData(List<int> cellSymbols,List<long> moneys)
        {
            Cells = new List<CellData>();
            for (int i = 0; i < cellSymbols.Count; i++)
            {
                float money = 0;
                if (cellSymbols[i] == (int)SymbolEnum.SS)
                {
                    
                    money = (float)ServiceUtils.ToClientBalance((ulong)moneys[^1]);
                    moneys.RemoveAt(moneys.Count - 1);
                }
                Cells.Add(new CellData(cellSymbols[i],money));
            }
        }
        private static readonly double[] Moneys = new double[]{500,300,200,100,50,30,25,20,15,12,10,8,6,5,4,3,2.5,2,1.5,1,0.5};

        private static readonly Random _random = new();
        public static ReelData GetCombReelData(ReelStripGroupSO groupData, int reelIndex,int randomNum,float betRatio)
        {
            var reelStrip = groupData.ReelStripGroups[0].ReelStrips[reelIndex];
            ReelData reelData = new()
            {
                Cells = new List<CellData>()
            };
       
            for (int i = 0; i < randomNum; i++)
            {
                string randomSymbol = reelStrip.Symbols[_random.Next(reelStrip.Symbols.Count)];
                float money = 0;
                if ((int)Enum.Parse(typeof(SymbolEnum), randomSymbol)  == (int)SymbolEnum.SS)
                {
                    money = (float) Moneys[_random.Next(Moneys.Length)] * betRatio;
                }
                reelData.Cells.Add(new CellData(randomSymbol,money));
            }
            return reelData;
        }

        public static List<ReelData> GetRandomCombReelDataList(ReelStripGroupSO groupData, int reelStripCount ,float betRatio)
        {
            int randomNum = _random.Next(160, 250);

            List<ReelData> combReels = new();
            for (int i = 0; i < reelStripCount; i++)
            {
                combReels.Add(GetCombReelData(groupData,  i,randomNum,betRatio));
            }
            return combReels;
        }
    }

    
   
    [Serializable]
    public class CellData
    {
        public int Id; // 符號ID
        public string Name; // 符號名稱
        public bool IsWild; // 是否為wild
        public bool IsScatter; // 是否為scatter
        public double WildMoney; //wild 显示金额

        public CellData(string name,  float wildMoney=0)
        {
            Id = (int)Enum.Parse(typeof(SymbolEnum), name);
            Name = name;
            IsWild = Id == (int)SymbolEnum.WW;
            IsScatter = Id == (int)SymbolEnum.SS;
            WildMoney = wildMoney;
        }

        public CellData(int SymbolId ,  float wildMoney)
        {
            //这里前端数据 0 是空 服务器数据 -1 是空
            SymbolId =  SymbolId== -1?(int)SymbolEnum.NN:SymbolId;
            Id = SymbolId;
            Name = Enum.GetName(typeof(SymbolEnum), Id);
            IsWild = Id == (int)SymbolEnum.WW;
            IsScatter = Id == (int)SymbolEnum.SS;
            WildMoney = wildMoney;
        }

        public void Trans2Wild()
        {
            Id = (int)SymbolEnum.SS;
            Name = Enum.GetName(typeof(SymbolEnum), SymbolEnum.SS);
            IsWild = true;
        }
    }
}