using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Three-dot bounce loading indicator for WebView overlay.
/// </summary>
public class WebViewLoadingDots : MonoBehaviour
{
    private const int DotCount = 3;

    [SerializeField]
    private float _dotSize = 14f;

    [SerializeField]
    private float _dotSpacing = 12f;

    [SerializeField]
    private float _bounceHeight = 12f;

    [SerializeField]
    private float _bounceSpeed = 4.5f;

    [SerializeField]
    private float _phaseOffset = 0.45f;

    [SerializeField]
    private Color _dotColor = new Color(0.12f, 0.46f, 0.95f, 1f);

    private RectTransform[] _dots;
    private float[] _baseY;
    private static Sprite _whiteSprite;

    private void Awake()
    {
        BuildDots();
    }

    private void Update()
    {
        if (_dots == null)
            return;

        var time = Time.unscaledTime * _bounceSpeed;
        for (var i = 0; i < _dots.Length; i++)
        {
            var bounce = Mathf.Abs(Mathf.Sin(time + i * _phaseOffset)) * _bounceHeight;
            var anchoredPosition = _dots[i].anchoredPosition;
            anchoredPosition.y = _baseY[i] + bounce;
            _dots[i].anchoredPosition = anchoredPosition;
        }
    }

    private void BuildDots()
    {
        if (_dots != null)
            return;

        _dots = new RectTransform[DotCount];
        _baseY = new float[DotCount];

        var containerRect = transform as RectTransform;
        if (containerRect == null)
            containerRect = gameObject.AddComponent<RectTransform>();

        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;

        var totalWidth = _dotSize * DotCount + _dotSpacing * (DotCount - 1);
        containerRect.sizeDelta = new Vector2(totalWidth, _dotSize + _bounceHeight);

        var startX = -totalWidth * 0.5f + _dotSize * 0.5f;
        for (var i = 0; i < DotCount; i++)
        {
            var dotObject = new GameObject($"Dot{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dotObject.layer = gameObject.layer;
            dotObject.transform.SetParent(transform, false);

            var dotRect = dotObject.GetComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(_dotSize, _dotSize);
            dotRect.anchoredPosition = new Vector2(startX + i * (_dotSize + _dotSpacing), 0f);

            var dotImage = dotObject.GetComponent<Image>();
            dotImage.sprite = GetWhiteSprite();
            dotImage.color = _dotColor;
            dotImage.raycastTarget = false;

            _dots[i] = dotRect;
            _baseY[i] = 0f;
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;

        _whiteSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f));
        return _whiteSprite;
    }
}
