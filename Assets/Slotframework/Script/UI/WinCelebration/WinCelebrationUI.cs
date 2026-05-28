using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;


using WinAnimationData = WinCelebrationUIMediator.WinAnimationData;
using WinType = WinCelebrationUIMediator.WinType;

public class WinCelebrationUI : MonoBehaviour
{
    [SerializeField]
    private GameObject _winScorePanel;
    [SerializeField]
    private SkeletonGraphic _winFxSpine;
    [SerializeField]
    private TextMeshProUGUI _winScoreText;
    [SerializeField]
    private Button _foreStopBtn;
    [SerializeField]
    private Image _imgBackground;

    private string _scoringSoundName;
    private string _voiceSoundName;

    [SerializeField]
    private Orientation _orientation;

    private List<WinAnimationData> _winAnimationDatas;
    private List<WinAnimationData> _epicLoopwinAnimationDatas;

    private void Awake()
    {
        if (_winFxSpine != null && _winFxSpine.AnimationState != null)
        {
            _winFxSpine.AnimationState.Data.DefaultMix = 0f;
        }
    }

    public void Init(WinCelebrationUIMediator mediator, List<WinAnimationData> winAnimationDatas, List<WinAnimationData> epicLoopwinAnimationDatas, string scoringSoundName)
    {
        _winAnimationDatas = winAnimationDatas;
        _epicLoopwinAnimationDatas = epicLoopwinAnimationDatas;
        _scoringSoundName = scoringSoundName;
        _foreStopBtn.onClick.RemoveListener(mediator.ForceStopTotalWin); // 保險清除一次
        _foreStopBtn.onClick.AddListener(mediator.ForceStopTotalWin);
    }

    public void UpdateWinScore(double winScore)
    {
        _winScoreText.text = winScore.ToString("#,##0.00");
    }

    public void SetWinScoreFx(int targetIndex, int epicTargetIndex)
    {
        bool isPortrait = _orientation == Orientation.Portrait;
        _winFxSpine.AnimationState.SetEmptyAnimation(0, 0);  // 由於需使用Start事件，當呼叫SetAnimation時，Start 事件會立即觸發，但Start事件未註冊，就不會被執行到，為了避免這個問題，先設定一個空的動畫即可正常運作。
        for (int i = 0; i <= targetIndex; i++)
        {
            var aniName = GetAnimationName(_winAnimationDatas[i], false, isPortrait);
            var audioName = _winAnimationDatas[i].audioName;
            var preAudioName = i > 0 ? _winAnimationDatas[i - 1].audioName : "";
            var voiceAudioName = _winAnimationDatas[i].voiceAudioName;
            var preVoiceAudioName = i > 0 ? _winAnimationDatas[i - 1].voiceAudioName : "";

            // 設定入場動畫
            _winFxSpine.AnimationState.AddAnimation(0, aniName, false, 0).Start += (trackEntry) =>
                {
                    //停止上一個音效
                    if (i != targetIndex && !string.IsNullOrEmpty(preAudioName))
                    {
                        AudioManager.StopEffectByName(preAudioName);
                        AudioManager.StopEffectByName(preVoiceAudioName);
                    }

                    AudioManager.PlayEffectByName(audioName, checkRepeat: true);
                    AudioManager.PlayEffectByName(voiceAudioName, checkRepeat: true);
                    _voiceSoundName = voiceAudioName;
                };

            // 設定loop動畫
            aniName = GetAnimationName(_winAnimationDatas[i], true, isPortrait);
            _winFxSpine.AnimationState.AddAnimation(0, aniName, true, 0);

        }

        if (epicTargetIndex > 0)
        {
            var aniName = GetAnimationName(_epicLoopwinAnimationDatas[0], false, isPortrait);
            var audioName = _epicLoopwinAnimationDatas[0].audioName;
            // 設定入場動畫
            _winFxSpine.AnimationState.AddAnimation(0, aniName, false, 0).Start += (trackEntry) =>
                {
                    AudioManager.PlayEffectByName(audioName, checkRepeat: true, loop: true);
                };

            // 設定loop動畫
            aniName = GetAnimationName(_epicLoopwinAnimationDatas[0], true, isPortrait);
            _winFxSpine.AnimationState.AddAnimation(0, aniName, true, 0);
        }
    }
    
    public async UniTask FadeOutWinScore()
    {
        //不能用CanvasGroup直接抓整個，spine會壞掉
        _winFxSpine.DOFade(0f, 0.2f);
        _winScoreText.DOFade(0f, 0.2f);
        _imgBackground.DOFade(0f, 0.2f);
        await UniTask.WaitForSeconds(0.2f);
        //恢復，下次還可以用
        _winFxSpine.DOFade(1f, 0f);
        _winScoreText.DOFade(1f, 0f);
        _imgBackground.DOFade(0.64f, 0f);
    }

    public void EnableWinScorePanel(bool enable)
    {
        _winScorePanel.SetActive(enable);
    }

    public void CompleteWinAnimation(int targetIndex)
    {
        bool isPortrait = _orientation == Orientation.Portrait;
        if (targetIndex > 0)  // 避免第一個動畫重複播放
        {
            var data = _winAnimationDatas[targetIndex];
            var aniName = GetAnimationName(data, false, isPortrait);
            _winFxSpine.AnimationState.SetAnimation(0, aniName, false);
            aniName = GetAnimationName(data, true, isPortrait);
            _winFxSpine.AnimationState.AddAnimation(0, aniName, true, 0);
            if (!string.IsNullOrEmpty(data.voiceAudioName) && _voiceSoundName != data.voiceAudioName)
            {
                AudioManager.StopEffectByName(_voiceSoundName);
                AudioManager.PlayEffectByName(data.voiceAudioName, checkRepeat: true);
            }
        }
    }

    private string GetAnimationName(WinAnimationData animationData, bool isLoop, bool isPortrait)
    {
        string animationName = animationData.animationName;
        string loopSuffix = isLoop ? animationData.loopSuffix : animationData.introSuffix;
        string orientationSuffix = isPortrait ? animationData.portraitSuffix : animationData.landscapeSuffix;
        var winType = animationData.winType;
        return winType switch
        {
            WinType.BigWin => $"{animationName}{loopSuffix}{orientationSuffix}",
            WinType.MegaWin => $"{animationName}{loopSuffix}{orientationSuffix}",
            WinType.SuperWin => $"{animationName}{loopSuffix}{orientationSuffix}",
            WinType.EpicWin => $"{animationName}{loopSuffix}{orientationSuffix}",
            WinType.EpicWinLoop => $"{animationName}{loopSuffix}{orientationSuffix}",
            _ => throw new ArgumentOutOfRangeException(nameof(winType), winType, null)
        };
    }

    public void SetWinFxSpine(SkeletonDataAsset dataAsset)
    {
        _winFxSpine.skeletonDataAsset = dataAsset;
        _winFxSpine.Initialize(true);
    }

    public void SetAnimation(WinAnimationData animationData, bool isEpicLoop = false)
    {
        bool isPortrait = _orientation == Orientation.Portrait;
        AudioManager.PlayEffectByName(animationData.audioName, checkRepeat: true, loop: isEpicLoop);
        if (!string.IsNullOrEmpty(animationData.voiceAudioName))
        {
            AudioManager.PlayEffectByName(animationData.voiceAudioName, checkRepeat: true, loop: isEpicLoop);
            _voiceSoundName = animationData.voiceAudioName;
        }
        _winFxSpine.AnimationState.SetAnimation(0, GetAnimationName(animationData, false, isPortrait), false);
        _winFxSpine.AnimationState.AddAnimation(0, GetAnimationName(animationData, true, isPortrait), true, 0);
    }
}
