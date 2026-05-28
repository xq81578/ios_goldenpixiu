namespace Slot.Common
{
    /// <summary>
    /// 儲存遊戲狀態資料（如自動旋轉、快速滾動、免費遊戲等）
    /// </summary>
    public class GameStateData
    {
        /// <summary>
        /// 是否自動旋轉
        /// </summary>
        public bool IsAuto { get; set; }
        public bool IsTurbo => TurboType == Bottom_TurboType.TurboQuick;

        /// <summary>
        /// 自動旋轉總次數
        /// </summary>
        public int AutoSpinTotalSpins { get; set; }

        /// <summary>
        /// 當前快速模式
        /// </summary>
        public Bottom_TurboType TurboType { get; set; } = Bottom_TurboType.TurboOff;

        /// <summary>
        /// 是否在免費遊戲狀態
        /// </summary>
        public bool IsFreeGame { get; set; }

        /// <summary>
        /// 是否鎖定 Spin
        /// </summary>
        public bool IsSpinLock { get; set; }

        /// <summary>
        /// 免費遊戲回合索引
        /// </summary>
        public int FreeGameRoundIndex { get; set; }

        /// <summary>
        /// 免費遊戲暫存次數
        /// </summary>
        public int FreeGameSpinTempCount { get; set; }
    }
}