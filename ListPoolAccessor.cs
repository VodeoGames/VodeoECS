using VodeoECS.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VodeoECS
{
    /// <summary>
    /// Component Pool Reader for use with Burst Compiled Jobs. 
    /// Can be passed to a job and can read and write the Elements of List Components, destroy or append them, but not destroy or create new List Components. 
    /// </summary>
    /// <typeparam name="T">The type of the List Component Elements accessed by this Accessor.</typeparam>
    public unsafe struct ListPoolAccessor<T> : IDisposable, IEnumerable<ListAccessor<T>> where T : unmanaged, IElementComponent
    {
        private NativeArray<UnsafeList<UnsafeList<T>>> lists;
        private NativeSlice<ComponentIndex> indexMap;
        private readonly NativeSlice<Taxon> taxa;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private NativeArray<AtomicSafetyHandle> handles;
#endif
        /// <summary>
        /// For internal use by the Component Pool class. Constructs a List Pool Accessor.
        /// </summary>
        /// <param name="components">List of List Component NativeNested containers for each Taxon in the Pool.</param>
        /// <param name="indices">Slice into the index map for the Pool.</param>
        /// <param name="taxa">Slice into the list of all created taxa for the pool.</param>
        /// <param name="allocator">Memory Allocator to use.</param>
        public ListPoolAccessor ( List<NativeNested<T>> components, NativeSlice<ComponentIndex> indices, NativeSlice<Taxon> taxa, Allocator allocator )
        {
            this.lists = new NativeArray<UnsafeList<UnsafeList<T>>>( components.Count, allocator );
            this.indexMap = indices;
            this.taxa = taxa;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.handles = new NativeArray<AtomicSafetyHandle>( components.Count, allocator );
#endif

            for ( int i = 0; i < taxa.Length; i++ )
            {
                int taxonID = taxa[i].ID;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                handles[taxonID] = components[taxonID].Safety;
                AtomicSafetyHandle.CheckReadAndThrow( handles[taxonID] );
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting( handles[taxonID], false );
#endif
                lists[taxonID] = new UnsafeList<UnsafeList<T>>( ( UnsafeList<T>* )components[taxonID].GetNestedSlice( ).GetUnsafeReadOnlyPtr( ), components[taxonID].Length );
            }
        }

        public ListAccessor<T> this[ComponentIndex index]
        {
            get
            {
                return this[index.taxonID, index.entry];
            }
        }
        public ListAccessor<T> this[Entity entity]
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
        /// The order of Entities associated with each Component in a Taxon Slice is preserved across all Component Pools sharing the Taxon.
        /// </summary>
        /// <returns>An accessor to each component in this pool.</returns>
        public IEnumerator<ListAccessor<T>> GetEnumerator ( )
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.handles.Dispose( );
#endif
            this.lists.Dispose( );
        }
        private ListAccessor<T> this[int t, int i]
        {
            get
            {
                NativeSlice<UnsafeList<T>> slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<UnsafeList<T>>( lists[t].Ptr, sizeof( UnsafeList<T> ), lists[t].Length );
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeSliceUnsafeUtility.SetAtomicSafetyHandle( ref slice, handles[t] );
#endif
                return new ListAccessor<T>( slice, i );
            }
        }
    }
}