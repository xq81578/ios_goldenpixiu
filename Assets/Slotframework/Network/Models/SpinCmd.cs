
// Spin 指令
public enum ESlotBuyType
{
    ExtraBet = 1,
    FreeSpin = 2,
    SuperFreeSpin = 3
}
public class SpinCmd
{
    // public int RTP = 1000; // 做異常測試再打開
    public int TotalBet;   // `json:"TotalBet"`      // 總投注額
    public bool ExtraBet;  // `json:"ExtraBet"`      // 是否額外投注
    public int BuyType;
}


public class loginCmd
{
    public string token;
}