namespace SFramework.ECS.Runtime
{
    public interface IEcsAutoInit<T> where T : struct
    {
        void AutoInit(ref T c);
    }
}