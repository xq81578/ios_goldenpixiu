using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// WebGL 手机浏览器震动功能。
/// 封装 navigator.vibrate() API，在非 WebGL/编辑器环境自动静默降级。
/// </summary>
public static class WebGLVibration
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void VibrateDevice(int[] pattern, int patternLength);

    [DllImport("__Internal")]
    private static extern void VibrateDeviceSimple(int durationMs);

    [DllImport("__Internal")]
    private static extern void VibrateDeviceStop();
#endif

    /// <summary>单次震动，指定持续时间（毫秒）</summary>
    public static void Vibrate(int durationMs)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (durationMs <= 0) return;
        VibrateDeviceSimple(durationMs);
#endif
    }

    /// <summary>按模式震动：数组中每个元素依次为 [震动ms, 停歇ms, 震动ms, ...]</summary>
    public static void VibratePattern(int[] pattern)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (pattern == null || pattern.Length == 0) return;
        VibrateDevice(pattern, pattern.Length);
#endif
    }

    /// <summary>停止正在进行的震动</summary>
    public static void Stop()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        VibrateDeviceStop();
#endif
    }
}
