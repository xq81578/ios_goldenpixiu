using CriminalMakers.GameEventHub;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Slot.Common.UI.Mediator
{
    public class LoadingUIMediator : UIOrientationMediator<LoadingUI>
    {
#if !RELEASE_BUILD
        [SerializeField]
        private bool _useFakeLoading = false;
        [SerializeField]
        private bool _autoChangeContent = true;
#endif
        [SerializeField]
        private int _changeDuration = 5;
        [SerializeField]
        private float _tempTime = 0;
        [SerializeField]
        private int _scrollIndex = 0;
        [SerializeField]
        private float _scrollSwitchDuration = 0.3f;

        [SerializeField]
        private Sprite _comboLogoSprite;
        [SerializeField]
        private Sprite _llgLogoSprite;

        private void OnEnable()
        {
            GameEventHub.Bind(this);
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
        }

        protected override void Awake()
        {
            base.Awake();
            SetVersionText();
        }

        protected override void Start()
        {
            FakeLoading();
        }

        private void Update()
        {
#if !RELEASE_BUILD
            if (!_autoChangeContent) return;
#endif
            _tempTime += Time.deltaTime;
            //每 5 秒換下一張圖
            if (_tempTime > _changeDuration)
            {
                SetScrollContentSwitch();
            }
        }

        private async void FakeLoading()
        {
#if !RELEASE_BUILD
            if (_useFakeLoading)
            {
                int fakeProgress = 0;

                while (fakeProgress < 100)
                {
                    fakeProgress += 1;
                    float progress = fakeProgress / 100f;
                    InvokeAllUIs(ui => ui.SetLoadingBarProgress(progress));
                    await UniTask.WaitForEndOfFrame();
                }

                InvokeAllUIs(ui => ui.ShowContinueButton());
            }
#endif
        }

        private void SetVersionText()
        {
            string version = ServiceUtils.GetVersionText();
            InvokeAllUIs(ui => ui.SetVersionText(version));
        }

        private void SetScrollContentSwitch(bool next = true)
        {
            _tempTime = 0;
            _scrollIndex = next ? _scrollIndex + 1 : _scrollIndex - 1;

            InvokeAllUIs(ui => 
            {
                ui.ScrollContentSwitch(next, _scrollIndex, _scrollSwitchDuration);
                ui.SetContentTextActive(_scrollIndex);
            });
        }

        private Sprite GetLogoSprite(PlatformType platformType)
        {
            return platformType switch
            {
                _ => _comboLogoSprite,
            };
        }

        #region Event Listener
        [OnGameEvent]
        private void OnScrollContentSwitchEvent(LoadingUIContentSwitchEvent e)
        {
            SetScrollContentSwitch(e.Next);
        }

        [OnGameEvent]
        private void OnLoadingProgressEvent(LoadingProgressEvent e)
        {
            InvokeAllUIs(ui => ui.SetLoadingBarProgress(e.Progress));
        }

        [OnGameEvent]
        private void OnProgressInfoTextEvent(GameFlowProgressInfoEvent e)
        {
            InvokeAllUIs(ui => ui.SetLoadingBarProgressInfoText(e.ProgressInfo));
        }

        [OnGameEvent]
        private void OnGameReadyEvent(GameReadyEvent e)
        {
            InvokeAllUIs(ui => ui.ShowContinueButton());
        }

        [OnGameEvent]
        private void OnContinueEvent(UILoadingContinueClickEvent e)
        {
            AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);

            foreach (LoadingUI ui in AllUIs)
            {
                Destroy(ui.gameObject);
            }

            _landscapeUI = null;
            _portraitUI = null;
            enabled = false;
        }

        [OnGameEvent]
        private void OnSetBrandLogoEvent(SetPlatformIdEvent e)
        {
            var logoSprite = GetLogoSprite(e.PlatformType);
            InvokeAllUIs(ui => ui.SetBrandLogo(logoSprite));
        }
        #endregion
    }
}
