using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ButtonSlider : MonoBehaviour
{
    public Button AddButton;
    public Button SubButton;
    public TextMeshProUGUI ValueText;
    public string ValueTextFormat = "{0}";
    public int InitialValue = 0;
    public int MinValue = 0;
    public int MaxValue = 100;
    public int Step = 1;

    public UnityEvent<int> OnValueChanged;

    private int value;

    public int Value
    {
        get => value;
        set
        {
            if (value < MinValue && MinValue != -1)
            {
                value = MinValue;
            }
            if (value > MaxValue && MaxValue != -1)
            {
                value = MaxValue;
            }
            this.value = value;
            UpdateText();
            OnValueChanged?.Invoke(value);
        }
    }

    private void Awake()
    {
        Value = InitialValue;
    }

    private void Start()
    {
        AddButton.onClick.AddListener(AddValue);
        SubButton.onClick.AddListener(SubValue);
    }

    private void UpdateText()
    {
        ValueText.text = string.Format(ValueTextFormat, value);
    }

    public void AddValue()
    {
        Value += Step;
    }

    public void SubValue()
    {
        Value -= Step;
    }

    public void SetMaxValue(int value)
    {
        MaxValue = value;
    }

    public void SetMinValue(int value)
    {
        MinValue = value;
    }

    public void SetStep(int value)
    {
        Step = value;
    }
}
