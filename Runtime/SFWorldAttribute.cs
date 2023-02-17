using SFramework.Core.Runtime;
using SFramework.Repositories.Runtime;

namespace SFramework.ECS.Runtime
{
    public class SFWorldAttribute : SFIdAttribute
    {
        public SFWorldAttribute() : base(typeof(SFWorldsRepository), -1)
        {
        }
    }
}