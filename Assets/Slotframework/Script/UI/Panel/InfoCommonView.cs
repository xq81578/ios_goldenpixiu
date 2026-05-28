using System;
using Cysharp.Threading.Tasks;
using Slot.Common;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.Localization.Settings;

namespace Slot.Common.UI
{
    public class InfoCommonView : MonoBehaviour
    {
        [SerializeField]
        private List<LocalizeStringEvent> _buyTypeLocalizes;
        [SerializeField]
        private LocalizeStringEvent _freeGameBaseLocalize;
        [SerializeField]
        private LocalizeStringEvent _betRangeLocalize;
        [SerializeField]
        private Transform _buyContentTransform;

        [SerializeField]
        private Image _spinImage;
        [SerializeField]
        private Image _stopImage;
        [SerializeField]
        private GameObject _autoLayoutGameObject;
        [SerializeField]
        private GameObject _turboLayoutGameObject;
        [SerializeField]
        private GameObject _homeLayoutGameObject;
        [SerializeField]
        private GameObject _historyLayoutGameObject;
        [SerializeField]
        private GameObject _spaceLayoutGameObject;

        [SerializeField]
        private VerticalLayoutGroup[] _verticalLayoutGroups;
        [SerializeField]
        private ContentSizeFitter[] _contentSizeFitters;

        private const string Zero_String = "0";
        private const string One_String = "1";

        /// <summary>
        /// 設定 info 內動態參數
        /// </summary>
        public async UniTask SetLocalize(string[] buyTypeRTP, bool hasExtraFeature, Dictionary<BuyType, LocalizedString> customRTPLocalizedStrings)
        {
            // Set Main Game RTP
            SetStringVariable(_buyTypeLocalizes[0], Zero_String, buyTypeRTP[0]);
            //
            // if (!hasExtraFeature)
            // {
                // If no extra feature, hide all buy type RTP info except Main Game
                for (int i = 1; i < _buyTypeLocalizes.Count; i++)
                {
                    _buyTypeLocalizes[i].gameObject.SetActive(false);
                }
                return;
            // }

            for (int rtpIndex = 1; rtpIndex < buyTypeRTP.Length; rtpIndex++)
            {
                string rtpValue = buyTypeRTP[rtpIndex];
                BuyType buyType = (BuyType)rtpIndex;
                LocalizeStringEvent buyTypeLocalizes = _buyTypeLocalizes[rtpIndex];

                if (rtpValue == Zero_String || !Enum.IsDefined(typeof(BuyType), buyType))
                {
                    buyTypeLocalizes.gameObject.SetActive(false);
                    continue;
                }

                buyTypeLocalizes.gameObject.SetActive(true);
                string buyTypeLocalizedString = customRTPLocalizedStrings.ContainsKey(buyType) ?
                    await customRTPLocalizedStrings[buyType].GetLocalizedStringAsync() :
                    await GetBuyTypeLocalizedString(buyType);

                SetStringVariable(buyTypeLocalizes, Zero_String, buyTypeLocalizedString);
                SetStringVariable(buyTypeLocalizes, One_String, buyTypeRTP[rtpIndex]);
            }
        }

        public void SetBetRange(string minBet, string maxBet)
        {
            SetStringVariable(_betRangeLocalize, Zero_String, minBet);
            SetStringVariable(_betRangeLocalize, One_String, maxBet);
        }

        public void SetAutoInfoActive(bool isActive)
        {
            if (_autoLayoutGameObject == null)
                return;
            _autoLayoutGameObject.SetActive(isActive);
        }

        public void SetTurboInfoActive(bool isActive)
        {
            if (_turboLayoutGameObject == null)
                return;
            _turboLayoutGameObject.SetActive(isActive);
        }

        public void SetHomeInfoActive(bool isActive)
        {
            if (_homeLayoutGameObject == null)
                return;
            _homeLayoutGameObject.SetActive(isActive);
        }

        public void SetHistoryInfoActive(bool isActive)
        {
            if (_historyLayoutGameObject == null)
                return;
            _historyLayoutGameObject.SetActive(isActive);
        }

        public void SetSpaceBarInfoActive(bool isActive)
        {
            if (_spaceLayoutGameObject == null)
                return;
            _spaceLayoutGameObject.SetActive(isActive);
        }

        public void SetSpinSprite(Sprite spinSprite, Sprite stopSprite)
        {
            if (spinSprite != null)
                _spinImage.sprite = spinSprite;

            if (stopSprite != null)
                _stopImage.sprite = stopSprite;
        }
        
        public void LayoutEnable(bool enable)
        {
            if (_verticalLayoutGroups != null)
            {
                foreach (var vlg in _verticalLayoutGroups)
                {
                    if (vlg == null)
                        continue;
                    vlg.enabled = enable;
                }
            }

            if (_contentSizeFitters != null)
            {
                foreach (var csf in _contentSizeFitters)
                {
                    if (csf == null)
                        continue;
                    csf.enabled = enable;
                }
            }
        }

        private static void SetStringVariable(LocalizeStringEvent localizeStringEvent, string key, string value)
        {
            StringVariable stringVariable;
            if (!localizeStringEvent.StringReference.TryGetValue(key, out var outValue))
            {
                stringVariable = new StringVariable();
                localizeStringEvent.StringReference.Add(key, stringVariable);
            }
            else
            {
                stringVariable = outValue as StringVariable;
            }

            stringVariable.Value = value;
        }

       private static async UniTask<string> GetBuyTypeLocalizedString(BuyType buyType)
        {
            string localizationKey;

            switch (buyType)
            {
                case BuyType.BUY_EXTRA_BET:
                    localizationKey = "Extra_Bet";
                    break;
                case BuyType.BUY_FREE_SPINS:
                    localizationKey = "Free_Game";
                    break;
                case BuyType.BUY_SUPER_FREE_SPINS:
                    localizationKey = "Super_Free_Game";
                    break;
                default:
                    return buyType.ToString();
            }

            var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("Common_StringTable", localizationKey);
            await handle.Task;
            return handle.Result;
        }

#if UNITY_EDITOR
        [Sirenix.OdinInspector.Button]
        private void GetAllLayoutComponents()
        {
            // 取得所有 VerticalLayoutGroup（包含子物件和孫物件等所有層級，包含停用的物件）
            _verticalLayoutGroups = GetComponentsInChildren<VerticalLayoutGroup>(includeInactive: true);
            if (_verticalLayoutGroups != null && _verticalLayoutGroups.Length > 0)
            {
                LogUtils.Log($"[InfoCommonView] Found {_verticalLayoutGroups.Length} VerticalLayoutGroup(s):");
                foreach (var vlg in _verticalLayoutGroups)
                {
                    LogUtils.Log($"  - {vlg.gameObject.name}");
                }
            }
            else
            {
                _verticalLayoutGroups = new VerticalLayoutGroup[0];
                LogUtils.LogWarning("[InfoCommonView] No VerticalLayoutGroup found");
            }

            // 取得所有 ContentSizeFitter（包含子物件和孫物件等所有層級，包含停用的物件）
            _contentSizeFitters = GetComponentsInChildren<ContentSizeFitter>(includeInactive: true);
            if (_contentSizeFitters != null && _contentSizeFitters.Length > 0)
            {
                LogUtils.Log($"[InfoCommonView] Found {_contentSizeFitters.Length} ContentSizeFitter(s):");
                foreach (var csf in _contentSizeFitters)
                {
                    LogUtils.Log($"  - {csf.gameObject.name}");
                }
            }
            else
            {
                _contentSizeFitters = new ContentSizeFitter[0];
                LogUtils.LogWarning("[InfoCommonView] No ContentSizeFitter found");
            }
        }
#endif
    }
}
