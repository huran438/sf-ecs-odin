// ----------------------------------------------------------------------------
// The MIT-Red License
// Copyright (c) 2012-2026 Leopotam <leopotam@yandex.ru>
//
// Данное программное обеспечение и сопутствующая документация (далее - Продукт)
// выпускается под MIT-Red лицензией.
//
// MIT-Red регулируется совокупностью следующих правил, если хотя бы
// одно из них невыполнимо - использование Продукта запрещено:
//
// Если вы за применение opensource программного обеспечения в
// военной сфере - вы не можете использовать этот Продукт.
//
// Если вы испытываете ненависть к русским или поддерживаете
// любые нападки на них - вы не можете использовать этот Продукт.
//
// Данная лицензия разрешает лицам, получившим копию данного Продукта,
// безвозмездно использовать Программное обеспечение без ограничений, включая
// неограниченное право на использование, копирование, изменение, слияние,
// публикацию и распространение копий Продукта.
//
// Указанное выше уведомление об авторском праве и данные условия должны быть
// включены во все копии или значимые части данного Продукта.
//
// ДАННЫЙ ПРОДУКТ ПРЕДОСТАВЛЯЕТСЯ «КАК ЕСТЬ», БЕЗ КАКИХ-ЛИБО ГАРАНТИЙ, ЯВНО
// ВЫРАЖЕННЫХ ИЛИ ПОДРАЗУМЕВАЕМЫХ, ВКЛЮЧАЯ ГАРАНТИИ ТОВАРНОЙ ПРИГОДНОСТИ,
// СООТВЕТСТВИЯ ПО ЕГО КОНКРЕТНОМУ НАЗНАЧЕНИЮ И ОТСУТСТВИЯ НАРУШЕНИЙ, НО
// НЕ ОГРАНИЧИВАЯСЬ ИМИ. НИ В КАКОМ СЛУЧАЕ АВТОРЫ ИЛИ ПРАВООБЛАДАТЕЛИ НЕ НЕСУТ
// ОТВЕТСТВЕННОСТИ ПО КАКИМ-ЛИБО ИСКАМ, ЗА УЩЕРБ ИЛИ ПО ИНЫМ ТРЕБОВАНИЯМ,
// В ТОМ ЧИСЛЕ, ПРИ ДЕЙСТВИИ КОНТРАКТА, ДЕЛИКТЕ ИЛИ ИНОЙ СИТУАЦИИ, ВОЗНИКШИМ
// ИЗ-ЗА ИСПОЛЬЗОВАНИЯ ПРОГРАММНОГО ОБЕСПЕЧЕНИЯ ИЛИ ИНЫХ ДЕЙСТВИЙ С ПРОДУКТОМ.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#if ENABLE_IL2CPP
using Unity.IL2CPP.CompilerServices;
#endif

namespace SFramework.ECS.Runtime
{
    public static class SFEcsRawEntityOffsets
    {
        public const int ComponentsCount = 0;
        public const int Gen = 1;
        public const int Components = 2;
    }

#if ENABLE_IL2CPP
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
#endif
    public class SFEcsWorld
    {
        // componentsCount, gen, c1, c2, ..., [next]
        private short[] _entities;
        private int _entitiesItemSize;
        private int _entitiesCount;
        private int[] _recycledEntities;
        private int _recycledEntitiesCount;
        private ISFEcsPool[] _pools;
        private short _poolsCount;
        private readonly int _poolDenseSize;
        private readonly int _poolRecycledSize;
        private readonly Dictionary<Type, ISFEcsPool> _poolHashes;
        private readonly Dictionary<int, SFEcsFilter> _hashedFilters;
        private readonly List<SFEcsFilter> _allFilters;
        private List<SFEcsFilter>[] _filtersByIncludedComponents;
        private List<SFEcsFilter>[] _filtersByExcludedComponents;
        private Mask[] _masks;
        private int _masksCount;

        private bool _destroyed;
#if DEBUG || SFECS_WORLD_EVENTS
        private List<ISFEcsWorldEventListener> _eventListeners;

        public void AddEventListener(ISFEcsWorldEventListener listener)
        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            if (listener == null)
            {
                throw new Exception("Listener is null.");
            }
#endif
            _eventListeners.Add(listener);
        }

        public void RemoveEventListener(ISFEcsWorldEventListener listener)
        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            if (listener == null)
            {
                throw new Exception("Listener is null.");
            }
#endif
            _eventListeners.Remove(listener);
        }

        public void RaiseEntityChangeEvent(int entity, short poolId, bool added)
        {
            for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
            {
                _eventListeners[ii].OnEntityChanged(entity, poolId, added);
            }
        }
#endif
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
        private readonly List<int> _leakedEntities = new List<int>(512);

        internal bool CheckForLeakedEntities()
        {
            if (_leakedEntities.Count > 0)
            {
                for (int i = 0, iMax = _leakedEntities.Count; i < iMax; i++)
                {
                    var entityData = GetRawEntityOffset(_leakedEntities[i]);
                    if (_entities[entityData + SFEcsRawEntityOffsets.Gen] > 0 && _entities[entityData + SFEcsRawEntityOffsets.ComponentsCount] == 0)
                    {
                        return true;
                    }
                }
                _leakedEntities.Clear();
            }
            return false;
        }
#endif
#if UNITY_EDITOR
        public string DebugName { get; internal set; }
#endif

        public SFEcsWorld(in Config cfg = default)
        {
            // entities.
            var capacity = cfg.Entities > 0 ? cfg.Entities : Config.EntitiesDefault;
            _entitiesItemSize = SFEcsRawEntityOffsets.Components + (cfg.EntityComponentsSize > 0 ? cfg.EntityComponentsSize : Config.EntityComponentsSizeDefault);
            _entities = new short[capacity * _entitiesItemSize];
            capacity = cfg.RecycledEntities > 0 ? cfg.RecycledEntities : Config.RecycledEntitiesDefault;
            _recycledEntities = new int[capacity];
            _entitiesCount = 0;
            _recycledEntitiesCount = 0;
            // pools.
            capacity = cfg.Pools > 0 ? cfg.Pools : Config.PoolsDefault;
            _pools = new ISFEcsPool[capacity];
            _poolHashes = new Dictionary<Type, ISFEcsPool>(capacity);
            _filtersByIncludedComponents = new List<SFEcsFilter>[capacity];
            _filtersByExcludedComponents = new List<SFEcsFilter>[capacity];
            _poolDenseSize = cfg.PoolDenseSize > 0 ? cfg.PoolDenseSize : Config.PoolDenseSizeDefault;
            _poolRecycledSize = cfg.PoolRecycledSize > 0 ? cfg.PoolRecycledSize : Config.PoolRecycledSizeDefault;
            _poolsCount = 0;
            // filters.
            capacity = cfg.Filters > 0 ? cfg.Filters : Config.FiltersDefault;
            _hashedFilters = new Dictionary<int, SFEcsFilter>(capacity);
            _allFilters = new List<SFEcsFilter>(capacity);
            // masks.
            _masks = new Mask[64];
            _masksCount = 0;
#if DEBUG || SFECS_WORLD_EVENTS
            _eventListeners = new List<ISFEcsWorldEventListener>(4);
#endif
            _destroyed = false;
#if UNITY_EDITOR
            SFEcsEditorWorlds.Track(this);
#endif
        }

        public void Destroy()
        {
            Destroy(validateEntities: true);
        }

        internal void DestroyOwnedWorld()
        {
            Destroy(validateEntities: false);
        }

        private void Destroy(bool validateEntities)
        {
            if (_destroyed) return;
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            if (validateEntities && CheckForLeakedEntities())
            {
                throw new Exception($"Empty entity detected before SFEcsWorld.Destroy().");
            }
#endif
            _destroyed = true;
#if UNITY_EDITOR
            SFEcsEditorWorlds.PruneDestroyedWorlds();
#endif
            for (var i = _entitiesCount - 1; i >= 0; i--)
            {
                if (IsEntityAliveInternal(i))
                {
                    DelEntity(i);
                }
            }
            _pools = Array.Empty<ISFEcsPool>();
            _poolHashes.Clear();
            _hashedFilters.Clear();
            _allFilters.Clear();
            _filtersByIncludedComponents = Array.Empty<List<SFEcsFilter>>();
            _filtersByExcludedComponents = Array.Empty<List<SFEcsFilter>>();
#if DEBUG || SFECS_WORLD_EVENTS
            for (var ii = _eventListeners.Count - 1; ii >= 0; ii--)
            {
                _eventListeners[ii].OnWorldDestroyed(this);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRawEntityOffset(int entity)
        {
            return entity * _entitiesItemSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive()
        {
            return !_destroyed;
        }

        public int NewEntity()
        {
            int entity;
            if (_recycledEntitiesCount > 0)
            {
                entity = _recycledEntities[--_recycledEntitiesCount];
                _entities[GetRawEntityOffset(entity) + SFEcsRawEntityOffsets.Gen] *= -1;
            }
            else
            {
                // new entity.
                if (_entitiesCount * _entitiesItemSize == _entities.Length)
                {
                    // resize entities and component pools.
                    var newSize = _entitiesCount << 1;
                    Array.Resize(ref _entities, newSize * _entitiesItemSize);
                    for (int i = 0, iMax = _poolsCount; i < iMax; i++)
                    {
                        _pools[i].Resize(newSize);
                    }
                    for (int i = 0, iMax = _allFilters.Count; i < iMax; i++)
                    {
                        _allFilters[i].ResizeSparseIndex(newSize);
                    }
#if DEBUG || SFECS_WORLD_EVENTS
                    for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
                    {
                        _eventListeners[ii].OnWorldResized(newSize);
                    }
#endif
                }
                entity = _entitiesCount++;
                _entities[GetRawEntityOffset(entity) + SFEcsRawEntityOffsets.Gen] = 1;
            }
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            _leakedEntities.Add(entity);
#endif
#if DEBUG || SFECS_WORLD_EVENTS
            for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
            {
                _eventListeners[ii].OnEntityCreated(entity);
            }
#endif
            return entity;
        }

        public void DelEntity(int entity)
        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            if (entity < 0 || entity >= _entitiesCount)
            {
                throw new Exception("Cant touch destroyed entity.");
            }
#endif
            var entityOffset = GetRawEntityOffset(entity);
            var componentsCount = _entities[entityOffset + SFEcsRawEntityOffsets.ComponentsCount];
            ref var entityGen = ref _entities[entityOffset + SFEcsRawEntityOffsets.Gen];
            if (entityGen < 0)
            {
                return;
            }
            if (componentsCount > 0)
            {
                for (var i = entityOffset + SFEcsRawEntityOffsets.Components + componentsCount - 1; i >= entityOffset + SFEcsRawEntityOffsets.Components; i--)
                {
                    _pools[_entities[i]].Del(entity);
                }
            }
            else
            {
                entityGen = (short)(entityGen == short.MaxValue ? -1 : -(entityGen + 1));
                if (_recycledEntitiesCount == _recycledEntities.Length)
                {
                    Array.Resize(ref _recycledEntities, _recycledEntitiesCount << 1);
                }
                _recycledEntities[_recycledEntitiesCount++] = entity;
#if DEBUG || SFECS_WORLD_EVENTS
                for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
                {
                    _eventListeners[ii].OnEntityDestroyed(entity);
                }
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetComponentsCount(int entity)
        {
            return _entities[GetRawEntityOffset(entity) + SFEcsRawEntityOffsets.ComponentsCount];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short GetEntityGen(int entity)
        {
            return _entities[GetRawEntityOffset(entity) + SFEcsRawEntityOffsets.Gen];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRawEntityItemSize()
        {
            return _entitiesItemSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetUsedEntitiesCount()
        {
            return _entitiesCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetWorldSize()
        {
            return _entities.Length / _entitiesItemSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPoolsCount()
        {
            return _poolsCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntitiesCount()
        {
            return _entitiesCount - _recycledEntitiesCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short[] GetRawEntities()
        {
            return _entities;
        }

        public SFEcsPool<T> GetPool<T>() where T : struct
        {
            var poolType = typeof(T);
            if (_poolHashes.TryGetValue(poolType, out var rawPool))
            {
                return (SFEcsPool<T>)rawPool;
            }
#if DEBUG
            if (_poolsCount == short.MaxValue)
            {
                throw new Exception("No more room for new component into this world.");
            }
#endif
            var pool = new SFEcsPool<T>(this, _poolsCount, _poolDenseSize, GetWorldSize(), _poolRecycledSize);
            _poolHashes[poolType] = pool;
            if (_poolsCount == _pools.Length)
            {
                var newSize = _poolsCount << 1;
                Array.Resize(ref _pools, newSize);
                Array.Resize(ref _filtersByIncludedComponents, newSize);
                Array.Resize(ref _filtersByExcludedComponents, newSize);
            }
            _pools[_poolsCount++] = pool;
            return pool;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISFEcsPool GetPoolById(int typeId)
        {
            return typeId >= 0 && typeId < _poolsCount ? _pools[typeId] : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISFEcsPool GetPoolByType(Type type)
        {
            return _poolHashes.TryGetValue(type, out var pool) ? pool : null;
        }

        public int GetAllEntities(ref int[] entities)
        {
            var count = _entitiesCount - _recycledEntitiesCount;
            if (entities == null || entities.Length < count)
            {
                entities = new int[count];
            }
            var id = 0;
            var offset = 0;
            for (int i = 0, iMax = _entitiesCount; i < iMax; i++, offset += _entitiesItemSize)
            {
                if (_entities[offset + SFEcsRawEntityOffsets.Gen] > 0 && _entities[offset + SFEcsRawEntityOffsets.ComponentsCount] >= 0)
                {
                    entities[id++] = i;
                }
            }
            return count;
        }

        public int GetAllPools(ref ISFEcsPool[] pools)
        {
            var count = _poolsCount;
            if (pools == null || pools.Length < count)
            {
                pools = new ISFEcsPool[count];
            }
            Array.Copy(_pools, 0, pools, 0, _poolsCount);
            return _poolsCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Mask Filter<T>() where T : struct
        {
            var mask = _masksCount > 0 ? _masks[--_masksCount] : new Mask(this);
            return mask.Inc<T>();
        }

        public int GetComponents(int entity, ref object[] list)
        {
            var entityOffset = GetRawEntityOffset(entity);
            var itemsCount = _entities[entityOffset + SFEcsRawEntityOffsets.ComponentsCount];
            if (itemsCount == 0)
            {
                return 0;
            }
            if (list == null || list.Length < itemsCount)
            {
                list = new object[_pools.Length];
            }
            var dataOffset = entityOffset + SFEcsRawEntityOffsets.Components;
            for (var i = 0; i < itemsCount; i++)
            {
                list[i] = _pools[_entities[dataOffset + i]].GetRaw(entity);
            }
            return itemsCount;
        }

        public int GetComponentTypes(int entity, ref Type[] list)
        {
            var entityOffset = GetRawEntityOffset(entity);
            var itemsCount = _entities[entityOffset + SFEcsRawEntityOffsets.ComponentsCount];
            if (itemsCount == 0)
            {
                return 0;
            }
            if (list == null || list.Length < itemsCount)
            {
                list = new Type[_pools.Length];
            }
            var dataOffset = entityOffset + SFEcsRawEntityOffsets.Components;
            for (var i = 0; i < itemsCount; i++)
            {
                list[i] = _pools[_entities[dataOffset + i]].GetComponentType();
            }
            return itemsCount;
        }

        public void CopyEntity(int srcEntity, int dstEntity)
        {
            var entityOffset = GetRawEntityOffset(srcEntity);
            var itemsCount = _entities[entityOffset + SFEcsRawEntityOffsets.ComponentsCount];
            if (itemsCount > 0)
            {
                var dataOffset = entityOffset + SFEcsRawEntityOffsets.Components;
                for (var i = 0; i < itemsCount; i++)
                {
                    _pools[_entities[dataOffset + i]].Copy(srcEntity, dstEntity);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsEntityAliveInternal(int entity)
        {
            return entity >= 0 && entity < _entitiesCount && _entities[GetRawEntityOffset(entity) + SFEcsRawEntityOffsets.Gen] > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddComponentToRawEntityInternal(int entity, short poolId)
        {
            var offset = GetRawEntityOffset(entity);
            var dataCount = _entities[offset + SFEcsRawEntityOffsets.ComponentsCount];
            if (dataCount + SFEcsRawEntityOffsets.Components == _entitiesItemSize)
            {
                // resize entities.
                ExtendEntitiesCache();
                offset = GetRawEntityOffset(entity);
            }
            _entities[offset + SFEcsRawEntityOffsets.ComponentsCount]++;
            _entities[offset + SFEcsRawEntityOffsets.Components + dataCount] = poolId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal short RemoveComponentFromRawEntityInternal(int entity, short poolId)
        {
            var offset = GetRawEntityOffset(entity);
            var dataCount = _entities[offset + SFEcsRawEntityOffsets.ComponentsCount];
            dataCount--;
            _entities[offset + SFEcsRawEntityOffsets.ComponentsCount] = dataCount;
            var dataOffset = offset + SFEcsRawEntityOffsets.Components;
            for (var i = 0; i <= dataCount; i++)
            {
                if (_entities[dataOffset + i] == poolId)
                {
                    if (i < dataCount)
                    {
                        // fill gap with last item.
                        _entities[dataOffset + i] = _entities[dataOffset + dataCount];
                    }
                    return dataCount;
                }
            }
#if DEBUG
            throw new Exception("Component not found in entity data");
#else
            return 0;
#endif
        }

        private void ExtendEntitiesCache()
        {
            var newItemSize = SFEcsRawEntityOffsets.Components + ((_entitiesItemSize - SFEcsRawEntityOffsets.Components) << 1);
            var newEntities = new short[GetWorldSize() * newItemSize];
            var oldOffset = 0;
            var newOffset = 0;
            for (int i = 0, iMax = _entitiesCount; i < iMax; i++)
            {
                // amount of entity data (components + header).
                var entityDataLen = _entities[oldOffset + SFEcsRawEntityOffsets.ComponentsCount] + SFEcsRawEntityOffsets.Components;
                for (var j = 0; j < entityDataLen; j++)
                {
                    newEntities[newOffset + j] = _entities[oldOffset + j];
                }
                oldOffset += _entitiesItemSize;
                newOffset += newItemSize;
            }
            _entitiesItemSize = newItemSize;
            _entities = newEntities;
        }

        private (SFEcsFilter, bool) GetFilterInternal(Mask mask, int capacity = 512)
        {
            var hash = mask.Hash;
            var exists = _hashedFilters.TryGetValue(hash, out var filter);
            if (exists)
            {
                return (filter, false);
            }
            filter = new SFEcsFilter(this, mask, capacity, GetWorldSize());
            _hashedFilters[hash] = filter;
            _allFilters.Add(filter);
            // add to component dictionaries for fast compatibility scan.
            for (int i = 0, iMax = mask.IncludeCount; i < iMax; i++)
            {
                var list = _filtersByIncludedComponents[mask.Include[i]];
                if (list == null)
                {
                    list = new List<SFEcsFilter>(8);
                    _filtersByIncludedComponents[mask.Include[i]] = list;
                }
                list.Add(filter);
            }
            for (int i = 0, iMax = mask.ExcludeCount; i < iMax; i++)
            {
                var list = _filtersByExcludedComponents[mask.Exclude[i]];
                if (list == null)
                {
                    list = new List<SFEcsFilter>(8);
                    _filtersByExcludedComponents[mask.Exclude[i]] = list;
                }
                list.Add(filter);
            }
            // scan exist entities for compatibility with new filter.
            for (int i = 0, iMax = _entitiesCount; i < iMax; i++)
            {
                if (_entities[GetRawEntityOffset(i) + SFEcsRawEntityOffsets.ComponentsCount] > 0 && IsMaskCompatible(mask, i))
                {
                    filter.AddEntity(i);
                }
            }
#if DEBUG || SFECS_WORLD_EVENTS
            for (int ii = 0, iMax = _eventListeners.Count; ii < iMax; ii++)
            {
                _eventListeners[ii].OnFilterCreated(filter);
            }
#endif
            return (filter, true);
        }

        public void OnEntityChangeInternal(int entity, short componentType, bool added)
        {
            var includeList = _filtersByIncludedComponents[componentType];
            var excludeList = _filtersByExcludedComponents[componentType];
            if (added)
            {
                // add component.
                if (includeList != null)
                {
                    foreach (var filter in includeList)
                    {
                        if (IsMaskCompatible(filter.GetMask(), entity))
                        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                            if (filter.SparseEntities[entity] > 0)
                            {
                                throw new Exception("Entity already in filter.");
                            }
#endif
                            filter.AddEntity(entity);
                        }
                    }
                }
                if (excludeList != null)
                {
                    foreach (var filter in excludeList)
                    {
                        if (IsMaskCompatibleWithout(filter.GetMask(), entity, componentType))
                        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                            if (filter.SparseEntities[entity] == 0)
                            {
                                throw new Exception("Entity not in filter.");
                            }
#endif
                            filter.RemoveEntity(entity);
                        }
                    }
                }
            }
            else
            {
                // remove component.
                if (includeList != null)
                {
                    foreach (var filter in includeList)
                    {
                        if (IsMaskCompatible(filter.GetMask(), entity))
                        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                            if (filter.SparseEntities[entity] == 0)
                            {
                                throw new Exception("Entity not in filter.");
                            }
#endif
                            filter.RemoveEntity(entity);
                        }
                    }
                }
                if (excludeList != null)
                {
                    foreach (var filter in excludeList)
                    {
                        if (IsMaskCompatibleWithout(filter.GetMask(), entity, componentType))
                        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                            if (filter.SparseEntities[entity] > 0)
                            {
                                throw new Exception("Entity already in filter.");
                            }
#endif
                            filter.AddEntity(entity);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsMaskCompatible(Mask filterMask, int entity)
        {
            for (int i = 0, iMax = filterMask.IncludeCount; i < iMax; i++)
            {
                if (!_pools[filterMask.Include[i]].Has(entity))
                {
                    return false;
                }
            }
            for (int i = 0, iMax = filterMask.ExcludeCount; i < iMax; i++)
            {
                if (_pools[filterMask.Exclude[i]].Has(entity))
                {
                    return false;
                }
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsMaskCompatibleWithout(Mask filterMask, int entity, int componentId)
        {
            for (int i = 0, iMax = filterMask.IncludeCount; i < iMax; i++)
            {
                var typeId = filterMask.Include[i];
                if (typeId == componentId || !_pools[typeId].Has(entity))
                {
                    return false;
                }
            }
            for (int i = 0, iMax = filterMask.ExcludeCount; i < iMax; i++)
            {
                var typeId = filterMask.Exclude[i];
                if (typeId != componentId && _pools[typeId].Has(entity))
                {
                    return false;
                }
            }
            return true;
        }

        public struct Config
        {
            public int Entities;
            public int RecycledEntities;
            public int Pools;
            public int Filters;
            public int PoolDenseSize;
            public int PoolRecycledSize;
            public int EntityComponentsSize;

            internal const int EntitiesDefault = 512;
            internal const int RecycledEntitiesDefault = 512;
            internal const int PoolsDefault = 512;
            internal const int FiltersDefault = 512;
            internal const int PoolDenseSizeDefault = 512;
            internal const int PoolRecycledSizeDefault = 512;
            internal const int EntityComponentsSizeDefault = 8;
        }

#if ENABLE_IL2CPP
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
#endif
        public sealed class Mask
        {
            private readonly SFEcsWorld _world;
            internal int[] Include;
            internal int[] Exclude;
            internal int IncludeCount;
            internal int ExcludeCount;
            internal int Hash;
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            private bool _built;
#endif

            internal Mask(SFEcsWorld world)
            {
                _world = world;
                Include = new int[8];
                Exclude = new int[2];
                Reset();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void Reset()
            {
                IncludeCount = 0;
                ExcludeCount = 0;
                Hash = 0;
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                _built = false;
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Mask Inc<T>() where T : struct
            {
                var poolId = _world.GetPool<T>().GetId();
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                if (_built)
                {
                    throw new Exception("Cant change built mask.");
                }
                if (Array.IndexOf(Include, poolId, 0, IncludeCount) != -1)
                {
                    throw new Exception($"{typeof(T).Name} already in constraints list.");
                }
                if (Array.IndexOf(Exclude, poolId, 0, ExcludeCount) != -1)
                {
                    throw new Exception($"{typeof(T).Name} already in constraints list.");
                }
#endif
                if (IncludeCount == Include.Length)
                {
                    Array.Resize(ref Include, IncludeCount << 1);
                }
                Include[IncludeCount++] = poolId;
                return this;
            }

#if UNITY_2020_3_OR_NEWER
            [UnityEngine.Scripting.Preserve]
#endif
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Mask Exc<T>() where T : struct
            {
                var poolId = _world.GetPool<T>().GetId();
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                if (_built)
                {
                    throw new Exception("Cant change built mask.");
                }
                if (Array.IndexOf(Include, poolId, 0, IncludeCount) != -1)
                {
                    throw new Exception($"{typeof(T).Name} already in constraints list.");
                }
                if (Array.IndexOf(Exclude, poolId, 0, ExcludeCount) != -1)
                {
                    throw new Exception($"{typeof(T).Name} already in constraints list.");
                }
#endif
                if (ExcludeCount == Exclude.Length)
                {
                    Array.Resize(ref Exclude, ExcludeCount << 1);
                }
                Exclude[ExcludeCount++] = poolId;
                return this;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SFEcsFilter End(int capacity = 512)
            {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                if (_built)
                {
                    throw new Exception("Cant change built mask.");
                }
                _built = true;
#endif
                Array.Sort(Include, 0, IncludeCount);
                Array.Sort(Exclude, 0, ExcludeCount);
                // calculate hash.
                unchecked
                {
                    Hash = IncludeCount + ExcludeCount;
                    for (int i = 0, iMax = IncludeCount; i < iMax; i++)
                    {
                        Hash = Hash * 314159 + Include[i];
                    }
                    for (int i = 0, iMax = ExcludeCount; i < iMax; i++)
                    {
                        Hash = Hash * 314159 - Exclude[i];
                    }
                }
                var (filter, isNew) = _world.GetFilterInternal(this, capacity);
                if (!isNew)
                {
                    Recycle();
                }
                return filter;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void Recycle()
            {
                Reset();
                if (_world._masksCount == _world._masks.Length)
                {
                    Array.Resize(ref _world._masks, _world._masksCount << 1);
                }
                _world._masks[_world._masksCount++] = this;
            }
        }
    }

#if DEBUG || SFECS_WORLD_EVENTS
    public interface ISFEcsWorldEventListener
    {
        void OnEntityCreated(int entity);
        void OnEntityChanged(int entity, short poolId, bool added);
        void OnEntityDestroyed(int entity);
        void OnFilterCreated(SFEcsFilter filter);
        void OnWorldResized(int newSize);
        void OnWorldDestroyed(SFEcsWorld world);
    }
#endif
}

#if ENABLE_IL2CPP
// Unity IL2CPP performance optimization attribute.
namespace Unity.IL2CPP.CompilerServices
{
    internal enum Option
    {
        NullChecks = 1,
        ArrayBoundsChecks = 2
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal class Il2CppSetOptionAttribute : Attribute
    {
        public Option Option
        {
            get; private set;
        }
        public object Value
        {
            get; private set;
        }

        public Il2CppSetOptionAttribute(Option option, object value)
        {
            Option = option;
            Value = value;
        }
    }
}
#endif
