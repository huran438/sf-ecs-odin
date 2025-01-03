using System;
using Leopotam.EcsLite;
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
        protected T _value;

        private EcsPackedEntityWithWorld _packedEntityWithWorld;
        private EcsPool<T> _pool;

        public void Setup(ref EcsPackedEntityWithWorld packedEntity)
        {
            if (!packedEntity.Unpack(out var world, out var entity)) return;

            _pool = world.GetPool<T>();
            _packedEntityWithWorld = packedEntity;

            if (_value is IEcsAutoInit<T> autoInit)
            {
                autoInit.AutoInit(ref _value);
            }

            _pool.Add(entity) = _value;
        }


        protected virtual void OnDrawGizmos()
        {
            if (!_packedEntityWithWorld.Unpack(out _, out var entity) || !_pool.Has(entity)) return;
            ref var value = ref _pool.Get(entity);

            if (value is ISFDrawGizmos<T> drawGizmos)
            {
                drawGizmos.DrawGizmos(transform);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!_packedEntityWithWorld.Unpack(out _, out var entity) || !_pool.Has(entity)) return;
            ref var value = ref _pool.Get(entity);

            if (value is ISFDrawGizmosSelected<T> drawGizmosSelected)
            {
                drawGizmosSelected.DrawGizmosSelected(transform);
            }
        }
    }
}