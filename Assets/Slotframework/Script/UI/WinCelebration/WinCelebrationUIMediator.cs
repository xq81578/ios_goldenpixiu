using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class WinCelebrationUIMediator : UIOrientationMediator<WinCelebrationUI>
{
    public enum WinType
    {
        BigWin = 0,
        MegaWin = 1,
        SuperWin = 2,
        EpicWin = 3,
        EpicWinLoop = 4,
    }
    [Serializable]
    public class WinAnimationData
    {
        public WinType winType;
        public int ratio;
        public string animationName;
        public string introSuffix;
        public string loopSuffix;
        public string landscapeSuffix;
        public string portraitSuffix;
        public float animationDuration;
        public string audioName;
        public string voiceAudioName;
    }

    [SerializeField, FoldoutGroup("報獎動畫設定")]
    private float _finalLoopDuration = 4f;
    [SerializeField, FoldoutGroup("報獎動畫設定")]
    private bool _hasOrientationSuffix = true;
    [SerializeField, FoldoutGroup("報獎動畫設定")]
    private List<WinAnimationData> _winAnimationDatas;
    [SerializeField, FoldoutGroup("報獎動畫設定")]
    private List<WinAnimationData> _epicLoopAnimationDatas;

    [SerializeField, FoldoutGroup("報獎聲音設定")]
    private string _endSoundName = "bw_end";
    [SerializeField, FoldoutGroup("報獎聲音設定")]
    private string _scoringSoundName = "bw_scoring";
    [SerializeField]
    private AssetReference _spinFxAssetReference;
    private Sequence _winScoreSequence = null;
    private bool _isForeStop = false;
    private bool _isStopping = false;
    private int _targetIdx;
    private int _targetEpicIdx;
    private CancellationTokenSource _showWinCTS = new();
    private CancellationTokenSource _forceEndCTS = new();

    protected override void Initialize()
    {
        if (_winAnimationDatas == null || _winAnimationDatas.Count == 0)
        {
            _winAnimationDatas = WinCelebrationDefaultData.GetDefaultWinAnimationData(_hasOrientationSuffix);
        }

        if (_epicLoopAnimationDatas == null || _epicLoopAnimationDatas.Count == 0)
        {
            _epicLoopAnimationDatas = WinCelebrationDefaultData.GetDefaultEpicLoopAnimationData(_hasOrientationSuffix);
        }

        _winAnimationDatas.Sort((a, b) => a.ratio.CompareTo(b.ratio));
        _epicLoopAnimationDatas.Sort((a, b) => a.ratio.CompareTo(b.ratio));

        Init();
        AfterLoadAction = Init;
    }

    private void Init()
    {
        InvokeAllUIs(ui =>
        {
            ui.Init(this, _winAnimationDatas, _epicLoopAnimationDatas, _scoringSoundName);
        });

        LoadSpineAsset();
    }

    private void OnDestroy()
    {
        _showWinCTS?.Cancel();
        _showWinCTS?.Dispose();

        _forceEndCTS?.Cancel();
        _forceEndCTS?.Dispose();
    }

    private void SetTargetIndex(double radio)
    {
        _targetIdx = 0;

        for (int i = 0; i < _winAnimationDatas.Count; i++)
        {
            if (radio >= _winAnimationDatas[i].ratio)
            {
                _targetIdx = i;
            }
            else
            {
                break;
            }
        }
    }

    private void SetEpicLoopIndex(double radio)
    {
        _targetEpicIdx = 0;

        for (int i = 0; i < _epicLoopAnimationDatas.Count; i++)
        {
            if (radio >= _epicLoopAnimationDatas[i].ratio)
            {
                _targetEpicIdx = i;
            }
            else
            {
                break;
            }
        }
    }

    [Button]
    public async UniTask ShowTotalWin(double bet, double totalWin)
    {
        double radio = totalWin / bet;
        if (radio < _winAnimationDatas[0].ratio)
            return;

        // 紀錄先前播放的音樂
        string lastMusic = AudioManager.currentMusicName;

        AudioManager.OnFadeOutBGMVolume();

        _isForeStop = false;
        _isStopping = false;
        UpdateWinScore(0);
        SetTargetIndex(radio);
        SetEpicLoopIndex(radio);
        PlayWinScoreText(bet, totalWin);
        //PlayWinScoreFx();

        InvokeAllUIs(ui => ui.EnableWinScorePanel(true));

        AudioManager.PlayEffectByName(_scoringSoundName, checkRepeat: false, loop: true);

        _showWinCTS?.Cancel();
        _showWinCTS?.Dispose();
        _showWinCTS = new CancellationTokenSource();
        CancellationToken cancellationToken = _showWinCTS.Token;

        float tweenDuration = _winScoreSequence.Duration();
        // Debug.LogWarning($"total tween duration: {tweenDuration + _finalLoopDuration}");
        var waitTask = UniTask.Delay((int)((tweenDuration + _finalLoopDuration) * 1000), cancellationToken: cancellationToken);
        var forceStopTask = UniTask.WaitUntil(() => _isForeStop == true, cancellationToken: cancellationToken);

        await UniTask.WhenAny(waitTask, forceStopTask);

        AudioManager.StopEffectByName(_endSoundName);

        await InvokeAllUIsAsync(ui => ui.FadeOutWinScore());
        InvokeAllUIs(ui => ui.EnableWinScorePanel(false));

        await UniTask.Delay(200, cancellationToken: cancellationToken);

        // 恢復背景音樂
        // AudioManager.PlayOneTrackByName(lastMusic);
        AudioManager.OnFadeInBGMVolume();
    }

    public void ForceStopTotalWin()
    {
        if (!_isStopping)
        {
            CompleteWinAnimation().Forget();
        }
        else
        {
            _isForeStop = true;
        }
    }

    private void PlayWinScoreText(double bet, double totalWin)
    {
        if (_winScoreSequence != null)
            _winScoreSequence.Kill();

        _winScoreSequence = DOTween.Sequence(this);
        double accumulatedWin = 0;

        for (int i = 0; i < _winAnimationDatas.Count - 1; i++)
        {
            WinAnimationData currentData = _winAnimationDatas[i];
            WinAnimationData nextData = _winAnimationDatas[i + 1];

            double startWin = accumulatedWin;
            double targetWin = nextData.ratio * bet;

            double winDiff = totalWin - startWin;
            if (winDiff <= 0)
                break;

            // Debug.LogWarning($"[{i}]normal startWin:{startWin} targetWin:{targetWin} winDiff:{winDiff}");

            float duration = (totalWin < targetWin)
                ? AdjustDurationForWinDiff(winDiff, startWin, targetWin, currentData.animationDuration)
                : currentData.animationDuration;

            _winScoreSequence.Append(CreateWinTween(startWin, targetWin, duration, totalWin, currentData));
            accumulatedWin = targetWin;
        }

        double lastWin = _winAnimationDatas.Last().ratio * bet;
        if (totalWin > lastWin)
        {
            accumulatedWin = lastWin;
            AddEpicLoopTweens(bet, accumulatedWin, totalWin);
        }
        else
        {
            // 多判斷是否剛好達到下一階的門檻，進行一次0.1秒的動畫
            if (totalWin == accumulatedWin)
            {
                _winScoreSequence.Append(CreateWinTween(accumulatedWin, totalWin, 0.1f, totalWin, _winAnimationDatas[_targetIdx]));
            }
        }

        _winScoreSequence.OnComplete(() =>
        {
            _isStopping = true;
            StopAllWinSound();
            AudioManager.PlayEffectByName(_endSoundName);
        });

        _winScoreSequence.Play();

        // Debug.LogWarning($"tween duration: {_winScoreSequence.Duration()}");
    }

    private float AdjustDurationForWinDiff(double winDiff, double startWin, double targetWin, float baseDuration)
    {
        if (winDiff <= 0)
            return 0.1f;

        float duration = (float)(winDiff / (targetWin - startWin)) * baseDuration;
        return duration < 0.1f ? 0.1f : duration;
    }

    private Tween CreateWinTween(double startWin, double targetWin, float duration, double totalWin, WinAnimationData winAnimationData, bool isEpic = false)
    {
        double currentWin = startWin;
        return DOTween.To(() => startWin, x => currentWin = x, targetWin, duration).SetEase(Ease.Linear).Pause()
                .OnStart(() =>
                {
                    SetAnimation(winAnimationData, isEpic);
                })
                .OnUpdate(() =>
                {
                    //Debug.LogWarning($"currentWin:{currentWin} add:{(double)((targetWin - startWin) / duration)} duration:{duration}");
                    if (currentWin > totalWin)
                        currentWin = totalWin;

                    UpdateWinScore(currentWin);
                });
    }

    private void AddEpicLoopTweens(double bet, double accumulatedWin, double totalWin)
    {
        for (int i = 0; i < _epicLoopAnimationDatas.Count; i++)
        {
            WinAnimationData currentData = _epicLoopAnimationDatas[i];
            double startWin = accumulatedWin;
            double targetWin = currentData.ratio * bet;

            double winDiff = totalWin - startWin;
            if (winDiff <= 0)
                break;

            // Debug.LogWarning($"[{i}]epic loop startWin:{startWin} targetWin:{targetWin} winDiff:{winDiff}");

            float duration = (totalWin < targetWin)
                 ? AdjustDurationForWinDiff(winDiff, startWin, targetWin, currentData.animationDuration)
                 : currentData.animationDuration;

            _winScoreSequence.Append(CreateWinTween(startWin, targetWin, duration, totalWin, currentData, true));
            accumulatedWin = targetWin;
        }

        //避免分數超過設定值
        WinAnimationData lastData = _epicLoopAnimationDatas.Last();
        double lastWin = lastData.ratio * bet;

        if (totalWin > lastWin)
        {
            _winScoreSequence.Append(CreateWinTween(lastWin, totalWin, _epicLoopAnimationDatas.Last().animationDuration, totalWin, lastData, true));
        }
    }

    private void UpdateWinScore(double winScore)
    {
        InvokeAllUIs(ui => ui.UpdateWinScore(winScore));
    }

    private void SetAnimation(WinAnimationData winAnimationData, bool isEpic = false)
    {
        InvokeAllUIs(ui => ui.SetAnimation(winAnimationData, isEpic));
    }

    private async void StopAllWinSound()
    {
        await UniTask.Delay(100);

        AudioManager.StopEffectByName(_scoringSoundName);

        foreach (var data in _winAnimationDatas)
        {
            AudioManager.StopEffectByName(data.audioName);
        }

        foreach (var data in _epicLoopAnimationDatas)
        {
            AudioManager.StopEffectByName(data.audioName);
        }
    }

    private async UniTaskVoid CompleteWinAnimation()
    {
        if (_isStopping)
            return;

        _isStopping = true;
        _winScoreSequence?.Complete();

        StopAllWinSound();

        AudioManager.PlayEffectByName(_endSoundName);

        InvokeAllUIs(ui => ui.CompleteWinAnimation(_targetIdx));

        _forceEndCTS?.Cancel();
        _forceEndCTS?.Dispose();
        _forceEndCTS = new CancellationTokenSource();
        CancellationToken cancellationToken = _forceEndCTS.Token;

        var waitTask = UniTask.Delay((int)(_finalLoopDuration * 1000), cancellationToken: cancellationToken);
        var forceStopTask = UniTask.WaitUntil(() => _isForeStop == true, cancellationToken: cancellationToken);
        await UniTask.WhenAny(waitTask, forceStopTask);
        _isForeStop = true;
    }

    private void LoadSpineAsset()
    {
        if (string.IsNullOrEmpty(_spinFxAssetReference.AssetGUID))
            return;

        if (_spinFxAssetReference.OperationHandle.IsValid())
        {
            InvokeAllUIs(ui => ui.SetWinFxSpine(_spinFxAssetReference.OperationHandle.Result as SkeletonDataAsset));
            return;
        }

        _spinFxAssetReference.LoadAssetAsync<SkeletonDataAsset>().Completed += handle =>
        {
            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                var dataAsset = handle.Result;
                InvokeAllUIs(ui => ui.SetWinFxSpine(dataAsset));
            }
            else
            {
                Debug.LogError($"Failed to load asset: {_spinFxAssetReference.RuntimeKey}");
            }
        };
    }
}
