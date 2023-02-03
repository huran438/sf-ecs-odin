using System;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    [Serializable]
    public class SFWorldConfig
    {
        public int Entities => _entities;

        public int RecycledEntities => _recycledEntities;

        public int Pools => _pools;

        public int Filters => _filters;

        public int PoolDenseSize => _poolDenseSize;

        public int PoolRecycledSize => _poolRecycledSize;

        public int EntityComponentsSize => _entityComponentsSize;

        [SerializeField]
        private int _entities = 512;

        [SerializeField]
        private int _recycledEntities = 512;

        [SerializeField]
        private int _pools = 512;

        [SerializeField]
        private int _filters = 512;

        [SerializeField]
        private int _poolDenseSize = 512;

        [SerializeField]
        private int _poolRecycledSize = 512;

        [SerializeField]
        private int _entityComponentsSize = 8;
    }
}