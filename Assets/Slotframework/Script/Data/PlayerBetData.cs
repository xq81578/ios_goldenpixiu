namespace Slot.Common
{
    /// <summary>
    /// 儲存玩家投注與餘額相關資料
    /// </summary>
    public class PlayerBetData
    {
        /// <summary>
        /// 當前投注金額
        /// </summary>
        public double Bet { get; set; }

        /// <summary>
        /// 玩家餘額
        /// </summary>
        public double Balance { get; set; }

        /// <summary>
        /// 是否啟用額外投注
        /// </summary>
        public bool IsExtraBet { get; set; }

        /// <summary>
        /// 額外投注倍率
        /// </summary>
        public double ExtraBetRatio { get; set; }
    
        /// <summary>
        /// 購買類型
        /// </summary>
        public BuyType BuyType { get; set; }
        
        #region 統計損益
        public double NetWin => TotalWin - TotalBet;
        public double TotalBet { get; set; }
        public double TotalWin { get; set; }
        #endregion
    }
}