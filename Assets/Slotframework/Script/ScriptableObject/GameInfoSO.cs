using System.Linq;
using Protobuf.Gateway;
using UnityEngine;

namespace Slot.Common
{
    /// <summary>
    /// Server 傳遞給 Client 的 遊戲基本信息。
    /// example:
    /// { "GameID": 1014, "GameCode": "AztecsTreasure", "GameType": "Slot", "RecordUrl": "https://game-history-dev.Slot.Commonburst.com", "BetRange": [ "10000", "20000", "30000", "50000", "80000", "100000", "200000", "500000", "1000000", "2000000", "9000000", "12000000", "15000000" ], "ClientOptions": "{\"DefaultBetIndex\":2,\"IsUfa\":false}", "MaxWin": 5000 }
    /// </summary>
    [CreateAssetMenu(fileName = "GameInfoData", menuName = "ScriptableObjects/GameInfoData")]
    public class GameInfoSO : ScriptableObject
    {
        [SerializeField] private int _gameID;
        [SerializeField] private string _recordUrl;
        [SerializeField] private double[] _betRange;
        [SerializeField] private int _maxWin;
        [SerializeField] private ClientOptions _clientOptions;

        public bool IsInit { get; private set; }
        public int GameID => _gameID;
        public string RecordUrl => _recordUrl;
        public double[] BetRange => _betRange;
        public int MaxWin => _maxWin;
        public ClientOptions ClientOptions => _clientOptions;

        public void SetGameInfo(GameInfoSO gameInfo)
        {
            _gameID = gameInfo.GameID;
            _recordUrl = gameInfo.RecordUrl;
            _betRange = gameInfo.BetRange;
            _maxWin = gameInfo.MaxWin;
            _clientOptions = gameInfo._clientOptions;
            IsInit = true;
        }

        public void SetRecordUrl(string urlStr)
        {
            _recordUrl = urlStr;
        }


        public void Clear()
        {
            _gameID = 0;
            _recordUrl = string.Empty;
            _betRange = null;
            _maxWin = 0;
            _clientOptions = new ClientOptions();
            IsInit = false;
        }

        public double GetDefaultBet()
        {
            if (_betRange == null || _betRange.Length == 0)
                return 0;

            int defaultBetIndex = _clientOptions.DefaultBetIndex;
            if (defaultBetIndex < 0 || defaultBetIndex >= _betRange.Length)
                defaultBetIndex = 0;

            return _betRange[defaultBetIndex];
        }

        public void SetBetRange(double[] betRange)
        {
            _betRange = betRange;
        }
        
        public string GetRecordUrlWithAccount(string account)
        {
            if (string.IsNullOrEmpty(_recordUrl) || string.IsNullOrEmpty(account))
                return string.Empty;

            return $"{_recordUrl}";
        }
    }
}
