using UnityEngine;
using VContainer;
using VContainer.Unity;
using OutsiderDriver.Core.Persistence;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Dialogue;
using OutsiderDriver.Localization;

namespace OutsiderDriver.Core
{
    /// <summary>
    /// Root VContainer dependency injection scope for persistent cross-scene domain services.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameLifetimeScope : LifetimeScope
    {
        [Header("Core Configurations")]
        [SerializeField] private TimeConfigSO _timeConfig;

        protected override void Awake()
        {
            base.Awake();
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<TimeService>(Lifetime.Singleton)
                .WithParameter(_timeConfig)
                .As<ITimeService>()
                .As<ITickable>();

            builder.Register<PlayerStateService>(Lifetime.Singleton)
                .As<IPlayerStateService>();

            builder.Register<InkDialogueService>(Lifetime.Singleton)
                .As<IDialogueService>();

            builder.Register<LocalizationService>(Lifetime.Singleton)
                .As<ILocalizationService>();

            builder.Register<SaveService>(Lifetime.Singleton)
                .As<ISaveService>();
        }
    }
}
