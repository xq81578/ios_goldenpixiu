using TMPro;
using UnityEngine;

/// <summary>
/// 根據字串長度自動調整文字尺寸
/// 字元數越少字體越大
/// </summary>
public class TextSizeWithLength : MonoBehaviour
{
    [SerializeField]
    private int maxLength;

    [SerializeField]
    private float maxSize;

    [SerializeField]
    private float minSize;

    [SerializeField]
    private float interval;

    private TextMeshProUGUI textComponet;
    private float originSize;
    private int changedLength;


    private void Start()
    {
        textComponet = this.GetComponent<TextMeshProUGUI>();
        originSize = textComponet.fontSize;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnChangeText);
    }

    private void OnChangeText(Object obj)
    {
        if (obj != textComponet || changedLength == textComponet.text.Length) return;
        
        //根據字元數調整字體尺寸
        if (textComponet.text.Length >= maxLength)
        {
            textComponet.fontSize = minSize;
        }
        else
        {
            textComponet.fontSize = originSize + (interval * (maxLength - textComponet.text.Length));

            if (textComponet.fontSize > maxSize)
            {
                textComponet.fontSize = maxSize;
            }
        }
        
        changedLength = textComponet.text.Length;
    }

}
