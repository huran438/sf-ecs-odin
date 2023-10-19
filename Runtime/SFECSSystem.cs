using Leopotam.EcsLite;
using SFramework.Core.Runtime;

namespace SFramework.ECS.Runtime
{
    public abstract class SFECSSystem : IEcsPreInitSystem, IEcsRunSystem, ISFInjectable
    {
        public void PreInit(IEcsSystems systems)
        {
            this.Inject();
        }

        public void Run(IEcsSystems systems)
        {
            Tick(ref systems);
        }


        protected virtual void Tick(ref IEcsSystems systems)
        {
        }
    }
}