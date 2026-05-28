using Slot.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Slot.Common
{
    public class Common_LifetimeScope : LifetimeScope
    {
        [SerializeField]
        private CurrencySettingSO _currencySetting;

        protected override void Configure(IContainerBuilder builder)
        {
            // 若有沒有設置父節點 則取場景上的
            if (Parent == null)
            {
                builder.RegisterComponentInHierarchy<BaseGameService>();
                builder.RegisterComponentInHierarchy<LocalizationManager>();
                builder.Register<PlayerBetData>(Lifetime.Singleton);
                builder.Register<PlatformData>(Lifetime.Singleton);
                builder.Register<AutoSpinSetting>(Lifetime.Singleton);
            }

            builder.RegisterInstance(_currencySetting);
        }
    }
}
