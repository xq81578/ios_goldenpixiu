using CriminalMakers.GameEventHub;

/// <summary>
/// 客戶端點擊事件
/// </summary>
public class ClientClickEvent : GameEvent
{
    public int ClickId { get; private set; }

    public ClientClickEvent() {}

    public ClientClickEvent(CommonDefine.EClientClick clickEnum)
    {
        ClickId = (int)clickEnum;
    }

    public ClientClickEvent SetClick(CommonDefine.EClientClick clickEnum)
    {
        ClickId = (int)clickEnum;
        return this;
    }
}