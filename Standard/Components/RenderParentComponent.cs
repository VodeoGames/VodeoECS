using Unity.Mathematics;

namespace VodeoECS.Standard
{
    /// <summary>
    /// The parent Entity and offset that the Entity should be rendered relative to.
    /// </summary>
    public struct RenderParentComponent : IDataComponent
    {
        public Entity parent;
        public float3 offset;
    }
}
