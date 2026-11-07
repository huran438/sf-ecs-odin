namespace SFramework.ECS.Runtime
{
    public interface ISFEcsAutoInit<T> where T : struct
    {
        void AutoInit(ref T c);
    }
}
