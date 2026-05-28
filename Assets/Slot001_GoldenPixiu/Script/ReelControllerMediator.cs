using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Slot001_GoldenPixiu
{
    public class ReelControllerMediator : UIOrientationMediator<ReelController>
    {
        [SerializeField]
        private int _reelStripCount = 7;

        [SerializeField, FoldoutGroup("轮转节奏参数")]
        private float _rotateSpeed = 40.0f;
        [SerializeField, FoldoutGroup("轮转节奏参数")]
        private float _stopInterval = 0.15f;
        [SerializeField, FoldoutGroup("轮转节奏参数"), InfoBox("一列轮转的持续时间")]
        private float _spinDuration = 0.4f;

        [SerializeField, FoldoutGroup("眯牌"), InfoBox("几个Scatter开始眯牌")] //当前游戏用不到
        private int _revealScatterCount = 3;
        [SerializeField, FoldoutGroup("眯牌"), InfoBox("眯牌时的旋转速度")]
        private float _revealRotateSpeed = 12.0f;

        
        #region Inject (依賴注入)
        [Inject] private GameData _gameData;
        [Inject] private GameLogic _gameLogic;
        [Inject] private PerformanceSettingSO _performanceSetting;
        #endregion

        public bool IsForceStop
        {
            set
            {
                _landscapeUI.IsForceStop = value;
                _portraitUI.IsForceStop = value;
            }
        }
        public bool IsRotating => _isRotating;
        private bool _isRotating = false;
        public bool CanStop => _landscapeUI.CanStop && _portraitUI.CanStop;

        [Button("Init")]
        protected override void Initialize()
        {
            InvokeAllUIs(ui => ui.Init(_rotateSpeed, _stopInterval, _spinDuration, _revealScatterCount,
                _revealRotateSpeed ,_gameData,_gameLogic,_performanceSetting));
        }

        public void InitializeBoard(BoardData boardData)
        {
            // 根據資料設置每個 ReelStrip 的符號
            InvokeAllUIs(ui => ui.InitializeBoard(boardData));
        }

        public void StartRotation(ReelStripGroupSO groupData,float betRatio,bool isTurbo = false)
        {
            _isRotating = true;
            List<ReelData> combReels = ReelData.GetRandomCombReelDataList(groupData, _reelStripCount,betRatio);
            // AudioManager.PlayEffectByName("se_start");
            AudioManager.PlayEffectByName("ALL_SFX_ReelSpin");
            AudioManager.PlayEffectByName("se_drop");
            InvokeAllUIs(ui => ui.StartRotation(combReels, isTurbo).Forget());
        }

        public async UniTask StopRotation(BoardData boardData, List<ReelData> combReels, List<int> endPositions, List<int> preList, bool isForceStop = false, bool isFreeGame = false)
        {
            IsForceStop = isForceStop;

            int[] scatterAccCount = boardData.GetScatterAccumulation();
            InvokeAllUIs(ui => ui.CheckWin());
            await InvokeAllUIsAsync(ui => ui.StopRotation(boardData,combReels, endPositions, preList,isFreeGame));
          
            _isRotating = false;
        }

        public void PlayScatterWinFx()
        {
            InvokeAllUIs(ui => ui.PlayScatterWinFx());
        }

        public void StopScatterFx()
        {
            InvokeAllUIs(ui => ui.StopScatterFx());
        }


        
        public void ShowWinEffect()
        {

            double win = _gameLogic.GetCurrentSpinWin(_gameData);
            if (win>0)
            {
                // AudioManager.PlayEffectByName(SlotSounds.SeFinishLine);

           
                    if (_gameLogic.CheckFreeGame(_gameData))
                    {
                        InvokeAllUIs(ui => ui.ShowAllWinLine()); //展示全部连线 后 进入免费游戏
                    }
                    else
                    {
                        InvokeAllUIs(ui => ui.ShowWin()); //单条连线依次展示
                    }
            }
          
        }

        public void ShowFGWinEffect()
        {
           
            InvokeAllUIs(ui => ui.ShowWin());
        }
      
        public async UniTask ShowScatterWinEffect()
        {
           await InvokeAllUIs(ui => ui.ShowScatterWinEffect()); 
        }
       
    }
}