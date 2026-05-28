
using System;
using UnityEngine;

public static class UtilityConstants
{
    public const string BGMFadeOut = "bgm_fade_out";
    public const string BGMFadeIn = "bgm_fade_in";

    public const string PlatformGameToken = "cp_game_token";
    public const string PlatformUserId = "cp_user_id";
    public const string PlatformCurrency = "cp_currency";
    public const string PlatformGameUrl = "cp_game_url";

    public static ECurrency TryParseCurrency(string currencyStr)
    {
        if (Enum.TryParse(currencyStr, out ECurrency currency))
            return currency;
        else
        {
            Debug.Log("Invalid enum value");
            return ECurrency.PHP;
        }
    }
}

public enum ECurrency
{
    PHP=0, // 真金
    COM,  // 娛樂幣

    USD,
    TWD,
    BDT,
    BND,
    BRL,
    CNY,
    EUR,
    HKD,
    INR,
    JPY,
    KRW,
    LKR,
    MMK,
    MXN,
    MYR,
    PKR,
    IDR,
    RUB,
    SGD,
    THB,
    VND,

    AED,
    NPR,
    GBP,
    AMD,
    AUD,
    ARS,
    CAD,
    CHF,
    CLP,
    COP,
    GEL,
    GHS,
    KES,
    KZT,
    LAK,
    NGN,
    NOK,
    NZD,
    OMR,
    PGK,
    PEN,
    PLN,
    SAR,
    SEK,
    TND,
    TRY,
    TZS,
    VES,
    ZAR,

    //kKRW,
    kMMK,
    kIDR,
    kVND,
    kKHR,
    kLAK
}
