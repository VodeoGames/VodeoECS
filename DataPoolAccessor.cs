using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VodeoECS
{
    /// <summary>
    /// Component Pool Reader for use with Burst Compiled Jobs. 
    /// Can be passed to a job and can read Data Components and write to them, but not destroy them or create new ones.
    /// </summary>
    /// <typeparam name="T">The type of the Data Components accessed by this Accessor.</typeparam>
    public unsafe struct DataPoolAccessor<T> : IDisposable, IEnumerable<DataAccessor<T>> where T : unmanaged, IDataComponent
    {
        private NativeArray<UnsafeList<T>> lists;
        private readonly NativeSlice<ComponentIndex> indexMap;
        private readonly NativeSlice<Taxon> taxa;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private NativeArray<AtomicSafetyHandle> handles;
#endif
        /// <summary>
        /// For internal use by the Component Pool class. Constructs a Data Pool Accessor.
        /// </summary>
        /// <param name="components">List of Data Component NativeLists for each Taxon in the Pool.</param>
        /// <param name="indices">Slice into the index map for the Pool.</param>
        /// <param name="taxa">Slice into the list of all created taxa for the pool.</param>
        /// <param name="allocator">Memory Allocator to use.</param>
        public DataPoolAccessor ( IReadOnlyList<NativeList<T>> components, NativeSlice<ComponentIndex> indices, NativeSlice<Taxon> taxa, Allocator allocator )
        {
            this.lists = new NativeArray<UnsafeList<T>>( components.Count, allocator );
            this.indexMap = indices;
            this.taxa = taxa;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.handles = new NativeArray<AtomicSafetyHandle>( components.Count, allocator );
#endif

            for ( int i = 0; i < taxa.Length; i++ )
            {
                int taxonID = taxa[i].ID;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeSlice<T> slice = components[taxonID].AsArray( );
                handles[taxonID] = NativeSliceUnsafeUtility.GetAtomicSafetyHandle( slice );
                AtomicSafetyHandle.CheckReadAndThrow( handles[taxonID] );
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting( handles[taxonID], false );
#endif
                this.lists[taxonID] = new UnsafeList<T>( ( T* )components[taxonID].GetUnsafePtr( ), components[taxonID].Length );
            }
        }
        /// <summary>
        /// Get access to a Data Component in this pool, by ComponentIndex.
        /// This is more efficient than by Entity if the same ComponentIndex is used repeatedly.
        /// </summary>
        /// <param name="index">The ComponentIndex of the Data Component requested.</param>
        /// <returns>An accessor for the requested Data Component.</returns>
        public DataAccessor<T> this[ComponentIndex index]
        {
            get
            {
                return this[index.taxonID, index.entry];
            }
        }
        /// <summary>
        /// Get access to a Data Component in this pool, by Entity.
        /// </summary>
        /// <param name="entity">The Entity associated with the Data Component requested.</param>
        /// <returns>An accessor for the requested Data Component.</returns>
        public DataAccessor<T> this[Entity entity]
        {
            get
            {
                ComponentIndex index = this.indexMap[entity.ID];
                return this[index];
            }
        }
        /// <summary>
        /// Get the Component Index for a given Entity. Components within an Archetype share the same ComponentIndex if their Entities implement the Archetype! 
        /// Component Indices should not be stored permanently as they are invalidated by Component removal, and Archetype or Filter changes. Accessing a Component through a Component Index is more efficient than through an Entity.
        /// </summary>
        /// <param name="entity">The ComponentIndex for this Entity will be returned.</param>
        /// <returns>The ComponentIndex requested.</returns>
        public ComponentIndex GetIndex ( Entity entity )
        {
            return this.indexMap[entity.ID];
        }
        /// <summary>
        /// Does the given Entity have a Component in this pool?
        /// </summary>
        /// <param name="entity">The Entity to test.</param>
        /// <returns>Returns true if the Entity has a Component in this Pool, false if not.</returns>
        public bool HasComponent ( Entity entity )
        {
            if ( entity.ID >= indexMap.Length ) return false;
            return indexMap[entity.ID] != ComponentIndex.Null;
        }

        /// <summary>
        /// Enumerates through access to all components in this pool.
        /// </summary>
        /// <returns>An accessor to each component in this pool.</returns>
        public IEnumerator<DataAccessor<T>> GetEnumerator ( )
        {
            foreach ( Taxon taxon in taxa )
            {
                for ( int i = 0; i < this.lists[taxon.ID].Length; i++ )
                {
                    yield return this[i, taxon.ID];
                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return GetEnumerator( );
        }

        /// <summary>
        /// Enumerates through the values of all Data Components in the accessed pool.
        /// </summary>
        public IEnumerable<T> Values
        {
            get
            {
                foreach ( Taxon taxon in taxa )
                {
                    foreach ( T value in lists[taxon.ID] )
                    {
                        yield return value;
                    }
                }
            }
        }

        /// <summary>
        /// Dispose the native memory reserved by this Pool Accessor.
        /// </summary>
        public void Dispose ( )
        {
            for ( int i = 0; i < taxa.Length; i++ )
            {
                int id = taxa[i].ID;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting( handles[id], true );
#endif
                this.lists[id].Dispose( );
            }
            this.lists.Dispose( );
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.handles.Dispose( );
#endif
        }

        private DataAccessor<T> this[int t, int i]
        {
            get
            {
                NativeSlice<T> slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<T>( lists[t].Ptr + i, sizeof( T ), 1 );
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeSliceUnsafeUtility.SetAtomicSafetyHandle( ref slice, handles[t] );
#endif
                return new DataAccessor<T>( slice );
            }
        }
    }
}