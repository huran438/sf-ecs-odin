using System;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    [Serializable]
    public class SFWorldConfig
    {
        public int Entities = 512;
        public int RecycledEntities  = 512;
        public int Pools = 512;
        public int Filters  = 512;
        public int PoolDenseSize = 512;
        public int PoolRecycledSize = 512;
        public int EntityComponentsSize  = 8;
    }
}