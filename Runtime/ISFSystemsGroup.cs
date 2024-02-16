using Leopotam.EcsLite;

namespace SFramework.ECS.Runtime
{
    public interface ISFSystemsGroup
    {
        IEcsSystem[] Systems { get; }
    }
}