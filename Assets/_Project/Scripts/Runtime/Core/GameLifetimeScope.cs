using UnityEngine;
using VContainer;
using VContainer.Unity;
using OutsiderDriver.Core.Time;

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
        }
    }
}
