#region ErrorCode
public enum ErrorCode
{
    /// <summary>
    /// 遊戲命令內容無法解析
    /// </summary>
    CommandParseError = 1,
    /// <summary>
    /// 找不到此遊戲命令
    /// </summary>
    CommandNotFound = 2,
    /// <summary>
    /// 遊戲機率無回應
    /// </summary>
    ProbabilityNoResponse = 3,
    /// <summary>
    /// 餘額不足
    /// </summary>
    InsufficientBalance = 4,
    /// <summary>
    /// token失效
    /// </summary>
    TokenInvalid = 5,
    /// <summary>
    /// 重複呼叫登入
    /// </summary>
    DuplicateLogin = 6,
    /// <summary>
    /// 被踢出遊戲(同帳號後踢前, 或平台踢人)
    /// </summary>
    Kicked = 7,
    /// <summary>
    /// 無此遊戲
    /// </summary>
    GameNotFound = 8,
    /// <summary>
    /// 無此平台
    /// </summary>
    PlatformNotFound = 9,
    /// <summary>
    /// 超過最大贏分/倍數
    /// </summary>
    ExceedMaxWin = 10,
    /// <summary>
    /// 無此幣別(或是平台登入後卻換了幣別下注)
    /// </summary>
    CurrencyNotFound = 11,
    /// <summary>
    /// 數值不合法
    /// </summary>
    InvalidValue = 12,
    /// <summary>
    /// 投注額不允許
    /// </summary>
    InvalidBetAmount = 13,
    /// <summary>
    /// 不允許的IP位址
    /// </summary>
    InvalidIPAddress = 14,
    /// <summary>
    /// 平台API錯誤
    /// </summary>
    PlatformApiError = 100,
    /// <summary>
    /// RTP指定錯誤
    /// </summary>
    RTPError = 1000,
    /// <summary>
    /// 購買類型錯誤
    /// </summary>
    BuyTypeError = 1001,
    /// <summary>
    /// 找不到指定的機率設定
    /// </summary>
    ProbabilitySettingNotFound = 1002,
    /// <summary>
    /// 機率產出盤面錯誤
    /// </summary>
    ProbabilitySpinError = 1003
}
#endregion