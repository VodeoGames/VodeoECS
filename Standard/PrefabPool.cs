using System.Collections.Generic;
using UnityEngine;

namespace VodeoECS.Standard
{
    /// <summary>
    /// This is a utility class to create a pool of Unity GameObjects from a given prefab.
    /// </summary>
    public class PrefabPool : MonoBehaviour
    {
        public GameObject prefab;
        public int objectCount = 16;
        public bool preallocate = false;

        Stack<GameObject> inactiveObjects = new Stack<GameObject>( );
        List<GameObject> activeObjects = new List<GameObject>( );
        bool allocated = false;

        public void Awake ( )
        {
            if ( !allocated )
                Allocate( );
        }

        /// <summary>
        /// Spawns a new GameObject in this pool.
        /// </summary>
        /// <returns>The spawned GameObject.</returns>
        public GameObject Spawn ( )
        {
            if ( !preallocate )
            {
                GameObject obj;

                if ( inactiveObjects.Count > 0 )
                {
                    obj = inactiveObjects.Pop( );
                    obj.SetActive( true );
                    activeObjects.Add( obj );
                    return obj;
                }

                obj = Instantiate( prefab.gameObject, transform, false );
                obj.SetActive( true );
                activeObjects.Add( obj );
                GameObject result = obj;

                return result;
            }


            if ( !allocated )
                Allocate( );

            try
            {
                GameObject obj = inactiveObjects.Pop( );
                obj.SetActive( true );
                activeObjects.Add( obj );
                return obj;
            }
            catch
            {
                Debug.LogError( name + ": PrefabPool over maximum capacity", this );
                return null;
            }
        }


        /// <summary>
        /// Despawns a given GameObject in this pool.
        /// </summary>
        /// <param name="obj">The GameObject to despawn.</param>
        /// <returns>The despawned GameObject.</returns>
        public GameObject Despawn ( GameObject obj )
        {
            obj.SetActive( false );
            obj.transform.SetParent( transform );
            inactiveObjects.Push( obj );
            activeObjects.Remove( obj );
            return obj;
        }

        /// <summary>
        /// Despawn all GameObjects in this pool.
        /// </summary>
        public void DespawnAll ( )
        {
            int numActiveObjects = activeObjects.Count - 1;
            for ( int i = numActiveObjects; i >= 0; i-- )
            {
                activeObjects[i].SetActive( false );
                activeObjects[i].transform.SetParent( transform );
                inactiveObjects.Push( activeObjects[i] );
            }
            activeObjects.Clear( );
        }

        /// <summary>
        /// Get a list of all GameObjects in this pool.
        /// </summary>
        /// <returns>The requested list of all GameObjects in this pool.</returns>
        public List<GameObject> GetAll ( )
        {
            List<GameObject> result = new List<GameObject>( );
            foreach ( GameObject obj in activeObjects )
                result.Add( obj );

            return result;
        }

        private void Allocate ( )
        {
            if ( !preallocate )
            {
                allocated = true;
                return;
            }

            for ( int i = 0; i <= objectCount; i++ )
            {
                GameObject obj = Instantiate( prefab.gameObject, transform, false ) as GameObject;
                obj.SetActive( false );
                inactiveObjects.Push( obj );
            }

            allocated = true;
        }

    }
}