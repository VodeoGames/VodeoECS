using Unity.Mathematics;

namespace VodeoECS.Standard
{
    /// <summary>
    /// The linear trajectory of the Entity in space, over a finite timespan.
    /// </summary>
    public struct TrajectoryComponent : IDataComponent
    {
        public float4 start;
        public float4 end;
    }
}