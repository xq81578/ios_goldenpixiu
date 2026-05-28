using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.UI;

namespace Slot001_GoldenPixiu
{
    public class GamePlayUIMediator : UIOrientationMediator<GamePlayUI>
    {
        protected override void Initialize()
        {
            ResetNewRound();
        }

        public void ShowFreeGameImageBg(bool isShow=false)
        {
            InvokeAllUIs(ui => ui.ShowFreeGameImageBg(isShow));
        }
        public void ShowAniLightPanel(bool isShow=false)
        {
           InvokeAllUIs(ui => ui.ShowAniLightPanel(isShow)); 
        }

        public void ResetNewRound()
        {
            ResetWinText();
        }

        public void ResetWinText()
        {
            new UISetWinEvent(0).Publish(this);
        }


        public void PlayPXNomormalAnimation()
        {
           InvokeAllUIs(ui => ui.PlayPXNomormalAnimation());
        }  
        [Button]
        public void PlarPXWinAnimation(float bet, double spinWin)
        {
           InvokeAllUIs(ui => ui.PlarPXWinAnimation(bet, spinWin));
        }  
     
        public async Task ChangeSjSlider(float value = -1f)
        {
           await InvokeAllUIs(ui => ui.ChangeSjSlider(value));
        }

        [Button]
        public async Task PlayFreeGameEntry()
        {
            //播放转场角色的音效
            AudioManager.PlayEffectByName("se_trans_to_free");
           
            // AudioManager.PlayEffectByName("vo_fg_enter");
            await InvokeAllUIs(ui => ui.PlayFreeGameEntry());

           
            
        }

        [Button]
        public void PlayCharacterMultipler()
        {
            // AudioManager.PlayEffectByName("vo_mul_equation");
            InvokeAllUIs(ui => ui.PlayCharacterMultipler());
        }

      
        [Button]
        public async UniTask PlayFreeGameShow()
        {
            await InvokeAllUIsAsync(ui => ui.PlayFreeGameShow());
        }

        [Button]
        public void TransGamePlayUI(bool isFreeGame)
        {
            InvokeAllUIs(ui => ui.TransGamePlayUI(isFreeGame));
            if (isFreeGame)
            {
                new FreeGameEnterEvent().Publish(this);
            }
            else
            {
                new FreeGameLeaveEvent().Publish(this);
            }
        }
    }
}
