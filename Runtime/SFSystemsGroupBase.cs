using Leopotam.EcsLite;

namespace SFramework.ECS.Runtime
{
    public abstract class SFSystemsGroupBase : ISFSystemsGroup
    {
        public abstract IEcsSystem[] Systems { get; }

        public static implicit operator IEcsSystem[](SFSystemsGroupBase group)
        {
            return group.Systems;
        }
    }
}