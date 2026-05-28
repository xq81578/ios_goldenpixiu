using Cysharp.Threading.Tasks;
using Spine;
using Spine.Unity;


public static class SpineExtensions
{
    /// <summary>
    /// 播放指定的Spine動畫並以Task等待播放結束。
    /// </summary>
    /// <param name="animationState">Spine的AnimationState實例</param>
    /// <param name="trackIndex">要設定的Track Index，通常為0</param>
    /// <param name="animationName">動畫名稱</param>
    /// <param name="loop">是否Loop播放</param>
    /// <returns>Task，若非Loop則會在動畫結束時完成。</returns>
    public static async UniTask PlayAnimationAsync(this AnimationState animationState, int trackIndex, string animationName, bool loop = false)
    {
        var trackEntry = animationState.SetAnimation(trackIndex, animationName, loop);

        // 若是Loop動畫，永不結束，直接回傳完成任務
        if (loop)
        {
            return;
        }

        // 建立UniTaskCompletionSource，在Complete事件觸發時完成Task
        var tcs = new UniTaskCompletionSource<bool>();

        void onComplete(TrackEntry entry)
        {
            // 一旦完成，解除事件訂閱（以防因其它地方共用導致重複呼叫）
            trackEntry.Complete -= onComplete;
            tcs.TrySetResult(true);
        }

        trackEntry.Complete += onComplete;
        await tcs.Task;
    }

    /// <summary>
    /// 非同步等待 Spine Animation 播放完成。
    /// 若動畫為loop播放，則此方法將不會真正等待結束(直接結束)。
    /// </summary>
    /// <param name="trackEntry">要等待的 TrackEntry</param>
    /// <returns>Task，動畫完成時Task結束</returns>
    public static async UniTask AsyncWaitForCompletion(this TrackEntry trackEntry)
    {
        // 若 trackEntry 或 Animation 無效，直接返回
        if (trackEntry == null || trackEntry.Animation == null)
        {
            return;
        }

        // 若為迴圈動畫，代表不會有結束，直接返回完成
        if (trackEntry.Loop)
        {
            return;
        }

        var tcs = new UniTaskCompletionSource<bool>();
        void onComplete(TrackEntry entry)
        {
            // 解除事件，避免 Memory Leak
            trackEntry.Complete -= onComplete;
            tcs.TrySetResult(true);
        }

        trackEntry.Complete += onComplete;

        // 等待動畫完成事件觸發
        await tcs.Task;
    }

    /// <summary>
    /// 判斷是否存在指定名稱的動畫
    /// </summary>
    /// <param name="animationName">動畫名稱</param>
    /// <returns>如果動畫存在，返回 true；否則返回 false</returns>
    public static bool HasAnimation(this SkeletonGraphic spine, string animationName)
    {
        SkeletonData skeletonData = spine.Skeleton.Data;
        return skeletonData.FindAnimation(animationName) != null;
    }
}
