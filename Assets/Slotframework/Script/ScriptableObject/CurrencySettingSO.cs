using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "CurrencySetting", menuName = "ScriptableObjects/CurrencySetting")]
public class CurrencySettingSO : ScriptableObject
{
    public Sprite DefaultChangeSprite;
    public List<Currency> Currencies;

    private Currency GetDefaultCurrency()
    {
        return Currencies.Find(x => x.Code == "");
    }

    public Currency GetCurrency(ECurrency currency)
    {
        if (Currencies.Find(x => x.Code == currency.ToString()) == null)
        {
            LogUtils.LogWarning("[CurrencySetting] Not Find Currency !!");
            return GetDefaultCurrency();
        }
        return Currencies.Find(x => x.Code == currency.ToString());
    }

    public void SetCurrencyUI(ECurrency currencyEnum, Image currencyImage, TextMeshProUGUI currencyText, TextMeshProUGUI valueText, bool isDefault = false, bool setColor = true)
    {
        Currency cur = GetCurrency(currencyEnum);
        if (isDefault)
        {
            cur = GetDefaultCurrency();
        }

        if (currencyImage != null)
        {
            currencyImage.gameObject.SetActive(cur.Sprite != null);
            currencyImage.sprite = cur.Sprite;
        }

        if (currencyText != null)
        {
            currencyText.gameObject.SetActive(cur.Sprite == null);
            currencyText.text = cur.Symbol;
            if (setColor)
                currencyText.color = cur.Color;
        }

        if (setColor)
            valueText.color = cur.Color;
    }

    public Color GetCurrencyColor(ECurrency currency, bool isDefault = false)
    {
        if (isDefault)
        {
            return GetDefaultCurrency().Color;
        }
        return GetCurrency(currency).Color;
    }

    public string GetCurrencyText(ECurrency currency, bool isDefault = false)
    {
        if (isDefault)
        {
            return GetDefaultCurrency().Symbol;
        }
        return GetCurrency(currency).Symbol;
    }

    public Sprite GetCurrencySprite(ECurrency currency, bool isDefault = false)
    {
        if (isDefault)
        {
            return GetDefaultCurrency().Sprite;
        }
        return GetCurrency(currency).Sprite;
    }

    public Sprite GetCurrencyChangeSprite(ECurrency currency, bool isDefault = false)
    {
        //目前沒換幣功能，先隱藏
        return DefaultChangeSprite;
        /*
        if (isDefault)
        {
            return GetDefaultCurrency().ChangeSprite;
        }
        return GetCurrency(currency).ChangeSprite;
        */
    }
}

[Serializable]
public class Currency
{
    public string Name;
    public string Code;
    //貨幣是否需顯示成 KMB
    //public bool isKilo;
    public string Symbol;
    public Color Color = new Color32(255, 240, 180, 255);
    public Sprite Sprite;
    //換幣用圖示
    //public Sprite ChangeSprite;
}