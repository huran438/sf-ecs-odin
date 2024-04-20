using Leopotam.EcsLite;

namespace SFramework.ECS.Runtime
{
    public interface ISFSystemsGroup
    {
        ISFSystem[] Systems { get; }
    }
}