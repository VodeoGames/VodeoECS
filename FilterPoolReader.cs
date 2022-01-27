using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VodeoECS
{
    /// <summary>
    /// Component Pool Reader for use with Burst Compiled jobs. 
    /// Can be passed to a job and can read Filter Component Values; but not change, create, or destroy Filter Components. 
    /// </summary>
    /// <typeparam name="T">The type of the Filter Components accessed by this Accessor.</typeparam>
    public unsafe struct FilterPoolReader<T> : IDisposable, IEnumerable<T> where T : unmanaged, IFilterComponent<T>
    {
        private NativeSlice<T> filtersByID;
        private NativeArray<UnsafeList<int>> lists;
        private readonly NativeSlice<Taxon> taxa;
        private readonly NativeSlice<ComponentIndex> indexMap;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private NativeArray<AtomicSafetyHandle> handles;
#endif
        /// <summary>
        /// For internal use by the Component Pool class. Constructs a Filter Pool Accessor.
        /// </summary>
        /// <param name="filter_ids">List of Filter index NativeLists for each Taxon in the Pool.</param>
        /// <param name="filter_slice">Slice into all unique Filter Comoponent values for the pool.</param>
        /// <param name="indices">Slice into the index map for the Pool.</param>
        /// <param name="taxa">Slice into the list of all created taxa for the pool.</param>
        /// <param name="allocator">Memory Allocator to use.</param>
        public FilterPoolReader ( IReadOnlyList<NativeList<int>> filter_ids, NativeSlice<T> filter_slice, NativeSlice<ComponentIndex> indices, NativeSlice<Taxon> taxa, Allocator allocator )
        {
            this.lists = new NativeArray<UnsafeList<int>>( filter_ids.Count, allocator );
            this.indexMap = indices;
            this.taxa = taxa;
            this.filtersByID = filter_slice;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.handles = new NativeArray<AtomicSafetyHandle>( filter_ids.Count, allocator );
#endif

            for ( int i = 0; i < taxa.Length; i++ )
            {
                int taxonID = taxa[i].ID;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeSlice<int> slice = filter_ids[taxonID].AsArray( );
                handles[taxonID] = NativeSliceUnsafeUtility.GetAtomicSafetyHandle( slice );
                AtomicSafetyHandle.CheckReadAndThrow( handles[taxonID] );
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting( handles[taxonID], false );
#endif
                this.lists[taxonID] = new UnsafeList<int>( ( int* )filter_ids[taxonID].GetUnsafePtr( ), filter_ids[taxonID].Length );
            }
        }
        /// <summary>
        /// Read the value of a Filter Component in this pool, by ComponentIndex.
        /// This is more efficient than by Entity if the same ComponentIndex is used repeatedly.
        /// </summary>
        /// <param name="index">The ComponentIndex of the Filter Component requested.</param>
        /// <returns>The value of the requested Filter Component.</returns>
        public T this[ComponentIndex index]
        {
            get
            {
                return this[index.taxonID, index.entry];
            }
        }
        /// <summary>
        /// Read the value of a Filter Component in this pool, by Entity.
        /// </summary>
        /// <param name="entity">The Entity associated with the Filter Component requested.</param>
        /// <returns>The value of the requested Filter Component.</returns>
        public T this[Entity entity]
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
        /// Enumerates through the values of all Filter Components in this pool.
        /// </summary>
        /// <returns>The value of each Filter Component in this pool.</returns>
        public IEnumerator<T> GetEnumerator ( )
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
        /// Enumerates through the values of all Filter Components in this pool.
        /// </summary>
        public IEnumerable<T> Values
        {
            get
            {
                foreach ( Taxon taxon in taxa )
                {
                    foreach ( int id in lists[taxon.ID] )
                    {
                        yield return filtersByID[id];
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates through all unique Filter Component values in this pool. Filter Component values will not be repeated.
        /// </summary>
        /// <returns>Each unique Filter Component value in this pool.</returns>
        public IEnumerable<T> UniqueFilters
        {
            get
            {
                foreach ( T value in this.filtersByID )
                {
                    yield return value;
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

        private T this[int t, int i]
        {
            get
            {
                return filtersByID[lists[t][i]];
            }
        }
    }
}