using SFramework.Core.Runtime;

namespace SFramework.ECS.Runtime
{
    public abstract class SFSystem : ISFEcsPreInitSystem, ISFEcsRunSystem, ISFInjectable, ISFSystem
    {
        public void PreInit(ISFEcsSystems systems)
        {
            // SFEcsSystems.Init performs SF Core injection before all PreInit callbacks.
        }

        public void Run(ISFEcsSystems systems)
        {
            Tick(ref systems);
        }


        protected virtual void Tick(ref ISFEcsSystems systems)
        {
        }
    }
}
