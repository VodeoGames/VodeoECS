using UnityEngine;

namespace VodeoECS.Standard
{
    /// <summary>
    /// Registry Loader for loading Meshes.
    /// </summary>
    public class MeshLoader : IRegistryLoader<Mesh>
    {
        public Mesh Load ( string name )
        {
            return Resources.Load<Mesh>( "Meshes/" + name );
        }
    }
}