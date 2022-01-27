using UnityEngine;

namespace VodeoECS.Standard
{
    /// <summary>
    /// The rendering mesh of an Entity.
    /// </summary>
    public struct MeshRenderFilter : IFilterComponent<MeshRenderFilter>
    {
        public RegistryIndex<Mesh> mesh;
        public bool Equals ( MeshRenderFilter other )
        {
            return ( this.mesh == other.mesh );
        }
        public override int GetHashCode ( ) { return (mesh).GetHashCode( ); }
    }
}