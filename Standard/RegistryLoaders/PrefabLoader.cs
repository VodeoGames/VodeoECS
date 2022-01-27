using System;
using UnityEngine;

namespace VodeoECS.Standard
{
    /// <summary>
    /// Registry Loader for loading Prefabs.
    /// </summary>
    public class PrefabLoader : IRegistryLoader<PrefabPool>
    {
        private Transform pools;
        public PrefabLoader ( )
        {
            this.pools = new GameObject( "ObjectPools" ).transform;
        }
        public PrefabPool Load ( string name )
        {
            PrefabPool pool = new GameObject( name ).AddComponent<PrefabPool>( );
            pool.transform.parent = this.pools;
            pool.prefab = Resources.Load<GameObject>( "Prefabs/" + name );
            if ( pool.prefab == null ) throw new Exception( "Prefab " + name + " not found in assets." );

            return pool;
        }
    }
}
