using UnityEngine;

namespace VodeoECS.Standard
{
    /// <summary>
    /// The rendering material of an Entity.
    /// </summary>
    public struct MaterialFilter : IFilterComponent<MaterialFilter>
    {
        public RegistryIndex<Material> material;
        public bool Equals ( MaterialFilter other )
        {
            return ( this.material == other.material );
        }
        public override int GetHashCode ( ) { return ( material ).GetHashCode( ); }
    }
}