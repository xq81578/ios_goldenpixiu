using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Core.UI
{
    [RequireComponent(typeof(Button))]
    public class AntiDoubleClick : MonoBehaviour
    {
        [SerializeField]
        private float disableDuration = 1.0f; // 禁止連點的時間 (秒)
        private Button _button;
        private bool _isClicked = false;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnButtonClick);
        }

        private void OnButtonClick()
        {
            if (_isClicked)
            {
                return; // 如果已經點擊過，則不執行
            }

            _isClicked = true;
            _button.interactable = false; // 禁止按鈕交互
            _button.image.raycastTarget = false;
            StartCoroutine(EnableButtonAfterCooldown());
        }

        private IEnumerator EnableButtonAfterCooldown()
        {
            yield return new WaitForSeconds(disableDuration); // 等待冷卻時間
            ResetButton();
        }

        private void ResetButton()
        {
            _button.interactable = true; // 恢復按鈕交互
            _button.image.raycastTarget = true;
            _isClicked = false; // 重置點擊狀態
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ResetButton();
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OnButtonClick);
        }
    }
}