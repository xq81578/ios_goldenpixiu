using Sirenix.OdinInspector;

namespace Slot001_GoldenPixiu
{
    public class FreeSpinsUIMediator : UIOrientationMediator<FreeSpinsUI>
    {
        public bool IsOpening => _isOpening;
        private bool _isOpening = false;

        protected override void Initialize()
        {
            InvokeAllUIs(ui => ui.Init(this));
        }

        [Button]
        public void OpenObtainFreeSpinsPanel(int freeSpinCount, bool isRetrigger = false)
        {
            _isOpening = true;
            InvokeAllUIs(ui => ui.OpenObtainFreeSpinsPanel(freeSpinCount, isRetrigger));
        }

        public void CloseObtainFreeSpinsPanel()
        {
            if (!_isOpening)
            {
                return;
            }
            _isOpening = false;
            InvokeAllUIs(ui => ui.CloseObtainFreeSpinsPanel());
        }
    }
}
