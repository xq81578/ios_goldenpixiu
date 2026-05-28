using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Slot.Common.UI
{
    public enum NumericKeypadMode
    {
        Integer,
        Decimal
    }

    /// <summary>
    /// 全场景共享的虚拟数字键盘（运行时创建，固定在 rootCanvas 底部、宽度铺满）。
    /// </summary>
    internal static class NumericKeypadRuntime
    {
        private const int SortingOrderOffset = 250;

        private static readonly Color OverlayDim = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color KeyNormal = new Color(0.22f, 0.24f, 0.28f, 1f);
        private static readonly Color KeyTextColor = Color.white;
        private static readonly Color PreviewBgColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color PanelBgColor = new Color(0.12f, 0.13f, 0.15f, 0.98f);

        private static bool _built;
        private static Canvas _attachedRootCanvas;
        private static TMP_InputField _target;
        private static NumericKeypadMode _mode;
        private static int _maxChars = 18;
        private static Button _dotButton;
        private static GameObject _overlayGo;
        private static GameObject _modalRoot;
        private static RectTransform _panel;
        private static TextMeshProUGUI _previewText;

        public static bool CloseOnOverlayClick { get; set; } = true;

        public static void ShowFor(TMP_InputField field, NumericKeypadMode mode, float keypadHeight, int maxChars)
        {
            if (field == null)
                return;

            var canvas = field.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[NumericKeypad] TMP_InputField 需在 Canvas 下。");
                return;
            }

            EnsureBuilt(canvas.rootCanvas);

            if (_modalRoot == null || _panel == null)
                return;

            if (_target != null)
                _target.onValueChanged.RemoveListener(OnTargetValueChangedWhileOpen);

            _target = field;
            _mode = mode;
            _maxChars = maxChars;
            _target.onValueChanged.AddListener(OnTargetValueChangedWhileOpen);

            ApplyDotKeyVisualState(mode);
            SyncPreviewFromTarget();
            ApplyKeypadHeight(keypadHeight);

            _modalRoot.SetActive(true);
            _overlayGo?.SetActive(true);
            _panel.gameObject.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
            if (_overlayGo != null)
                _overlayGo.transform.SetSiblingIndex(0);
            _panel.SetAsLastSibling();

            field.readOnly = true;
            field.shouldHideMobileInput = true;
            field.ActivateInputField();
            field.MoveTextEnd(false);
        }

        public static void NotifyFieldDestroyed(TMP_InputField field)
        {
            if (field != null && _target == field)
                OnDone();
        }

        private static void ApplyKeypadHeight(float height)
        {
            if (_panel == null)
                return;
            _panel.sizeDelta = new Vector2(0f, Mathf.Max(120f, height));
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
        }

        private static void EnsureBuilt(Canvas rootCanvas)
        {
            if (rootCanvas == null)
                return;

            if (_built)
            {
                if (_attachedRootCanvas != rootCanvas)
                    ReparentModalToRootCanvas(rootCanvas);
                return;
            }

            _built = true;
            _attachedRootCanvas = rootCanvas;

            _modalRoot = new GameObject("NumericKeypadModal", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var modalRt = (RectTransform)_modalRoot.transform;
            modalRt.SetParent(rootCanvas.transform, false);
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.offsetMin = Vector2.zero;
            modalRt.offsetMax = Vector2.zero;
            var modalCanvas = _modalRoot.GetComponent<Canvas>();
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder = rootCanvas.sortingOrder + SortingOrderOffset;

            _overlayGo = new GameObject("KeypadOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
            var overlayRt = (RectTransform)_overlayGo.transform;
            overlayRt.SetParent(modalRt, false);
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            var overlayImg = _overlayGo.GetComponent<Image>();
            overlayImg.color = OverlayDim;
            overlayImg.raycastTarget = true;
            var overlayBtn = _overlayGo.GetComponent<Button>();
            overlayBtn.targetGraphic = overlayImg;
            overlayBtn.onClick.AddListener(() =>
            {
                if (CloseOnOverlayClick)
                    OnDone();
            });

            var panelGo = new GameObject("KeypadPanel", typeof(RectTransform));
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.SetParent(modalRt, false);
            _panel.anchorMin = new Vector2(0f, 0f);
            _panel.anchorMax = new Vector2(1f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.sizeDelta = new Vector2(0f, 540f);
            _panel.anchoredPosition = Vector2.zero;
            _overlayGo.transform.SetSiblingIndex(0);

            var panelCanvas = _panel.gameObject.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = modalCanvas.sortingOrder + 1;
            _panel.gameObject.AddComponent<GraphicRaycaster>();

            var panelImg = _panel.gameObject.AddComponent<Image>();
            panelImg.color = PanelBgColor;
            panelImg.raycastTarget = false;

            var vlg = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.childForceExpandWidth = true;

            _previewText = CreatePreviewRow(_panel);

            var row0 = CreateRow(_panel, 52f);
            AddKey(row0, "1", () => AppendDigit('1'));
            AddKey(row0, "2", () => AppendDigit('2'));
            AddKey(row0, "3", () => AppendDigit('3'));
            AddKey(row0, "Del", OnBackspace);

            var row1 = CreateRow(_panel, 52f);
            AddKey(row1, "4", () => AppendDigit('4'));
            AddKey(row1, "5", () => AppendDigit('5'));
            AddKey(row1, "6", () => AppendDigit('6'));
            AddKey(row1, "C", OnClear);

            var row2 = CreateRow(_panel, 52f);
            AddKey(row2, "7", () => AppendDigit('7'));
            AddKey(row2, "8", () => AppendDigit('8'));
            AddKey(row2, "9", () => AppendDigit('9'));
            _dotButton = AddKey(row2, ".", OnDot).GetComponent<Button>();

            var row3 = CreateRow(_panel, 52f);
            AddKey(row3, "0", () => AppendDigit('0'), flex: true);
            AddKey(row3, "Done", OnDone, flex: true);

            _panel.SetAsLastSibling();
            _modalRoot.SetActive(false);
        }

        /// <summary>
        /// 横竖屏常用两个独立 <see cref="Canvas"/>：键盘必须挂在「当前输入框」的 rootCanvas 下，否则会留在已隐藏或排序错误的画布上。
        /// </summary>
        private static void ReparentModalToRootCanvas(Canvas rootCanvas)
        {
            if (_modalRoot == null || rootCanvas == null)
                return;

            _attachedRootCanvas = rootCanvas;
            var modalRt = (RectTransform)_modalRoot.transform;
            modalRt.SetParent(rootCanvas.transform, false);
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.offsetMin = Vector2.zero;
            modalRt.offsetMax = Vector2.zero;

            var modalCanvas = _modalRoot.GetComponent<Canvas>();
            if (modalCanvas != null)
            {
                modalCanvas.overrideSorting = true;
                modalCanvas.sortingOrder = rootCanvas.sortingOrder + SortingOrderOffset;
            }

            if (_panel != null)
            {
                var panelCanvas = _panel.GetComponent<Canvas>();
                if (panelCanvas != null && modalCanvas != null)
                    panelCanvas.sortingOrder = modalCanvas.sortingOrder + 1;
            }

            if (_overlayGo != null)
                _overlayGo.transform.SetSiblingIndex(0);
            if (_panel != null)
                _panel.SetAsLastSibling();
        }

        private static TextMeshProUGUI CreatePreviewRow(RectTransform parent)
        {
            var go = new GameObject("PreviewRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 52f;
            le.flexibleWidth = 1f;
            var img = go.GetComponent<Image>();
            img.color = PreviewBgColor;
            img.raycastTarget = true;

            var textGo = new GameObject("PreviewText", typeof(RectTransform));
            textGo.transform.SetParent(rt, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(14f, 6f);
            trt.offsetMax = new Vector2(-14f, -6f);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.color = KeyTextColor;
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;

            return tmp;
        }

        private static RectTransform CreateRow(RectTransform parent, float preferredHeight)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.flexibleWidth = 1f;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = true;
            return rt;
        }

        private static GameObject AddKey(RectTransform row, string label, UnityEngine.Events.UnityAction onClick, bool flex = false)
        {
            var go = new GameObject("Key_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(row, false);
            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredWidth = flex ? -1f : 0f;

            var img = go.GetComponent<Image>();
            img.color = KeyNormal;
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(rt, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.color = KeyTextColor;
            tmp.fontSize = label.Length > 1 ? 22f : 26f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;

            return go;
        }

        private static void OnTargetValueChangedWhileOpen(string val)
        {
            if (_previewText != null)
                _previewText.text = val ?? string.Empty;
        }

        private static void SyncPreviewFromTarget()
        {
            if (_previewText == null)
                return;
            var t = _target != null ? _target.text : string.Empty;
            _previewText.text = t ?? string.Empty;
        }

        private static void ApplyDotKeyVisualState(NumericKeypadMode mode)
        {
            if (_dotButton == null)
                return;
            var dec = mode == NumericKeypadMode.Decimal;
            _dotButton.gameObject.SetActive(true);
            _dotButton.interactable = dec;
            var img = _dotButton.GetComponent<Image>();
            if (img != null)
            {
                img.color = dec ? KeyNormal : Color.clear;
                img.raycastTarget = true;
            }

            var label = _dotButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.enabled = dec;
        }

        private static void AppendDigit(char c)
        {
            if (_target == null)
                return;
            var t = _target.text ?? string.Empty;
            if (t.Length >= _maxChars)
                return;
            if (t == "0" && c != '.' && _mode == NumericKeypadMode.Integer)
                t = string.Empty;
            else if (t == "0" && c != '.' && _mode == NumericKeypadMode.Decimal && !t.Contains("."))
                t = string.Empty;

            t += c;
            t = Sanitize(t);
            ApplyText(t);
        }

        private static void OnDot()
        {
            if (_target == null || _mode != NumericKeypadMode.Decimal)
                return;
            var t = _target.text ?? string.Empty;
            if (t.Contains("."))
                return;
            if (t.Length >= _maxChars)
                return;
            if (t.Length == 0)
                t = "0.";
            else
                t += ".";
            ApplyText(Sanitize(t));
        }

        private static void OnBackspace()
        {
            if (_target == null)
                return;
            var t = _target.text ?? string.Empty;
            if (t.Length == 0)
                return;
            t = t.Substring(0, t.Length - 1);
            ApplyText(Sanitize(t));
        }

        private static void OnClear()
        {
            if (_target == null)
                return;
            ApplyText(string.Empty);
        }

        public static void OnDone()
        {
            if (_target == null)
            {
                Hide();
                return;
            }

            var f = _target;
            var finalText = f.text ?? string.Empty;
            f.onValueChanged.RemoveListener(OnTargetValueChangedWhileOpen);
            _target = null;

            f.DeactivateInputField(true);
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == f.gameObject)
                es.SetSelectedGameObject(null);
            f.onEndEdit.Invoke(finalText);

            Hide();
        }

        private static void Hide()
        {
            if (_modalRoot != null)
                _modalRoot.SetActive(false);
        }

        private static void ApplyText(string value)
        {
            if (_target == null)
                return;
            _target.text = value;
            _target.caretPosition = value.Length;
            _target.selectionAnchorPosition = value.Length;
            _target.selectionFocusPosition = value.Length;
            if (_previewText != null)
                _previewText.text = value ?? string.Empty;
        }

        private static string Sanitize(string t)
        {
            if (string.IsNullOrEmpty(t))
                return string.Empty;
            if (_mode == NumericKeypadMode.Integer)
            {
                var sbInt = new System.Text.StringBuilder(t.Length);
                foreach (var ch in t)
                {
                    if (ch >= '0' && ch <= '9')
                        sbInt.Append(ch);
                }

                return sbInt.ToString();
            }

            var seenDot = false;
            var sb = new System.Text.StringBuilder(t.Length);
            foreach (var ch in t)
            {
                if (ch >= '0' && ch <= '9')
                {
                    sb.Append(ch);
                    continue;
                }

                if (ch == '.' && !seenDot)
                {
                    seenDot = true;
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 挂在需要弹出虚拟数字键盘的 <see cref="TMP_InputField"/> 同一物体上。
    /// 平台判定（顺序）：<see cref="Application.isEditor"/> 走「编辑器」勾选；否则 <see cref="Application.isMobilePlatform"/> 走「移动端」勾选；否则走「PC」勾选。
    /// 键盘为全场景单例，固定在屏幕底部、宽度铺满。
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    [DisallowMultipleComponent]
    public class NumericKeypadController : MonoBehaviour
    {
        [SerializeField] private NumericKeypadMode mode = NumericKeypadMode.Decimal;

        [Tooltip("键盘区域高度（像素），底部对齐、横向铺满")]
        [SerializeField] private float keypadHeight = 540f;

        [Tooltip("允许输入的最大字符数（含小数点）")]
        [SerializeField] private int maxChars = 18;

        [Header("虚拟键盘在以下环境启用（可多项勾选）")]
        [Tooltip("勾选后：在 Unity 编辑器里按 Play 时使用虚拟键盘")]
        [SerializeField] private bool useInUnityEditor;

        [Tooltip("勾选后：非编辑器且非移动平台构建（Application.isMobilePlatform 为 false）时使用虚拟键盘，如 Standalone、多数 WebGL 等")]
        [SerializeField] private bool useOnPcBuild;

        [Tooltip("勾选后：移动平台构建（Application.isMobilePlatform 为 true）时使用虚拟键盘，如 Android、iOS 等")]
        [SerializeField] private bool useOnMobile = true;

        private TMP_InputField _field;

        private void Awake()
        {
            _field = GetComponent<TMP_InputField>();
        }

        private void OnEnable()
        {
            if (_field == null)
                _field = GetComponent<TMP_InputField>();
            if (!ShouldUseVirtualKeypad())
            {
                _field.readOnly = false;
                _field.shouldHideMobileInput = false;
                return;
            }

            _field.readOnly = true;
            _field.shouldHideMobileInput = true;
            _field.onSelect.AddListener(OnSelect);
        }

        private void OnDisable()
        {
            if (_field == null)
                return;
            _field.onSelect.RemoveListener(OnSelect);
            NumericKeypadRuntime.NotifyFieldDestroyed(_field);
        }

        private void OnDestroy()
        {
            if (_field != null)
                NumericKeypadRuntime.NotifyFieldDestroyed(_field);
        }

        private bool ShouldUseVirtualKeypad()
        {
            if (Application.isEditor)
                return useInUnityEditor;
            if (Application.isMobilePlatform)
                return useOnMobile;
            return useOnPcBuild;
        }

        private void OnSelect(string _)
        {
            if (!ShouldUseVirtualKeypad())
                return;
            NumericKeypadRuntime.ShowFor(_field, mode, keypadHeight, maxChars);
        }
    }
}
