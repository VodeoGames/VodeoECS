using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VodeoECS
{
    /// <summary>
    /// A slice into the Filter Components associated with a single Taxon.
    /// The order of Entities associated with each Component in a Taxon Slice is preserved across all Component Pools sharing the Taxon.
    /// Can be passed to Burst Compiled jobs. 
    /// </summary>
    /// <typeparam name="T">The type of the Filter Components accessed by this Taxon Slice.</typeparam>
    public struct FilterTaxonSlice<T> : IEnumerable<T> where T : unmanaged, IFilterComponent<T>
    {
        /// <summary>
        /// Number of Filter Components in this Slice.
        /// </summary>
        public int Length { get { return IDs.Length; } }
        private NativeSlice<int> IDs;
        private NativeSlice<T> filtersByID;

        /// <summary>
        /// For internal use by the Filter Component Pool class. Constructs a Filter Taxon Slice.
        /// </summary>
        /// <param name="filter_slice">A slice into all unique Filter Components in the Filter Pool.</param>
        /// <param name="id_slice">A slice into the Filter indices for a single Taxon in the Filter Pool.</param>
        public FilterTaxonSlice ( NativeSlice<T> filter_slice, NativeSlice<int> id_slice )
        {
            this.IDs = id_slice;
            this.filtersByID = filter_slice;
        }

        /// <summary>
        /// The value of the Filter Component at a specific position in the Taxon Slice.
        /// The order of Entities associated with each Component in a Taxon Slice is preserved across all Component Pools sharing the Taxon.
        /// </summary>
        /// <param name="i">The position of the Filter Component in the Taxon Slice.</param>
        /// <returns>The value of the Filter Component at the given position.</returns>
        public T this[int i]
        {
            get
            {
                return filtersByID[IDs[i]];
            }
        }
        /// <summary>
        /// Enumerates through all Filter Component values in this Taxon Slice.
        /// The order of Entities associated with each Component in a Taxon Slice is preserved across all Component Pools sharing the Taxon.
        /// </summary>
        /// <returns>Each Filter Component value in the Taxon Slice.</returns>
        public IEnumerator<T> GetEnumerator ( )
        {
            for ( int i = 0; i < this.Length; i++ )
            {
                yield return filtersByID[IDs[i]];
            }
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return GetEnumerator( );
        }
    }
}