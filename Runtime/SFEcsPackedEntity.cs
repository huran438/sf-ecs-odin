// ----------------------------------------------------------------------------
// The MIT-Red License
// Copyright (c) 2012-2026 Leopotam <leopotam@yandex.ru>
// See SFEcsWorld.cs for the MIT-Red license terms.
// ----------------------------------------------------------------------------

using System.Runtime.CompilerServices;

#if ENABLE_IL2CPP
using Unity.IL2CPP.CompilerServices;
#endif

namespace SFramework.ECS.Runtime
{
    public struct SFEcsPackedEntity
    {
        public int Id;
        public int Gen;

        public override int GetHashCode()
        {
            unchecked
            {
                return (23 * 31 + Id) * 31 + Gen;
            }
        }
    }

    public struct SFEcsPackedEntityWithWorld
    {
        public int Id;
        public int Gen;
        public SFEcsWorld World;

        public override int GetHashCode()
        {
            unchecked
            {
                return ((23 * 31 + Id) * 31 + Gen) * 31 + (World?.GetHashCode() ?? 0);
            }
        }
#if DEBUG
        // For using in IDE debugger.
        internal object[] DebugComponentsView
        {
            get
            {
                object[] list = null;
                if (World != null && World.IsAlive() && World.IsEntityAliveInternal(Id) && World.GetEntityGen(Id) == Gen)
                {
                    World.GetComponents(Id, ref list);
                }
                return list;
            }
        }
        // For using in IDE debugger.
        internal int DebugComponentsCount
        {
            get
            {
                if (World != null && World.IsAlive() && World.IsEntityAliveInternal(Id) && World.GetEntityGen(Id) == Gen)
                {
                    return World.GetComponentsCount(Id);
                }
                return 0;
            }
        }

        // For using in IDE debugger.
        public override string ToString()
        {
            if (Id == 0 && Gen == 0)
            {
                return "Entity-Null";
            }
            if (World == null || !World.IsAlive() || !World.IsEntityAliveInternal(Id) || World.GetEntityGen(Id) != Gen)
            {
                return "Entity-NonAlive";
            }
            System.Type[] types = null;
            var count = World.GetComponentTypes(Id, ref types);
            System.Text.StringBuilder sb = null;
            if (count > 0)
            {
                sb = new System.Text.StringBuilder(512);
                for (var i = 0; i < count; i++)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(",");
                    }
                    sb.Append(types[i].Name);
                }
            }
            return $"Entity-{Id}:{Gen} [{sb}]";
        }
#endif
    }

#if ENABLE_IL2CPP
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
#endif
    public static class SFEcsEntityExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SFEcsPackedEntity PackEntity(this SFEcsWorld world, int entity)
        {
            SFEcsPackedEntity packed;
            packed.Id = entity;
            packed.Gen = world.GetEntityGen(entity);
            return packed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Unpack(this in SFEcsPackedEntity packed, SFEcsWorld world, out int entity)
        {
            entity = packed.Id;
            return
                world != null
                && world.IsAlive()
                && world.IsEntityAliveInternal(packed.Id)
                && world.GetEntityGen(packed.Id) == packed.Gen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EqualsTo(this in SFEcsPackedEntity a, in SFEcsPackedEntity b)
        {
            return a.Id == b.Id && a.Gen == b.Gen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SFEcsPackedEntityWithWorld PackEntityWithWorld(this SFEcsWorld world, int entity)
        {
            SFEcsPackedEntityWithWorld packedEntity;
            packedEntity.World = world;
            packedEntity.Id = entity;
            packedEntity.Gen = world.GetEntityGen(entity);
            return packedEntity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Unpack(this in SFEcsPackedEntityWithWorld packedEntity, out SFEcsWorld world, out int entity)
        {
            world = packedEntity.World;
            entity = packedEntity.Id;
            return
                world != null
                && world.IsAlive()
                && world.IsEntityAliveInternal(packedEntity.Id)
                && world.GetEntityGen(packedEntity.Id) == packedEntity.Gen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EqualsTo(this in SFEcsPackedEntityWithWorld a, in SFEcsPackedEntityWithWorld b)
        {
            return a.Id == b.Id && a.Gen == b.Gen && a.World == b.World;
        }
    }
}
