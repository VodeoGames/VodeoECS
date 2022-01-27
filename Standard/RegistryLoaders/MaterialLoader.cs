using UnityEngine;

namespace VodeoECS.Standard
{
    /// <summary>
    /// Registry Loader for loading Materials.
    /// </summary>
    public class MaterialLoader : IRegistryLoader<Material>
    {
        public Material Load ( string name )
        {
            return Resources.Load<Material>( "Materials/" + name );
        }
    }
}