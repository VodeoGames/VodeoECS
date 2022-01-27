using VodeoECS.Internal;
using System.Collections;
using System.Collections.Generic;

namespace VodeoECS
{
    /// <summary>
    /// A slice into the List Components associated with a single Taxon. 
    /// The order of Entities associated with each Component in a Taxon Slice is preserved across all Component Pools sharing the Taxon.
    /// Can be passed to Burst Compiled jobs. 
    /// </summary>
    /// <typeparam name="T">The type of the List Component Elements accessed by this Taxon Slice.</typeparam>
    public struct ListTaxonSlice<T> : IEnumerable<ListAccessor<T>> where T : unmanaged, IElementComponent
    {
        /// <summary>
        /// Number of List Components in this Slice.
        /// </summary>
        public int Length { get { return nested.Length; } }
        private NativeNested<T> nested;

        /// <summary>
        /// For internal use by the List Component Pool class. Constructs a List Taxon Slice.
        /// </summary>
        /// <param name="nested">The Native Nested container of List Components for a single Taxon.</param>
        public ListTaxonSlice ( NativeNested<T> nested )
        {
            this.nested = nested;
        }

        /// <summary>
        /// Access the List Component at a specific position in the Taxon Slice.
        /// The order of Entities associated with each Component in a Taxon Slice is preserved across all Component Pools sharing the Taxon.
        /// </summary>
        /// <param name="i">The position of the List Component in the Taxon Slice.</param>
        /// <returns>An accessor for the List Component at the given position.</returns>
        public ListAccessor<T> this[int i]
        {
            get
            {
                return new ListAccessor<T>( this.nested.GetNestedSlice( ), i );
            }
        }
        /// <summary>
        /// Enumerates access to all List Component values in this Taxon Slice.
        /// </summary>
        /// <returns>Accessor to each List Component in the Taxon Slice.</returns>
        public IEnumerator<ListAccessor<T>> GetEnumerator ( )
        {
            for ( int i = 0; i < this.Length; i++ )
            {
                yield return this[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return GetEnumerator( );
        }
    }
}