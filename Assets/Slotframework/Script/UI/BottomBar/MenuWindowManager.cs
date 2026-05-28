using CriminalMakers.GameEventHub;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Slot.Common.UI
{
    public class MenuWindowManager : MonoBehaviour
    {
        private class LogButtonClickEvent : GameEvent { }
        private class VolumeButtonClickEvent : GameEvent { }
        private enum PendingMenuAction
        {
            None = 0,
            Home = 1,
            Log = 2,
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void JumpUrl(string url);
#endif
        [Inject]
        private GameInfoSO _gameInfoSO;
        [Inject]
        private PlatformData _platformData;

        [SerializeField]
        private Button _homeButton;
        [SerializeField]
        private Button _volumeButton;
        [SerializeField]
        private Button _logButton;
        [SerializeField]
        private GameObject _autoButton;

        [Tooltip("仅显示音量按钮，隐藏首页/记录/自动旋转等菜单项。")]
        [SerializeField]
        private bool _onlyShowVolumeButton = true;

        [SerializeField]
        private Sprite _volumeOnSprite;
        [SerializeField]
        private Sprite _volumeOffSprite;

        [SerializeField]
        private GameObject _childObjectToggle;
        [SerializeField]
        private string _recordUrl;

        [SerializeField] private bool _isHide = false;
        private bool _isRefreshingSystemUrlBeforeAction = false;
        private PendingMenuAction _pendingMenuAction = PendingMenuAction.None;

        #region Unity Methods
        private void OnEnable()
        {
            GameEventHub.Bind(this);
            _homeButton.onClick.AddListener(OnHomeButtonClick);
            _logButton.onClick.AddListener(OnLogButtonClick);
            _volumeButton.onClick.AddListener(OnVolumeButtonClick);
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
            _homeButton.onClick.RemoveListener(OnHomeButtonClick);
            _logButton.onClick.RemoveListener(OnLogButtonClick);
            _volumeButton.onClick.RemoveListener(OnVolumeButtonClick);
        }

        private void Start()
        {
            if (_gameInfoSO == null)
            {
                var scope = LifetimeScope.Find<BootLifeTimeScope>();
                if (scope != null)
                    scope.Container.Inject(this);
            }

#if ComboPlatform
            if (!_onlyShowVolumeButton)
                _homeButton.gameObject.SetActive(true);
#endif

            OnMenuClose();

            if (_autoButton == null && _childObjectToggle != null)
            {
                var autoTransform = _childObjectToggle.transform.Find("BtnAuto");
                if (autoTransform != null)
                    _autoButton = autoTransform.gameObject;
            }

            OnSetHomeUrlEvent(null);
            _recordUrl = _gameInfoSO.GetRecordUrlWithAccount(_platformData.Account);
            OnSetRecordUrlEvent(null);
            ApplyMenuButtonVisibility();
        }
        #endregion

        private void ApplyMenuButtonVisibility()
        {
            if (!_onlyShowVolumeButton)
                return;

            SetMenuButtonActive(_homeButton, false);
            SetMenuButtonActive(_logButton, false);
            SetMenuButtonActive(_volumeButton, true);

            if (_autoButton != null)
                _autoButton.SetActive(false);
        }

        private static void SetMenuButtonActive(Button button, bool active)
        {
            if (button != null)
                button.gameObject.SetActive(active);
        }

        private void OnMenuClick()
        {
            if (_isHide) return;
            
            _childObjectToggle.SetActive(!_childObjectToggle.activeSelf);
        }

        private void OnMenuClose()
        {
            if (_isHide) return;
            if (_childObjectToggle.activeSelf)
            {
                _childObjectToggle.SetActive(false);
            }
        }

        private void OnHomeButtonClick()
        {
            HandleMenuAction(PendingMenuAction.Home);
        }

        private void OnLogButtonClick()
        {
            HandleMenuAction(PendingMenuAction.Log);
        }

        private void HandleMenuAction(PendingMenuAction action)
        {
            if (_isRefreshingSystemUrlBeforeAction)
            {
                return;
            }

            // Socket已断线时，沿用旧逻辑直接触发。
            // if (!GameServerHandler.Instance.IsConnected)
            // {
            //     ExecuteMenuAction(action);
            //     return;
            // }

            _isRefreshingSystemUrlBeforeAction = true;
            _pendingMenuAction = action;
            new RefreshSystemUrlRequestEvent(OnRefreshSystemUrlCompleted).Publish(this);
        }

        private void OnRefreshSystemUrlCompleted(bool _)
        {
            var action = _pendingMenuAction;
            _pendingMenuAction = PendingMenuAction.None;
            _isRefreshingSystemUrlBeforeAction = false;
            ExecuteMenuAction(action);
        }

        private void ExecuteMenuAction(PendingMenuAction action)
        {
            switch (action)
            {
                case PendingMenuAction.Home:
                    new MenuHomeClickEvent().Publish(this);
                    new UIAllCloseEvent().Publish(this);
                    break;
                case PendingMenuAction.Log:
                    new LogButtonClickEvent().Publish(this);
                    new UIAllCloseEvent().Publish(this);
                    break;
            }
        }

        private void OnAutoButtonClick()
        {
            new AutoLongClickEvent().Publish(this);
            new UIAllCloseEvent().Publish(this);
        }

        private void OnVolumeButtonClick()
        {
            new VolumeButtonClickEvent().Publish(this);
        }

        private void OnLogClick()
        {

            if (string.IsNullOrEmpty(_recordUrl))
            {
                LogUtils.LogWarning("Record URL is not set.");
                return;
            }

            WebViewController.Instance.ShowWebView(_recordUrl,true);
        }

        private void OnVolumeClick()
        {
            if (!(_volumeButton.image.sprite == _volumeOffSprite))
            {
                //轉Mute
                //SoundManager.Mute();
                AudioManager.MuteMusic(true);
                AudioManager.MuteSFX(true);
                _volumeButton.image.sprite = _volumeOffSprite;
            }
            else
            {
                //SoundManager.UnMute();
                AudioManager.MuteMusic(false);
                AudioManager.MuteSFX(false);
                _volumeButton.image.sprite = _volumeOnSprite;
            }
        }

        private void OnReturnTo()
        {
            if (!ResolutionManager.Instance.CheckIsOn(transform))
                return;
                
#if UNITY_WEBGL && !UNITY_EDITOR
            string url = _platformData.HomeUrl;
            if (!string.IsNullOrEmpty(url))
            {
                JumpUrl(url);
            }
            else
            {
                WebGLPageReloader.RefreshPage();
            }
#elif UNITY_EDITOR   

            Application.OpenURL(Application.absoluteURL); //TODO
#else
            if (AudioManager.Instance != null)
                Destroy(AudioManager.Instance);

            GameServerHandler.Instance.DisConnect();
            UnityEngine.SceneManagement.SceneManager.LoadScene("Scene_Mobile001");
#endif
        }

        #region Event Listener
        [OnGameEvent(SubscriberPriority.High)]
        private void OnHomeClickEvent(MenuHomeClickEvent e)
        {
            OnReturnTo();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnLogClickEvent(LogButtonClickEvent e)
        {
            OnLogClick();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnVolumeClickEvent(VolumeButtonClickEvent e)
        {
            OnVolumeClick();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnSetHomeUrlEvent(SetHomeUrlEvent e)
        {
            if (_onlyShowVolumeButton)
            {
                ApplyMenuButtonVisibility();
                return;
            }

            if (_platformData == null)
                return;
#if UNITY_WEBGL
            _homeButton.gameObject.SetActive(!string.IsNullOrEmpty(_platformData.HomeUrl));  //TODO
#endif
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnSetRecordUrlEvent(SetRecordUrlEvent e)
        {
            if (e != null)
            {
                _recordUrl = e.RecordUrl;
            }
            else
            {
                _recordUrl = _gameInfoSO.RecordUrl;
            }

            if (_onlyShowVolumeButton)
            {
                ApplyMenuButtonVisibility();
                return;
            }

            bool logActive = !string.IsNullOrEmpty(_recordUrl);
            _logButton.gameObject.SetActive(logActive);
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIAllCloseEvent(UIAllCloseEvent e)
        {
            OnMenuClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIMenuClickEvent(UIMenuClickEvent e)
        {
            OnMenuClick();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUISpinClickEvent(SpinTriggerEvent e)
        {
            OnMenuClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIAutoClickEvent(UIAutoClickEvent e)
        {
            OnMenuClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIBetClickEvent(UIBetClickEvent e)
        {
            OnMenuClose();
        }

        [OnGameEvent(SubscriberPriority.High)]
        private void OnUIMenuInfoClickEvent(InfoUIActiveEvent e)
        {
            OnMenuClose();
        }

        
        [OnGameEvent(SubscriberPriority.High)]
        private void OnErrorLogEvent(ErrorLogEvent e)
        {
            HandleMenuAction(PendingMenuAction.Home);
        }
        #endregion
    }
}
