namespace Slot.Common.UI
{
    public class MaxWinUI : RewardPresentationUI
    {
        public bool Active => _spine.SkeletonDataAsset != null;
    }
}
