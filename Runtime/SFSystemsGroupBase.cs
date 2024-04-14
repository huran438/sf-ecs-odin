using System;
using Leopotam.EcsLite;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public abstract class SFSystemsGroupBase : ISFSystemsGroup, ISystem
    {
        public abstract ISystem[] Systems { get; }

        public static implicit operator ISystem[](SFSystemsGroupBase group)
        {
            return group.Systems;
        }
    }
}