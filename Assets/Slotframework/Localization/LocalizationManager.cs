using System;
using System.Collections.Generic;
#if !DISABLE_SRDEBUGGER
using SRDebugger;
#endif
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Cysharp.Threading.Tasks;

public class LocalizationManager : MonoBehaviour
{
    private const int LoadingTime = 5;
    private float _time;
    private bool _isInit;
    private bool _isloading;
    private bool _isChangingLanguage = false;
#if !DISABLE_SRDEBUGGER
    private Language_OptionContainer _optionContainer;
#endif

    private async UniTaskVoid Start()
    {
#if !DISABLE_SRDEBUGGER
        _optionContainer = new Language_OptionContainer();
        SRDebug.Instance.AddOptionContainer(_optionContainer);
        SRDebug.Instance.IsTriggerEnabled = true;
#endif
        await LocalizationSettings.InitializationOperation.Task.AsUniTask();
        _isInit = true;
    }

#if !DISABLE_SRDEBUGGER
    private void OnDestroy()
    {
        if (SRDebug.Instance != null && _optionContainer != null)
        {
            SRDebug.Instance.RemoveOptionContainer(_optionContainer);
            SRDebug.Instance.IsTriggerEnabled = false;
        }
    }
#endif

    private bool CheckLadingTimeout()
    {
        _time += Time.deltaTime;
        if (_time > LoadingTime)
        {
            _time = 0;
            _isloading = false;
            return true;
        }
        return false;
    }

    public void ChangeLanguage(LocalizationDefine.LanguageType type)
    {
        // 如果正在切換語系,直接返回忽略新的請求
        if (_isChangingLanguage)
        {
            return;
        }

        _isChangingLanguage = true;
        _isloading = true;

        try
        {
            Locale target = LocalizationSettings.AvailableLocales.Locales[(int)type];

            // 檢查是否需要切換
            if (LocalizationSettings.SelectedLocale == target)
            {
                _isInit = true;
                return;
            }

            // 切換語系
            LocalizationSettings.SelectedLocale = target;
        }
        catch (Exception ex)
        {
            LogUtils.LogError($"ChangeLanguage error: {ex.Message}");
        }
        finally
        {
            _isChangingLanguage = false;
        }
    }

    public string GetString(string table, string key)
    {
        while (!_isInit && _isloading)
        {
            // wait init
            if (CheckLadingTimeout())
            {
                LogUtils.LogWarning("init time out");
                return "";
            }
        }
        //Debug.Log("GetString " + table + " ; " + key);
        if (!LocalizationSettings.StringDatabase.IsTableLoaded(table))
        {
            _isloading = true;
            var tableOperation = LocalizationSettings.StringDatabase.GetTableAsync(table);
            while (!tableOperation.IsDone && _isloading)
            {
                // wait table loading;
                if (CheckLadingTimeout())
                {
                    LogUtils.LogWarning("GetTableAsync time out");
                }
            }
        }

        _isloading = true;
        var stringOperation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key);
        while (!stringOperation.IsDone && _isloading)
        {
            // wait string;
            if (CheckLadingTimeout())
            {
                LogUtils.LogWarning("GetLocalizedStringAsync time out");
            }
        }
        return stringOperation.Result;
    }

    public List<string> GetStringByList(string table, List<string> list)
    {
        List<string> newList = new();
        for (int i = 0; i < list.Count; i++)
        {
            string newItem = GetString(table, list[i]);
            newList.Add(newItem);
        }

        return newList;
    }

    public List<string> GetStringByList(string table, IEnumerable<string> list)
    {
        List<string> oldList = new(list);
        List<string> newList = new();
        for (int i = 0; i < oldList.Count; i++)
        {
            string newItem = GetString(table, oldList[i]);
            newList.Add(newItem);
        }

        return newList;
    }
}

#if !DISABLE_SRDEBUGGER
public class Language_OptionContainer : IOptionContainer
{
    public LocalizationDefine.LanguageType language = LocalizationDefine.LanguageType.en;
    private static bool _isChangingLanguage = false;

    public bool IsDynamic => true;

    public event Action<OptionDefinition> OptionAdded;
    public event Action<OptionDefinition> OptionRemoved;

    public IEnumerable<OptionDefinition> GetOptions()
    {
        int index = 0;
        List<OptionDefinition> options = new()
        {
            OptionDefinition.Create(
            "Langauage",
            () =>
            {
                return language;
            },
            (v) =>
            {
                if (language != v)
                {
                    ChangeLanguage(v);
                }
                language = v;
            },
            "Common", index++)
        };

        return options;
    }

    public void ChangeLanguage(LocalizationDefine.LanguageType type)
    {
        // 如果正在切換語系,直接返回忽略新的請求
        if (_isChangingLanguage)
        {
            return;
        }

        _isChangingLanguage = true;

        try
        {
            Locale target = LocalizationSettings.AvailableLocales.Locales[(int)type];

            // 檢查是否需要切換
            if (LocalizationSettings.SelectedLocale == target)
            {
                return;
            }

            // 切換語系
            LocalizationSettings.SelectedLocale = target;
        }
        catch (Exception ex)
        {
            LogUtils.LogError($"ChangeLanguage error: {ex.Message}");
        }
        finally
        {
            _isChangingLanguage = false;
        }
    }
}
#endif