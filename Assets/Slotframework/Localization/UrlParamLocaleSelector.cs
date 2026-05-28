using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// 啟動時固定使用英文語系
/// </summary>
[Serializable]
public class UrlParamLocaleSelector : IStartupLocaleSelector
{
    public Locale GetStartupLocale(ILocalesProvider availableLocales)
    {
        return LocalizationSettings.AvailableLocales.Locales[(int)LocalizationDefine.LanguageType.en];
    }
}
