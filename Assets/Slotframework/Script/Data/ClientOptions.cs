using UnityEngine;

namespace Slot.Common
{
    /// <summary>
    /// GameInfo 中的 ClientOptions 資料結構。
    /// </summary>
    [System.Serializable]
    public struct ClientOptions
    {
        [System.Serializable]
        private struct ClientOptionsJson
        {
            public int DefaultBetIndex;
            public bool Auto;
            public bool Turbo;
            public bool BuyBonus;
            public string Platform; // 使用字串接收
            public string[] RTP;
        }

        public int DefaultBetIndex;
        public bool IsAutoActive;
        public bool IsTurboActive;
        public bool IsBuyBonusActive;
    
        public PlatformType Platform;
        public string[] RTP;

        public ClientOptions(string json)
        {
            DefaultBetIndex = 0;
            Platform = PlatformType.NONE;
            RTP = new string[0]; // 預設指派空陣列
            IsAutoActive = true;
            IsTurboActive = true;
            IsBuyBonusActive = true;

            try
            {
                if (string.IsNullOrEmpty(json))
                {
                    LogUtils.LogWarning("JSON string is null or empty");
                    return;
                }

                ClientOptionsJson jsonData = JsonUtility.FromJson<ClientOptionsJson>(json);
                SetData(jsonData);
            }
            catch (System.Exception ex)
            {
                LogUtils.LogError($"Failed to parse ClientOptions JSON: {ex.Message}");
            }
        }
        
        private void SetData(ClientOptionsJson jsonData)
        {
            DefaultBetIndex = jsonData.DefaultBetIndex;
            Platform = ConvertToPlatformType(jsonData.Platform);
            RTP = jsonData.RTP;
            IsAutoActive = jsonData.Auto;
            IsTurboActive = jsonData.Turbo;
            IsBuyBonusActive = jsonData.BuyBonus;
        }
        
        private static PlatformType ConvertToPlatformType(string platformStr)
        {
            if (string.IsNullOrEmpty(platformStr))
                return PlatformType.NONE;
            
            // 嘗試轉換字串為列舉
            if (System.Enum.TryParse(platformStr, true, out PlatformType result))
                return result;
            
            return PlatformType.NONE;
        }
    }
}

