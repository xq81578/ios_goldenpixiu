using Slot.Common;
using Slot.Common.UI;
using Slot.Common.UI.Mediator;
using UnityEngine;
using VContainer;
using VContainer.Unity;


namespace Slot001_GoldenPixiu
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private SymbolDataSO _symbolDataSO;
        [SerializeField]
        private PayTableSO _payTable;
        [SerializeField]
        private PerformanceSettingSO _performanceSetting;
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<SlotStateMachine>();
            builder.RegisterComponentInHierarchy<ReelControllerMediator>();
            builder.RegisterComponentInHierarchy<GamePlayUIMediator>();
            builder.RegisterComponentInHierarchy<FreeSpinsUIMediator>();
            builder.RegisterComponentInHierarchy<WinCelebrationUIMediator>();
            builder.RegisterComponentInHierarchy<SettleUIMediator>();
            builder.RegisterComponentInHierarchy<RewardPresentationPanelMediator>();
            builder.RegisterInstance(_symbolDataSO);
            builder.RegisterInstance(_payTable);
            builder.RegisterInstance(_performanceSetting);
            builder.Register<GameData>(Lifetime.Scoped);
            builder.Register<GameLogic>(Lifetime.Scoped);
            builder.Register<FlowPresenter>(Lifetime.Scoped);
            
        }
    }
}