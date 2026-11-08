namespace SFramework.ECS.Runtime
{
    public interface IEcsInclude
    {
        SFEcsWorld.Mask CreateMask(SFEcsWorld world);
    }

    public interface IEcsExclude
    {
        SFEcsWorld.Mask ExcludeFrom(SFEcsWorld.Mask mask);
    }

    public readonly struct NoExcludes : IEcsExclude
    {
        public SFEcsWorld.Mask ExcludeFrom(SFEcsWorld.Mask mask) => mask;
    }

    public readonly struct Inc<T1> : IEcsInclude
        where T1 : struct
    {
        public SFEcsWorld.Mask CreateMask(SFEcsWorld world) =>
            world.Filter<T1>();
    }

    public readonly struct Inc<T1, T2> : IEcsInclude
        where T1 : struct
        where T2 : struct
    {
        public SFEcsWorld.Mask CreateMask(SFEcsWorld world) =>
            world.Filter<T1>().Inc<T2>();
    }

    public readonly struct Inc<T1, T2, T3> : IEcsInclude
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        public SFEcsWorld.Mask CreateMask(SFEcsWorld world) =>
            world.Filter<T1>().Inc<T2>().Inc<T3>();
    }

    public readonly struct Inc<T1, T2, T3, T4> : IEcsInclude
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
    {
        public SFEcsWorld.Mask CreateMask(SFEcsWorld world) =>
            world.Filter<T1>().Inc<T2>().Inc<T3>().Inc<T4>();
    }

    public readonly struct Inc<T1, T2, T3, T4, T5> : IEcsInclude
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
        where T5 : struct
    {
        public SFEcsWorld.Mask CreateMask(SFEcsWorld world) =>
            world.Filter<T1>().Inc<T2>().Inc<T3>().Inc<T4>().Inc<T5>();
    }

    public readonly struct Inc<T1, T2, T3, T4, T5, T6> : IEcsInclude
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
        where T5 : struct
        where T6 : struct
    {
        public SFEcsWorld.Mask CreateMask(SFEcsWorld world) =>
            world.Filter<T1>().Inc<T2>().Inc<T3>().Inc<T4>().Inc<T5>().Inc<T6>();
    }

    public readonly struct Inc<T1, T2, T3, T4, T5, T6, T7> : IEcsInclude
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
        where T5 : struct
        where T6 : struct
        where T7 : struct
    {
        public SFEcsWorld.Mask CreateMask(SFEcsWorld world) =>
            world.Filter<T1>().Inc<T2>().Inc<T3>().Inc<T4>().Inc<T5>().Inc<T6>().Inc<T7>();
    }

    public readonly struct Exc<T1> : IEcsExclude
        where T1 : struct
    {
        public SFEcsWorld.Mask ExcludeFrom(SFEcsWorld.Mask mask) =>
            mask.Exc<T1>();
    }

    public readonly struct Exc<T1, T2> : IEcsExclude
        where T1 : struct
        where T2 : struct
    {
        public SFEcsWorld.Mask ExcludeFrom(SFEcsWorld.Mask mask) =>
            mask.Exc<T1>().Exc<T2>();
    }

    public readonly struct Exc<T1, T2, T3> : IEcsExclude
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        public SFEcsWorld.Mask ExcludeFrom(SFEcsWorld.Mask mask) =>
            mask.Exc<T1>().Exc<T2>().Exc<T3>();
    }
}
