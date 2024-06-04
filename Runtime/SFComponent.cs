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
#endif

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

#if UNITY_EDITOR
            _packedEntity = packedEntity;
#endif
        }

#if UNITY_EDITOR

        protected virtual void OnDrawGizmos()
        {
            if (typeof(ISFDrawGizmos).IsAssignableFrom(typeof(T)))
            {
                (_value as ISFDrawGizmos).DrawGizmos(transform);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (typeof(ISFDrawGizmosSelected).IsAssignableFrom(typeof(T)))
            {
                (_value as ISFDrawGizmosSelected).DrawGizmosSelected(transform);
            }
        }
        
        protected virtual void OnValidate()
        {
            if (Application.isPlaying)
            {
                if (PackedEntity.Unpack(out var world, out var entity))
                { 
                    var pool = world.GetPool<T>();
                    
                    if (pool.Has(entity))
                    {
                        pool.Del(entity);
                        pool.Add(entity) = Value;
                    }
                    else
                    {
                        pool.Add(entity) = Value;
                    }
                    
                    if (_value is IEcsAutoInit<T> value)
                    {
                        value.AutoInit(ref _value);
                    }
                }
            }
            
            if (typeof(ISFOnValidate).IsAssignableFrom(typeof(T)))
            {
                (_value as ISFOnValidate).OnValidate();
            }
        }
#endif
    }
}