using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VodeoECS
{
    /// <summary>
    /// A slice into the Data Components associated with a single Taxon. 
    /// The order of Entities associated with each Component in a Taxon Slice is preserved across all Component Pools sharing the Taxon.
    /// Can be passed to Burst Compiled jobs. 
    /// </summary>
    /// <typeparam name="T">The type of the Data Components accessed by this Taxon Slice.</typeparam>
    public struct DataTaxonSlice<T> : IEnumerable<T> where T : unmanaged, IDataComponent
    {
        /// <summary>
        /// Number of Data Components in this Slice.
        /// </summary>
        public int Length { get { return slice.Length; } }
        private NativeSlice<T> slice;

        /// <summary>
        /// For internal use by the Data Component Pool class. Constructs a Data Taxon Slice.
        /// </summary>
        /// <param name="slice">A NativeSlice into the Data Components for a single Taxon.</param>
        public DataTaxonSlice ( NativeSlice<T> slice )
        {
            this.slice = slice;
        }

        /// <summary>
        /// The Data Component at a specific position in the Taxon Slice.
        /// The order of Entities associated with each Component in a Taxon Slice is preserved across all Component Pools sharing the Taxon.
        /// </summary>
        /// <param name="i">The position of the Data Component in the Taxon Slice.</param>
        /// <returns>The value of the Data Component at the given position.</returns>
        public T this[int i]
        {
            get
            {
                return slice[i];
            }
            set
            {
                this.slice[i] = value;
            }
        }
        /// <summary>
        /// Enumerates through all Data Component values in this Taxon Slice.
        /// The order of Entities associated with each Component in a Taxon Slice is preserved across all Component Pools sharing the Taxon.
        /// </summary>
        /// <returns>Each Data Component value in the Taxon Slice.</returns>
        public IEnumerator<T> GetEnumerator ( )
        {
            for ( int i = 0; i < this.Length; i++ )
            {
                yield return slice[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return GetEnumerator( );
        }
    }
}