using System;
using System.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Slot.Common.UI
{
    public class WinCelebrationUI : RewardPresentationUI
    {
        [Space(10)]
        [SerializeField]
        private Orientation _orientation = Orientation.Landscape;
        [SerializeField]
        private bool _hasOrientationSuffix = false;
        [SerializeField]
        private WinAnimationData[] _winAnimationDatas;

        //[SerializeField]
        private double _totalWin;
        //[SerializeField]
        private double _bet;
        //[SerializeField]
        private int _level = 0;
        //[SerializeField]
        private double _winScore;
        //[SerializeField]
        private bool _isProcessComplete;
        //[SerializeField]
        private bool _isSkipRequested;

        private Tween _tween;
        private StringBuilder _valueBuilder = new StringBuilder();

        private const float LevelPlayTime = 4f;
        private const float Acceleration = 0.1f;
        private const string Loop = "loop";

        private string _playingAudioName = string.Empty;
        private string _playingVoiceAudioName = string.Empty;

        /// <summary>
        /// 跑分時: 顯示最終結果; 完成跑分: 關閉介面
        /// </summary>
        /// <returns></returns>
        public override UniTask Hide()
        {
            if (!_isProcessComplete)
                return SkipToEndAsync();
            StopAudio();
            return base.Hide();
        }

        /// <summary>
        /// Main Entry: 報獎流程
        /// </summary>
        /// <param name="bet"></param>
        /// <param name="totalWin"></param>
        /// <returns></returns>
        public async UniTask ShowTotalWin(double bet, double totalWin)
        {
            if (_winAnimationDatas == null || totalWin == 0 || bet <= 0 || totalWin / bet < _winAnimationDatas[0].Ratio)
                return;

            ResetFlags(bet, totalWin);
            base.Show(_winScore.ToString(NumberFormat)).Forget();
            PlayShow(0);
            await UniTask.WaitForEndOfFrame();
            await RunProgress();
        }

        /// <summary>
        /// 跑分流程
        /// </summary>
        /// <returns></returns>
        private async UniTask RunProgress()
        {
            for (int i = 0; i < _winAnimationDatas.Length; i++)
            {
                if (_isSkipRequested)
                    break;

                _level += 1;
                bool isMaxLevel = _level >= _winAnimationDatas.Length;

                if (isMaxLevel)
                {
                    double countUpValue = CalculateVelocityForMaxLevel();
                    await AcceleratingScoreAsync(countUpValue);
                }
                else
                {
                    double nextTargetValue = NextTargetValue(isMaxLevel, _level);
                    bool isEnd = _winAnimationDatas[_level].Ratio * _bet > _totalWin;
                    float duration = isEnd ? LevelPlayTime / 2 : LevelPlayTime;
                    await TweenScoreToAsync(nextTargetValue, duration);

                    if (isEnd)
                    {
                        break;
                    }

                    if (!isEnd && !_isSkipRequested)
                        PlayShow(_level);
                }
            }

            _isProcessComplete = true;
            StopAudio();

            await UniTask.Yield();
        }

        /// <summary>
        /// 跳過到最終結果
        /// </summary>
        /// <returns></returns>
        private async UniTask SkipToEndAsync()
        {
            if (_isProcessComplete)
                return;

            DisableCloseButtonUntil().Forget();
            _isSkipRequested = true;
            _tween?.Kill();

            _winScore = _totalWin;
            UpdateWinScoreDisplay();

            await UniTask.Yield();

            int targetLevel = GetLevel(_winScore, _bet);

            if (_level < targetLevel)
            {
                StopAudio();
                PlayAudio(targetLevel - 1, true);
                PlayAnimation(targetLevel - 1);
            }
        }

        /// <summary>
        /// 限定時間內完成跑分
        /// </summary>
        /// <param name="targetValue"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        private async UniTask TweenScoreToAsync(double targetValue, float duration)
        {
            _tween?.Kill();

            _tween = DOTween.To(() => _winScore, value =>
            {
                _winScore = value;
                UpdateWinScoreDisplay();
            }, targetValue, duration).SetEase(Ease.Linear);

            await _tween.AsyncWaitForCompletion();
        }

        /// <summary>
        /// 不限定時間完成跑分
        /// </summary>
        /// <param name="addValue"></param>
        /// <returns></returns>
        private async UniTask AcceleratingScoreAsync(double addValue)
        {
            double acceleration = Acceleration * _bet;

            while (_winScore < _totalWin)
            {
                _winScore += addValue * Time.deltaTime;
                if (_winScore > _totalWin)
                {
                    _winScore = _totalWin;
                }
                UpdateWinScoreDisplay();
                await UniTask.Yield();
                addValue += acceleration;
            }
        }
        
        /// <summary>
        /// 重置旗標
        /// </summary>
        /// <param name="bet"></param>
        /// <param name="totalWin"></param>
        private void ResetFlags(double bet, double totalWin)
        {
            _totalWin = totalWin;
            _bet = bet;

            _isSkipRequested = false;
            _isProcessComplete = false;

            _winScore = 0;
            _level = 0;

            _playingAudioName = string.Empty;
            _playingVoiceAudioName = string.Empty;
        }

        private string GetAnimationName(WinAnimationData data, bool isLoop = false)
        {
            string aniName = data.AnimationName + (isLoop ? data.LoopSuffix : data.IntroSuffix);
            if (_hasOrientationSuffix)
            {
                aniName += _orientation == Orientation.Landscape ? data.LandscapeSuffix : data.PortraitSuffix;
            }
            return aniName;
        }

        private double NextTargetValue(bool isMaxLevel, int level)
        {
            double nextLevelValue = isMaxLevel ? _totalWin : _winAnimationDatas[level].Ratio * _bet;
            double nextTargetValue = isMaxLevel ? _totalWin : Math.Min(nextLevelValue, _totalWin);
            return nextTargetValue;
        }

        private double CalculateVelocityForMaxLevel()
        {
            double startValue = _winAnimationDatas[^2].Ratio * _bet;
            double endValue = _winAnimationDatas[^1].Ratio * _bet;
            return (endValue - startValue) / LevelPlayTime;
        }

        private int GetLevel(double winScore, double bet)
        {
            double radio = winScore / bet;
            int dataLength = _winAnimationDatas.Length;

            for (int level = 0; level < dataLength; level++)
            {
                if (radio < _winAnimationDatas[level].Ratio)
                {
                    return level;
                }
            }

            return dataLength;
        }

        private void PlayShow(int dataIndex)
        {
            PlayAnimation(dataIndex);
            PlayAudio(dataIndex);
        }

        private void PlayAnimation(int dataIndex)
        {
            var data = _winAnimationDatas[dataIndex];
            _spine.Skeleton.SetToSetupPose();
            _spine.AnimationState.ClearTracks();
            _spine.AnimationState.SetAnimation(0, GetAnimationName(data), false);
            _spine.AnimationState.AddAnimation(0, GetAnimationName(data, true), true, 0);
        }

        private void PlayAudio(int dataIndex, bool onlyVoice = false)
        {
            var data = _winAnimationDatas[dataIndex];

            if (_playingVoiceAudioName != data.VoiceAudioName)
            {
                AudioManager.PlayEffectByName(data.VoiceAudioName, checkRepeat: true);
                _playingVoiceAudioName = data.VoiceAudioName;
            }

            if (!onlyVoice)
            {
                AudioManager.PlayEffectByName(data.AudioName, checkRepeat: true, endCallback: PlayAudioLoop);
                _playingAudioName = data.AudioName;
            }
        }

        private void PlayAudioLoop(string audioName)
        {
            if (!_isProcessComplete && !_isProcessComplete && audioName == _winAnimationDatas[^1].AudioName)
            {
                _playingAudioName = audioName + Loop;
                AudioManager.PlayEffectByName(_playingAudioName, loop: true, checkRepeat: true);
            }
        }

        private void StopAudio()
        {
            if (!string.IsNullOrEmpty(_playingAudioName))
            {
                AudioManager.StopEffectByName(_playingAudioName);
            }

            if (!string.IsNullOrEmpty(_playingVoiceAudioName))
            {
                AudioManager.StopEffectByName(_playingVoiceAudioName);
            }
        }

        private async UniTaskVoid DisableCloseButtonUntil()
        {
            _closeButton.interactable = false;
            await UniTask.Delay(2000);
            _closeButton.interactable = true;
        }

        /// <summary>
        /// 更新顯示的贏額文字（使用 StringBuilder 避免 GC）
        /// </summary>
        private void UpdateWinScoreDisplay()
        {
            _valueBuilder.Clear();
            _valueBuilder.Append(_winScore.ToString(NumberFormat));
            _valueText.text = _valueBuilder.ToString();
        }
    }
}
