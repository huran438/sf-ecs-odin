using SFramework.Configs.Runtime;

namespace SFramework.ECS.Runtime
{
    public class SFWorldAttribute : SFIdAttribute
    {
        public SFWorldAttribute() : base(typeof(SFWorldsConfig), -1)
        {
        }
    }
}