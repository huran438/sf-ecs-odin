using System;
using Leopotam.EcsLite;
using SFramework.Core.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    [Serializable]
    public abstract class SFComponent<T> : MonoBehaviour, ISFComponentInspector where T : struct
    {
        [HideLabel]
        [InlineProperty]
        [SerializeField]
        private T _value;

        public ref T Value => ref _value;

        public void Setup(ref EcsPackedEntityWithWorld packedEntity)
        {
            if (_value is IEcsAutoInit<T> value)
            {
                value.AutoInit(ref _value);
            }

            if (packedEntity.Unpack(out var world, out var entity))
            {
                world.GetPool<T>().Add(entity) = _value;
            }
        }
    }
}