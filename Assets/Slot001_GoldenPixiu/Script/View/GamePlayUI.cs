using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.UI.Extensions.Tweens;

namespace Slot001_GoldenPixiu
{
    public class GamePlayUI : MonoBehaviour
    {
        [SerializeField]
        [InfoBox("背景動畫")]
        private SkeletonGraphic _bgSpine;
        [SerializeField]
        [InfoBox("免费游戏背景图片")]
        private Image _bgFreeGameImage;
        [SerializeField]
        [InfoBox("免费游戏背景图片")]
        private Image _bgFstage1;
        [SerializeField]
        [InfoBox("免费游戏背景图片")]
        private Image _bgFstage2;
        [SerializeField]
        [InfoBox("角色動畫")]
        private SkeletonGraphic _characterSpine;
        [SerializeField]
        [InfoBox("收集进度")]
        private Slider _sjSlider;
        [SerializeField]
        [InfoBox("進FreeGame動畫")]
        private SkeletonGraphic _freeGameEntrySpine;
        [SerializeField]
        private Image _freeGameEntryBG;
        [SerializeField]
        private TextMeshProUGUI _waysText;
        [SerializeField]
        private TextMeshProUGUI _valueText;

        [SerializeField, FoldoutGroup("得分特效")]
        private TextMeshProUGUI _multiplierText;
        [SerializeField, FoldoutGroup("得分特效")]
        private SkeletonGraphic _multiplierBackSpine;
        [SerializeField, FoldoutGroup("得分特效")]
        private SkeletonGraphic _multiplierFrontSpine;
        [SerializeField, FoldoutGroup("得分特效")]
        private float _multiplierFxDelay = 0.2f;
        [SerializeField, FoldoutGroup("得分特效")]
        private float _multiplierFrontDelay = 0.56f;
        [SerializeField]
        private Orientation _orientation;

        [SerializeField]
        private Image _backgroundImgBg;

        [SerializeField]
        private GameObject _aniLightPanel;

        [SerializeField]
        private Image _respin;

        private bool _isShow = false;

        private string _currentSkin = "2";

        private void Awake()
        {
            _aniLightPanel.SetActive(false);
            _sjSlider.gameObject.SetActive(false); //暂时不用进度条了

            _sjSlider.value = 0.0f;

            //设置皮肤
            SetCharacterSkin();
        }

        public void ShowAniLightPanel(bool isShow = false)
        {
            _aniLightPanel.SetActive(isShow);
        }

        //切换免费游戏的背景图
        public void ShowFreeGameImageBg(bool isShow = false)
        {
            _isShow = isShow;
            // 0. 首先设置进度条的显示状态
            // _sjSlider.gameObject.SetActive(!isShow);


            // 2. 在开始新动画前，立即终止对象上可能正在进行的旧动画，防止冲突
            _bgFreeGameImage.DOKill();
            _bgFstage1.DOKill();
            _bgFstage2.DOKill();

            // 3. 设置动画的初始透明度
            float startAlpha = isShow ? 0.0f : 1.0f;
            _bgFreeGameImage.DOFade(startAlpha, 0).SetUpdate(true);
            _bgFstage1.DOFade(startAlpha, 0).SetUpdate(true);
            _bgFstage2.DOFade(startAlpha, 0).SetUpdate(true);

            // 4. 创建动画序列（可选，但推荐用于复杂逻辑）
            // 如果三个动画的显示/隐藏逻辑完全一致，可以创建一个Sequence来统一管理
            Sequence fadeSequence = DOTween.Sequence();

            // 将三个动画加入到序列中，它们会同时开始
            fadeSequence.Join(_bgFreeGameImage.DOFade(isShow ? 1.0f : 0.0f, 0.5f).SetUpdate(true));
            fadeSequence.Join(_bgFstage1.DOFade(isShow ? 1.0f : 0.0f, 0.5f).SetUpdate(true));
            fadeSequence.Join(_bgFstage2.DOFade(isShow ? 1.0f : 0.0f, 0.5f).SetUpdate(true));

            fadeSequence.OnComplete(() =>
            {
                _aniLightPanel.SetActive(isShow);
            });
        }


        // 设置角色皮肤
        public void SetCharacterSkin()
        {
            //根据_sjSlider.value 来设置当前皮肤
            if (_sjSlider.value >= 0.0f && _sjSlider.value < 0.3f)
            {
                _currentSkin = "2";
            }
            else if (_sjSlider.value >= 0.3f && _sjSlider.value < 0.5f)
            {
                _currentSkin = "3";
            }
            else if (_sjSlider.value >= 0.5f && _sjSlider.value < 0.8f)
            {
                _currentSkin = "4";
            }
            else if (_sjSlider.value >= 0.8f && _sjSlider.value <= 1.0f)
            {
                _currentSkin = "5";
            }

            if (_characterSpine != null && _characterSpine.Skeleton != null)
            {
                try
                {
                    _characterSpine.Skeleton.SetSkin(_currentSkin);
                    _characterSpine.Skeleton.SetSlotsToSetupPose();
                    _characterSpine.AnimationState.Apply(_characterSpine.Skeleton);
                }
                catch (System.ArgumentException ex)
                {
                    Debug.LogWarning($"皮肤 '{_currentSkin}' 未找到: {ex.Message}");
                }
            }
        }

        // 打印所有可用皮肤（用于调试）
        public void PrintAvailableSkins()
        {
            if (_characterSpine != null && _characterSpine.SkeletonDataAsset != null)
            {
                var skeletonData = _characterSpine.SkeletonDataAsset.GetSkeletonData(true);
                if (skeletonData != null)
                {
                    foreach (var skin in skeletonData.Skins)
                    {
                        Debug.Log($"可用皮肤: {skin.Name}");
                    }
                }
            }
        }

        public async UniTask PlayFreeGameEntry()
        {
            // 先设置皮肤（请将"default"替换为实际的皮肤名称）
            SetCharacterSkin();

            // await ChangeSjSlider(1); //收集进度条满进度
            _characterSpine.AnimationState.SetAnimation(0, "win4", false);
            _characterSpine.AnimationState.Complete += OnAnimationComplete;
            await UniTask.Delay((int)(0.8 * 1000));
            // _characterSpine.AnimationState.AddAnimation(0, "eat2", true, 0);
        }
        private void OnAnimationComplete(Spine.TrackEntry trackEntry)
        {
            _characterSpine.AnimationState.Complete -= OnAnimationComplete;
            _characterSpine.gameObject.SetActive(false);
        }
        public void PlayCharacterMultipler()
        {
            // 先设置皮肤（请将"default"替换为实际的皮肤名称）
            SetCharacterSkin();

            _characterSpine.gameObject.SetActive(!_isShow);
            _characterSpine.AnimationState.SetAnimation(0, "eat", false);
            _characterSpine.AnimationState.AddAnimation(0, "idle", true, 0);
        }


        public void PlarPXWinAnimation(float bet, double spinWin)
        {
            // 先设置皮肤（请将"default"替换为实际的皮肤名称）
            SetCharacterSkin();

            _characterSpine.gameObject.SetActive(!_isShow);
            // string aniname = spinWin / bet > 50? "win3":"win";
            string aniname = "win";
            _characterSpine.AnimationState.SetAnimation(0, aniname, true);
        }

        public void PlayPXNomormalAnimation()
        {
            // 先设置皮肤（请将"default"替换为实际的皮肤名称）
            SetCharacterSkin();

            _characterSpine.gameObject.SetActive(!_isShow);
            _characterSpine.AnimationState.SetAnimation(0, "idle", true);
        }


        [Button]
        public async UniTask PlayFreeGameShow()
        {

            //拿到_freeGameEntrySpine的子视图respin
            if (_respin == null)
            {
                _respin = _freeGameEntrySpine.GetComponentsInChildren<Image>().FirstOrDefault(x => x.name == "respin");
            }

            _respin.gameObject.SetActive(false);
            _valueText.gameObject.SetActive(false);

            DOVirtual.DelayedCall(0.8f, () =>
            {
                _respin.gameObject.SetActive(true);
                _valueText.gameObject.SetActive(true);

                //播放免费游戏转场背景音乐
                if (ResolutionManager.Instance.CheckIsOn(this.transform))
                {
                    AudioManager.PlayOneTrackByName("mu_trans_background");
                }

            });


            var color = _freeGameEntryBG.color;
            color.a = 0.9f;
            _freeGameEntryBG.color = color;
            _freeGameEntryBG.gameObject.SetActive(true);
            string aniName = "draw";
            // _freeGameEntrySpine.AnimationState.ClearTrack(0);
            var trackEntry = _freeGameEntrySpine.AnimationState.SetAnimation(0, aniName, false);
            trackEntry.MixDuration = 0f; // 取消混合过渡，立即生效

            aniName = "loop";
            _freeGameEntrySpine.AnimationState.AddAnimation(0, aniName, true, 0);
            _freeGameEntrySpine.gameObject.SetActive(true);
            await UniTask.Delay(5000);
            aniName = "end";
            _freeGameEntrySpine.AnimationState.SetAnimation(0, aniName, false);
            await UniTask.Delay(500);
            _respin.gameObject.SetActive(false);
            _valueText.gameObject.SetActive(false);
            await _freeGameEntryBG.DOFade(0, 1).AsyncWaitForCompletion().AsUniTask();
            _freeGameEntryBG.gameObject.SetActive(false);
            _freeGameEntrySpine.gameObject.SetActive(false);



        }


        public void TransGamePlayUI(bool isFreeGame)
        {
            // _bgSpine.AnimationState.SetAnimation(0, isFreeGame ? "fg" : "mg", true);
            _bgSpine.AnimationState.SetAnimation(0, "animation", true);
        }

        //改变收集进度条的值 value=-1:根据现在的值随机增加一点，但是不能满 value=0:清零 value>0:设置值
        public async UniTask ChangeSjSlider(float value = -1f)
        {
            if (_sjSlider.value > 0.85 && value == -1f)
            {
                return;
            }
            if (value == 0)
            {
                _sjSlider.value = 0;
                return;
            }
            //动画设置显示_sjSlider.value的值
            if (value < 0)
            {

                //收集粒子貔貅手上的香炉动效
                SetCharacterSkin();
                _characterSpine.AnimationState.SetAnimation(0, "eat", false);
                _characterSpine.AnimationState.AddAnimation(0, "idle", true, 0);

                //6分之1的概率去增加
                if (Random.Range(0, 6) == 3)
                {
                    float newValue = _sjSlider.value + Random.Range(0.01f, 0.1f);
                    if (newValue >= 1)
                        newValue = 0.85f;

                    // PlayCharacterMultipler();
                    // await UniTask.Delay(9800);


                    await _sjSlider.DOValue(newValue, 0.5f).AsyncWaitForCompletion().AsUniTask();
                    SetCharacterSkin();

                    //设置_sjSlider透明度为0.5
                    // await _sjSlider.DOColor(new Color(1, 1, 1, 0.5f), 0.5f).AsyncWaitForCompletion().AsUniTask();
                }
                else
                {
                    await _sjSlider.DOValue(_sjSlider.value, 0.5f).AsyncWaitForCompletion().AsUniTask();
                }

            }
            else
            {
                await _sjSlider.DOValue(value, 0.5f).AsyncWaitForCompletion().AsUniTask();
            }

        }

        //测试跑马灯
        [Button]
        private void TestRunMarquee()
        {
            new BroadcastMessageEvent(BroadcastMessageType.EpicWin, "恭喜xxx玩家获得奖励xxxx").Publish(this);
        }

    }
}
