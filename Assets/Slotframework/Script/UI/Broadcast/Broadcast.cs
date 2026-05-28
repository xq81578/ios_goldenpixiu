using System;
using System.Collections.Generic;
using System.Threading;
using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slot.Common.UI
{
    [RequireComponent(typeof(Canvas))]
    public class Broadcast : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private RectTransform _backgroundRect;
        [SerializeField]
        private RectTransform _contentRect;
        [SerializeField]
        private TextMeshProUGUI _contentText;

        [Header("Panel Animation")]
        [SerializeField]
        private float _hiddenY = 0f;
        [SerializeField]
        private float _shownY = -50f;
        [SerializeField]
        private float _showDuration = 0.25f;
        [SerializeField]
        private float _hideDuration = 0.2f;
        [SerializeField]
        private Ease _showEase = Ease.OutCubic;
        [SerializeField]
        private Ease _hideEase = Ease.InCubic;

        [Header("Marquee")]
        [SerializeField]
        private float _moveSpeed = 300f;
        [SerializeField]
        private float _messageInterval = 0.15f;
        [SerializeField]
        private float _minimumMoveDuration = 0.1f;
        [SerializeField]
        private bool _ignoreTimeScale = true;

        private readonly List<BroadcastQueueItem> _pendingMessages = new();

        private CancellationTokenSource _playCancellationTokenSource;
        private Tween _backgroundTween;
        private Tween _contentTween;
        private bool _isProcessing;
        private bool _isBackgroundVisible;
        private long _sequence;

        private const int HighestPriorityType = (int)BroadcastMessageType.MaintenanceNotice;
        private const int LowestPriorityType = (int)BroadcastMessageType.SystemNotice;

        private sealed class BroadcastQueueItem
        {
            public BroadcastQueueItem(int type, string content, long sequence)
            {
                Type = type;
                Content = content;
                Sequence = sequence;
            }

            public int Type { get; }
            public string Content { get; }
            public long Sequence { get; }
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        private void Awake()
        {
            AutoAssignReferences();
            ConfigureTextComponent();
            HideImmediately();
        }

        private void OnEnable()
        {
            AutoAssignReferences();
            ConfigureTextComponent();
            EnsureCancellationSource();
            GameEventHub.Bind(this);

            if (_pendingMessages.Count > 0 && !_isProcessing)
            {
                ProcessQueueAsync().Forget();
            }
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
            CancelProcessing();
            KillTweens();
            HideImmediately();
        }

        public void EnqueueMessage(int type, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            _pendingMessages.Add(new BroadcastQueueItem(NormalizeType(type), content.Trim(), ++_sequence));
            SortPendingMessages();

            if (isActiveAndEnabled && !_isProcessing)
            {
                EnsureCancellationSource();
                ProcessQueueAsync().Forget();
            }
        }

        public void EnqueueMessage(BroadcastMessageType type, string content)
        {
            EnqueueMessage((int)type, content);
        }

        public void ClearPendingMessages(bool hideImmediately = false)
        {
            _pendingMessages.Clear();

            if (!hideImmediately)
            {
                return;
            }

            CancelProcessing();
            KillTweens();
            HideImmediately();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnBroadcastMessageEvent(BroadcastMessageEvent e)
        {
            EnqueueMessage(e.Type, e.Content);
        }

        private async UniTaskVoid ProcessQueueAsync()
        {
            if (_isProcessing)
            {
                return;
            }

            EnsureCancellationSource();
            CancellationToken token = _playCancellationTokenSource.Token;
            _isProcessing = true;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    while (_pendingMessages.Count > 0)
                    {
                        await ShowBackgroundAsync(token);

                        BroadcastQueueItem nextMessage = DequeueNextMessage();
                        await PlayMessageAsync(nextMessage, token);

                        if (_pendingMessages.Count > 0 && _messageInterval > 0f)
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(_messageInterval),
                                ignoreTimeScale: _ignoreTimeScale,
                                cancellationToken: token);
                        }
                    }

                    await HideBackgroundAsync(token);

                    if (_pendingMessages.Count == 0)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
            finally
            {
                _isProcessing = false;

                if (isActiveAndEnabled && _pendingMessages.Count > 0)
                {
                    EnsureCancellationSource();
                    ProcessQueueAsync().Forget();
                }
            }
        }

        private async UniTask ShowBackgroundAsync(CancellationToken token)
        {
            if (_backgroundRect == null || _isBackgroundVisible)
            {
                return;
            }

            KillBackgroundTween();
            _backgroundTween = _backgroundRect
                .DOAnchorPosY(_shownY, _showDuration)
                .SetEase(_showEase)
                .SetUpdate(_ignoreTimeScale);

            await _backgroundTween.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(token);
            _isBackgroundVisible = true;
        }

        private async UniTask HideBackgroundAsync(CancellationToken token)
        {
            if (_backgroundRect == null)
            {
                return;
            }

            if (!_isBackgroundVisible)
            {
                SetBackgroundY(_hiddenY);
                return;
            }

            KillBackgroundTween();
            _backgroundTween = _backgroundRect
                .DOAnchorPosY(_hiddenY, _hideDuration)
                .SetEase(_hideEase)
                .SetUpdate(_ignoreTimeScale);

            await _backgroundTween.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(token);
            _isBackgroundVisible = false;
            ResetContentPosition();
        }

        private async UniTask PlayMessageAsync(BroadcastQueueItem message, CancellationToken token)
        {
           

            _contentText.text = message.Content;
            ForceRefreshLayout();

            float backgroundWidth = _backgroundRect.rect.width;
            float contentWidth = Mathf.Max(_contentRect.rect.width, _contentText.preferredWidth);
            float startX = backgroundWidth;
            float endX = -contentWidth;
            float distance = backgroundWidth + contentWidth;
            float duration = Mathf.Max(_minimumMoveDuration, distance / Mathf.Max(1f, _moveSpeed));
            float currentY = _contentRect.anchoredPosition.y;

            _contentRect.anchoredPosition = new Vector2(startX, currentY);

            KillContentTween();
            _contentTween = _contentRect
                .DOAnchorPosX(endX, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(_ignoreTimeScale);

            await _contentTween.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(token);
            _contentRect.anchoredPosition = new Vector2(startX, currentY);
        }

        private void ForceRefreshLayout()
        {
            Canvas.ForceUpdateCanvases();

            if (_contentText != null)
            {
                _contentText.ForceMeshUpdate();
            }

            if (_contentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
            }

            if (_backgroundRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_backgroundRect);
            }

            Canvas.ForceUpdateCanvases();
        }

        private void AutoAssignReferences()
        {
            if (_backgroundRect == null)
            {
                _backgroundRect = transform.Find("bg") as RectTransform;
            }

            if (_contentRect == null && _backgroundRect != null)
            {
                _contentRect = _backgroundRect.Find("Content") as RectTransform;
            }

            if (_contentText == null && _contentRect != null)
            {
                _contentText = _contentRect.GetComponent<TextMeshProUGUI>();
            }
        }

        private void ConfigureTextComponent()
        {
            if (_contentText == null)
            {
                return;
            }

            _contentText.enableWordWrapping = false;
            _contentText.overflowMode = TextOverflowModes.Overflow;
        }

        private void EnsureCancellationSource()
        {
            if (_playCancellationTokenSource != null)
            {
                return;
            }

            _playCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        }

        private void CancelProcessing()
        {
            if (_playCancellationTokenSource == null)
            {
                return;
            }

            _playCancellationTokenSource.Cancel();
            _playCancellationTokenSource.Dispose();
            _playCancellationTokenSource = null;
            _isProcessing = false;
        }

        private void HideImmediately()
        {
            if (_backgroundRect != null)
            {
                SetBackgroundY(_hiddenY);
            }

            if (_contentText != null)
            {
                _contentText.text = string.Empty;
            }

            ResetContentPosition();
            _isBackgroundVisible = false;
        }

        private void ResetContentPosition()
        {
            if (_contentRect == null)
            {
                return;
            }

            float currentY = _contentRect.anchoredPosition.y;
            _contentRect.anchoredPosition = new Vector2(GetBackgroundWidth(), currentY);
        }

        private void SetBackgroundY(float y)
        {
            Vector2 anchoredPosition = _backgroundRect.anchoredPosition;
            anchoredPosition.y = y;
            _backgroundRect.anchoredPosition = anchoredPosition;
        }

        private float GetBackgroundWidth()
        {
            return _backgroundRect == null ? 0f : _backgroundRect.rect.width;
        }

        private BroadcastQueueItem DequeueNextMessage()
        {
            BroadcastQueueItem queueItem = _pendingMessages[0];
            _pendingMessages.RemoveAt(0);
            return queueItem;
        }

        private void SortPendingMessages()
        {
            _pendingMessages.Sort((left, right) =>
            {
                int typeCompare = left.Type.CompareTo(right.Type);
                return typeCompare != 0 ? typeCompare : left.Sequence.CompareTo(right.Sequence);
            });
        }

        private int NormalizeType(int type)
        {
            if (type >= HighestPriorityType && type <= LowestPriorityType)
            {
                return type;
            }

            return LowestPriorityType;
        }

        private void KillTweens()
        {
            KillBackgroundTween();
            KillContentTween();
        }

        private void KillBackgroundTween()
        {
            if (_backgroundTween == null)
            {
                return;
            }

            _backgroundTween.Kill();
            _backgroundTween = null;
        }

        private void KillContentTween()
        {
            if (_contentTween == null)
            {
                return;
            }

            _contentTween.Kill();
            _contentTween = null;
        }
    }
}
