// ----------------------------------------------------------------------------
// The MIT-Red License
// Copyright (c) 2012-2025 Leopotam <leopotam@yandex.ru>
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
using System.Linq;

namespace SFramework.ECS.Runtime
{
    public struct SFEcsGroupSystemState
    {
        public string Name;
        public bool State;
    }

    public static class SFEcsExtendedSystems
    {
        public static ISFEcsSystems AddGroup(this ISFEcsSystems systems, string name, bool enabled,
            string eventsWorldName, params ISFEcsSystem[] children)
        {
            return systems.Add(new SFEcsGroupSystem(name, enabled, eventsWorldName, children));
        }

        public static ISFEcsSystems DelHere<T>(this ISFEcsSystems systems, string worldName = null) where T : struct
        {
            return systems.Add(new SFEcsDelHereSystem<T>(worldName));
        }
    }

    public class SFEcsDelHereSystem<T> : ISFEcsPreInitSystem, ISFEcsRunSystem where T : struct
    {
        private readonly string _worldName;
        private SFEcsFilterNative _filter;
        private SFEcsPoolNative<T> _pool;

        public SFEcsDelHereSystem(string worldName = null) => _worldName = worldName;

        public void PreInit(ISFEcsSystems systems)
        {
            var world = SFEcsContext.RequireWorld(systems, _worldName);
            _filter = world.Filter<T>().End();
            _pool = world.GetPool<T>();
        }

        public void Run(ISFEcsSystems systems)
        {
            foreach (var entity in _filter) _pool.Del(entity);
        }
    }

    public class SFEcsGroupSystem : ISFEcsPreInitSystem, ISFEcsInitSystem, ISFEcsRunSystem, ISFEcsPostRunSystem,
        ISFEcsDestroySystem, ISFEcsPostDestroySystem
    {
        private readonly ISFEcsSystem[] _children;
        private readonly ISFEcsRunSystem[] _run;
        private readonly ISFEcsPostRunSystem[] _postRun;
        private readonly string _eventsWorldName;
        private SFEcsFilterNative _events;
        private SFEcsPoolNative<SFEcsGroupSystemState> _eventPool;

        public string Name { get; }
        public bool Enabled { get; private set; }
        public IReadOnlyList<ISFEcsSystem> NestedSystems { get; }

        public SFEcsGroupSystem(string name, bool enabled, string eventsWorldName, params ISFEcsSystem[] children)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A group name is required.", nameof(name));
            if (children == null || children.Length == 0 || Array.IndexOf(children, null) >= 0)
                throw new ArgumentException("A group requires at least one system and no null entries.", nameof(children));

            Name = name;
            Enabled = enabled;
            _eventsWorldName = eventsWorldName;
            _children = (ISFEcsSystem[])children.Clone();
            NestedSystems = Array.AsReadOnly(_children);
            _run = _children.OfType<ISFEcsRunSystem>().ToArray();
            _postRun = _children.OfType<ISFEcsPostRunSystem>().ToArray();
        }

        public void PreInit(ISFEcsSystems systems)
        {
            var world = SFEcsContext.RequireWorld(systems, _eventsWorldName);
            _eventPool = world.GetPool<SFEcsGroupSystemState>();
            _events = world.Filter<SFEcsGroupSystemState>().End();
            foreach (var child in _children)
            {
                if (child is not ISFEcsPreInitSystem preInit) continue;
                preInit.PreInit(systems);
                CheckWorlds(systems, child, nameof(PreInit));
            }
        }

        public void Init(ISFEcsSystems systems)
        {
            foreach (var child in _children)
            {
                if (child is not ISFEcsInitSystem init) continue;
                init.Init(systems);
                CheckWorlds(systems, child, nameof(Init));
            }
        }

        public void Run(ISFEcsSystems systems)
        {
            ApplyStateChanges();
            if (!Enabled) return;
            for (var i = 0; i < _run.Length; i++)
            {
                var child = _run[i];
#if UNITY_EDITOR
                using (SFEcsSystemsProfiler.MeasureChild(systems, this, i, postRun: false))
#endif
                    child.Run(systems);
                CheckWorlds(systems, child, nameof(Run));
            }
        }

        public void PostRun(ISFEcsSystems systems)
        {
            ApplyStateChanges();
            if (!Enabled) return;
            for (var i = 0; i < _postRun.Length; i++)
            {
                var child = _postRun[i];
#if UNITY_EDITOR
                using (SFEcsSystemsProfiler.MeasureChild(systems, this, i, postRun: true))
#endif
                    child.PostRun(systems);
                CheckWorlds(systems, child, nameof(PostRun));
            }
        }

        public void Destroy(ISFEcsSystems systems)
        {
            Shutdown<ISFEcsDestroySystem>(systems, static (child, pipeline) => child.Destroy(pipeline));
        }

        public void PostDestroy(ISFEcsSystems systems)
        {
            Shutdown<ISFEcsPostDestroySystem>(systems, static (child, pipeline) => child.PostDestroy(pipeline));
        }

        private void ApplyStateChanges()
        {
            foreach (var entity in _events)
            {
                ref var change = ref _eventPool.Get(entity);
                if (change.Name != Name) continue;
                Enabled = change.State;
                _eventPool.Del(entity);
            }
        }

        private void Shutdown<T>(ISFEcsSystems systems, Action<T, ISFEcsSystems> callback) where T : ISFEcsSystem
        {
            List<Exception> failures = null;
            for (var i = _children.Length - 1; i >= 0; i--)
            {
                if (_children[i] is not T child) continue;
                try { callback(child, systems); }
                catch (Exception failure) { (failures ??= new List<Exception>()).Add(failure); }
            }
            if (failures != null) throw new AggregateException($"ECS group '{Name}' shutdown failed.", failures);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private static void CheckWorlds(ISFEcsSystems systems, ISFEcsSystem child, string phase)
        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            var world = SFEcsSystems.CheckForLeakedEntities(systems);
            if (world != null)
                throw new InvalidOperationException($"Empty entity detected in world '{world}' after {child.GetType().Name}.{phase}().");
#endif
        }
    }
}
