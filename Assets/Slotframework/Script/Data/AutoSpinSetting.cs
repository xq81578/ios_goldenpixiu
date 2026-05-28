using System.Collections.Generic;
using DG.Tweening;
using VContainer;

namespace Slot.Common
{
    public class AutoSpinSetting
    {
        public List<int> TotalSpinOptions { get; }
        public int TotalSpins { get; set; }
        public bool InfiniteSpins { get; set;}
        public bool IsStopIfFreeGameActive { get; set; }

        [Inject]
        private GameStateData _gameStateData;

        public AutoSpinSetting()
        {
            TotalSpinOptions = new() { 10, 25, 50, 100, int.MaxValue };
            TotalSpins = TotalSpinOptions[0];
            IsStopIfFreeGameActive = false;
        }

        public void SetAutoSpinEnabled()
        {
            _gameStateData.IsAuto = true;
            InfiniteSpins = TotalSpins == int.MaxValue;
            _gameStateData.AutoSpinTotalSpins = TotalSpins;
        }

        public bool UpdateAutoSpinState()
        {
            bool isAuto = _gameStateData.IsAuto;

            if (_gameStateData.IsFreeGame)
            {
                if (IsStopIfFreeGameActive)
                    CancelAutoSpin();
                return true;
            }

            if (InfiniteSpins && isAuto)
                return true;

            if (_gameStateData.AutoSpinTotalSpins <= 0)
            {
               
                CancelAutoSpin();
                
                
                isAuto = false;
            }

            return isAuto;
        }

        public void MinusAutoSpinCount()
        {
            if (_gameStateData.IsAuto && _gameStateData.AutoSpinTotalSpins > 0 && !InfiniteSpins)
            {
                _gameStateData.AutoSpinTotalSpins--;
            }
        }

        public void CancelAutoSpin()
        {
            _gameStateData.IsAuto = false;
            _gameStateData.AutoSpinTotalSpins = 0;
            InfiniteSpins = false;
            new UIAutoSpinStopClickEvent().Publish(this);
        }

        public void SetValue(AutoSpinOptionTypeEnum optionTypeEnum, int index)
        {
            switch (optionTypeEnum)
            {
                case AutoSpinOptionTypeEnum.TotalSpins:
                    TotalSpins = TotalSpinOptions[index];
                    break;
                default:
                    LogUtils.LogWarning($"Unknown AutoSpinOptionTypeEnum: {optionTypeEnum}");
                    break;
            }
        }

        public void SetActive(AutoSpinOptionTypeEnum optionTypeEnum, bool isActive)
        {
            switch (optionTypeEnum)
            {
                case AutoSpinOptionTypeEnum.StopIfFreeGameIsActive:
                    IsStopIfFreeGameActive = isActive;
                    break;
                default:
                    LogUtils.LogWarning($"Unknown AutoSpinOptionTypeEnum: {optionTypeEnum}");
                    break;
            }
        }
    }
}

