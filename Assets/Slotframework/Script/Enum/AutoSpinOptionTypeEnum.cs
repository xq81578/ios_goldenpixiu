public enum AutoSpinOptionTypeEnum//注意這邊的數字要對應到AutoSpinWindowOption的GameObject父節點的數字(Ex: "Option_0")
{
    None = -1,
    TotalSpins = 0,
    SingleWinRatioExceeds = 1,
    StopIfBalanceLessThan = 2,
    StopIfBalanceGreaterThan = 3,
    StopIfFreeGameIsActive = 4,
}