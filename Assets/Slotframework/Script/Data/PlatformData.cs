namespace Slot.Common
{
    /// <summary>
    /// 儲存平台相關資料（如平台ID、幣別、主頁與紀錄網址）
    /// </summary>
    public class PlatformData
    {
        /// <summary>
        /// 平台 ID
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// 帳號
        /// </summary>
        public string Account { get; set; }
        public int AccountId { get; set; }
        /// <summary>
        /// 幣別
        /// </summary>
        public ECurrency CurrencyEnum { get; private set; }

        /// <summary>
        /// HomeUrl
        /// </summary>
        public string HomeUrl { get; private set; }

        /// <summary>
        /// 是否為 UFA 平台
        /// </summary>
        public bool IsUFA { get; private set; }

        /// <summary>
        /// 設定平台 ID
        /// </summary>
        public void SetId(int id) => Id = id;

        /// <summary>
        /// 設定幣別
        /// </summary>
        public void SetCurrencyEnum(ECurrency currency) => CurrencyEnum = currency;

        /// <summary>
        /// 設定 HomeUrl
        /// </summary>
        public void SetHomeUrl(string url) => HomeUrl = url;
    }
}