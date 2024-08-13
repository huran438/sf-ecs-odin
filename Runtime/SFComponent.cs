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
        private T _value;

        public ref T Value => ref _value;

#if UNITY_EDITOR
        private EcsPackedEntityWithWorld _packedEntity;
        public ref EcsPackedEntityWithWorld PackedEntity => ref _packedEntity;
        private EcsPool<T> _pool;
#endif

        public void Setup(ref EcsPackedEntityWithWorld packedEntity)
        {
            if (_value is IEcsAutoInit<T> autoInit)
            {
                autoInit.AutoInit(ref _value);
            }
            else if (_value is IEcsAutoReset<T> autoReset)
            {
                autoReset.AutoReset(ref _value);
            }

            if (packedEntity.Unpack(out var world, out var entity))
            {
                world.GetPool<T>().Add(entity) = _value;
            }

#if UNITY_EDITOR
            _packedEntity = packedEntity;
            _pool = world.GetPool<T>();
#endif
        }

        
#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            if (_value is ISFDrawGizmos drawGizmos)
            {
                drawGizmos.DrawGizmos(transform);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (_value is ISFDrawGizmosSelected drawGizmosSelected)
            {
                drawGizmosSelected.DrawGizmosSelected(transform);
            }
        }
        
        protected virtual void OnValidate()
        {
            // Update component in ECS
            if (PackedEntity.Unpack(out var world, out var entity))
            {
                if (_pool.Has(entity))
                {
                    _pool.Del(entity);
                    _pool.Add(entity) = Value;
                }
                else
                {
                    _pool.Add(entity) = Value;
                }

                if (_value is IEcsAutoInit<T> autoInit)
                {
                    autoInit.AutoInit(ref _value);
                }
                else if (_value is IEcsAutoReset<T> autoReset)
                {
                    autoReset.AutoReset(ref _value);
                }
            }

            if (_value is ISFOnValidate onValidate)
            {
                onValidate.OnValidate();
            }
        }
#endif
    }
}