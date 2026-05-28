using Cysharp.Threading.Tasks;
using CriminalMakers.GameEventHub;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Slot.Common;

/// <summary>
/// 浮動計分欄 (WinTemp)
/// 
/// 支援事件 (Bottom_EventAction)：
/// - UiSetWinTemp：顯示計分，重設為 0 並加上新分數
/// - UiAddWinTemp：累加計分
/// - UiSettleWinTemp：依運算型態結算分數
/// - UiHideWinTemp：隱藏計分欄
/// - UiShowFormula：顯示運算公式
/// - UiSetWinTempEffect：切換動畫效果型態
///
/// 支援資料 (Bottom_EventData)：
/// - UiWinTempType：運算型態 (Addition, Subtraction, Multiplication, Division)
/// - UiWinTempValue：運算數值
/// - UiSetWinTempEffect：動畫效果型態
///
/// 動畫效果型態 (WinTempEffectEnum)：
/// - CountUp：數字滾動
/// - ScaleUp：放大縮放
///
/// Inspector 可調參數：
/// - _effectType：動畫型態
/// - _effectDuration：動畫時間
/// - _scaleUpValue：放大倍率
/// - _scaleUpTween：放大緩動
///
/// </summary>
namespace Slot.Common.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class WinTemp : MonoBehaviour
    {
        public enum EffectEnum
        {
            None = 0
            , CountUp     // 數字累加
            , ScaleUp     // 放大效果
        }

        [SerializeField]
        private double _winValue;
        [SerializeField]
        private EffectEnum _effectType = EffectEnum.CountUp;
        [SerializeField]
        private float _effectDuration = 0.3f;

        [Space(10)]
        [Header("放大效果")]
        [SerializeField]
        private float _scaleUpValue = 1.5f; // 放大效果的縮放值
        [SerializeField]
        private Ease _scaleUpTween = Ease.OutCubic; // 放大效果的緩動類型

        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI[] _texts;
        private RectTransform _canvasGroupRectTransform;

        private Tween _tween;

        private void OnEnable()
        {
            GameEventHub.Bind(this);
        }

        private void Oisable()
        {
            GameEventHub.Unbind(this);
        }

        private void Awake()
        {
            _canvasGroup = this.GetComponent<CanvasGroup>();
            _texts = this.GetComponentsInChildren<TextMeshProUGUI>();
            _canvasGroupRectTransform = _canvasGroup.GetComponent<RectTransform>();

            OnHideWinTemp();
        }

        private void OnSetWinTempEffect(EffectEnum effectEnum)
        {
            _effectType = effectEnum;
        }

        // 顯示計分
        private void OnSetWinTemp(double add)
        {
            _winValue = 0;
            AnimateWinTemp(add);
        }

        // 累加計分
        private void OnAddWinTemp(double add)
        {
            AnimateWinTemp(add);
        }

        // 共用動畫方法
        private void AnimateWinTemp(double add)
        {
            _canvasGroup.DOFade(1, _effectDuration);
            double currentWin = _winValue;
            double targetWin = currentWin + add;

            _tween?.Complete();
            switch (_effectType)
            {
                case EffectEnum.CountUp:
                    CountUpEffect(currentWin, targetWin);
                    break;
                case EffectEnum.ScaleUp:
                    ScaleUpEffect(targetWin);
                    break;
                default:
                    _texts[0].text = ServiceUtils.ToCurrentString(targetWin, Bottom_Define.MoneyFormat);
                    break;
            }

            _winValue = targetWin;
        }

        private async void OnSettleWinValue(int value, Bottom_MathType mathType)
        {
            double currentWin = _winValue;
            double targetWin = currentWin;

            int newVal = value;
            switch (mathType)
            {
                case Bottom_MathType.Addition:
                    targetWin += newVal;
                    break;
                case Bottom_MathType.Subtraction:
                    targetWin -= newVal;
                    break;
                case Bottom_MathType.Multiplication:
                    targetWin *= newVal;
                    break;
                case Bottom_MathType.Division:
                    targetWin /= newVal;
                    break;
            }

            _texts[1].text = FormatFormula(mathType, newVal);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_canvasGroupRectTransform);
            await UniTask.Delay(500);

            _texts[1].text = "";

            //用Tween做出winValueText有數字往上滾動的效果
            _tween?.Complete();
            _tween = DOTween.To(() => currentWin, x => currentWin = x, targetWin, _effectDuration)
                .OnUpdate(() => _texts[0].text = ServiceUtils.ToCurrentString(currentWin, Bottom_Define.MoneyFormat))
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    _winValue = targetWin;

                    if (!ResolutionManager.Instance.CheckIsOn(transform))
                        return;
                    new SettleWinTempEndEvent().Publish(this);
                });
        }

        // 隱藏
        private void OnHideWinTemp()
        {
            _canvasGroup.DOFade(0, 0f);
            _winValue = 0;
            _texts[0].text = "0.00";
            _texts[1].text = "";
        }

        private void OnShowFormula(int newValue, Bottom_MathType mathType)
        {
            var newVal = newValue;
            _texts[1].text = FormatFormula(mathType, newVal);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_canvasGroupRectTransform);
        }

        private string FormatFormula(Bottom_MathType mathType, int value)
        {
            string symbol = mathType switch
            {
                Bottom_MathType.Addition => "+",
                Bottom_MathType.Subtraction => "-",
                Bottom_MathType.Multiplication => "X",
                Bottom_MathType.Division => "÷",
                _ => ""
            };
            return $" {symbol} {value}";
        }
        #region Event Listener
        [OnGameEvent(SubscriberPriority.High)]
        private void OnSetWinTempEvent(SetWinTempEvent e)
        {
            OnSetWinTemp(e.Add);
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnAddWinTempEvent(AddWinTempEvent e)
        {
            OnAddWinTemp(e.Add);
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnSettleWinTempEvent(SettleWinTempEvent e)
        {
            OnSettleWinValue(e.NewValue, e.MathType);
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnHideWinTempEvent(HideWinTempEvent e)
        {
            OnHideWinTemp();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnShowFormulaEvent(ShowFormulaEvent e)
        {
            OnShowFormula(e.NewValue, e.MathType);
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnSetWinTempEffectEvent(SetWinTempEffectEvent e)
        {
            OnSetWinTempEffect(e.EffectType);
        }
        #endregion

        #region 表演效果
        private void CountUpEffect(double currentWin, double targetWin)
        {
            double win = currentWin;
            _tween = DOTween.To(() => win, x => win = x, targetWin, _effectDuration)
                .OnUpdate(() => _texts[0].text = ServiceUtils.ToCurrentString(win, Bottom_Define.MoneyFormat))
                .SetEase(Ease.Linear);
        }

        private void ScaleUpEffect(double targetWin)
        {
            RectTransform rectTransform = _texts[0].rectTransform;
            rectTransform.localScale = Vector3.one;
            _texts[0].text = ServiceUtils.ToCurrentString(targetWin, Bottom_Define.MoneyFormat);
            _tween = rectTransform.DOScale(_scaleUpValue, _effectDuration / 2).SetEase(Ease.OutBack).SetLoops(2, LoopType.Yoyo);
        }
        #endregion
    }
}