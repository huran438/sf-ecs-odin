using System;
using Leopotam.EcsLite;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public abstract class SFSystemsGroupBase : ISFSystemsGroup, ISFSystem
    {
        public abstract ISFSystem[] Systems { get; }

        public static implicit operator ISFSystem[](SFSystemsGroupBase group)
        {
            return group.Systems;
        }
    }
}