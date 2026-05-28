namespace Slot.Common
{
    [System.Serializable]
    public struct WinAnimationData
    {
        public WinCelebrationType Type;
        public int Ratio;
        public string AnimationName;
        public string IntroSuffix;
        public string LoopSuffix;
        public string LandscapeSuffix;
        public string PortraitSuffix;
        public float AnimationDuration;
        public string AudioName;
        public string VoiceAudioName;
    }
}
