using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LoadingCircle : MonoBehaviour
{
    [Header("動畫時長")]
    [SerializeField]
    private float _duration = 1f;
    [SerializeField]
    private GameObject _circle; // 圖像組件
    [SerializeField]
    private Image _mask; // 圖像遮罩組件
    private Tween _tween; // 用於儲存我們的 Tween 動畫
    private float _delay = 0f; // 延遲時間

    private void Update()
    {
        if (_delay > 0f)
        {
            _delay -= Time.deltaTime;
            if (_delay <= 0f)
            {
                _delay = 0f;
                ShowLoadingCircle();
            }
        }
    }

    public void ShowLoadingCircle(float delay = 0f)
    {
        enabled = true;
        // 要延遲顯示的話由 Update 計時後顯示
        if (delay != 0)
        {
            _delay = delay;
            return;
        }

        _circle.SetActive(true);
        _mask.enabled = true;
        if (_tween != null && _tween.IsActive())
        {
            _tween.Kill();
        }

        _tween = _circle.transform.DORotate(new Vector3(0, 0, -360), _duration, RotateMode.LocalAxisAdd)
        .SetLoops(-1, LoopType.Restart)
        .SetEase(Ease.Linear);
    }

    public void HideLoadingCircle()
    {
        _delay = 0f; // 重置延遲時間
        _circle.SetActive(false);
        _mask.enabled = false;
        enabled = false;
    }
}
