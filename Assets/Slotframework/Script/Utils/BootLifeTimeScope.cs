using UnityEngine;
using VContainer;
using VContainer.Unity;
using Slot.Common;

public class BootLifeTimeScope : LifetimeScope
{
    [SerializeField]
    private BaseGameService _gameService;
    [SerializeField]
    private GameInfoSO _gameInfoSO;
    [SerializeField]
    private PayTableSO _payTableSO;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_gameService);
        builder.RegisterInstance(_gameInfoSO);
        builder.RegisterInstance(_payTableSO);
        // builder.RegisterComponentInHierarchy<LocalizationManager>();
        
        builder.Register<PlatformData>(Lifetime.Singleton);
        builder.Register<PlayerBetData>(Lifetime.Singleton);
        builder.Register<GameStateData>(Lifetime.Singleton);
        builder.Register<AutoSpinSetting>(Lifetime.Singleton);
    }
}
