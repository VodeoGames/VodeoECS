using VodeoECS.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VodeoECS
{
    /// <summary>
    /// Accessor for reading and writing to a single List Component.
    /// Can be passed to Burst Compiled jobs. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct ListAccessor<T> : IEnumerable<T> where T : unmanaged, IElementComponent
    {
        /// <summary>
        /// Number of Elements in the List Component accessed.
        /// </summary>
        public int Length { get { return nested[entry].Length; } }
        private NativeSlice<UnsafeList<T>> nested;
        int entry;

        /// <summary>
        /// For internal use by the List Component Pool class. Constructs a List Accessor.
        /// </summary>
        /// <param name="nested">A slice into the Pool's NativeNested container.</param>
        /// <param name="entry">The index of the specific List Component to access in the slice.</param>
        public ListAccessor ( NativeSlice<UnsafeList<T>> nested, int entry )
        {
            this.nested = nested;
            this.entry = entry;
        }

        /// <summary>
        /// The Element at a specific position in the accessed List Component.
        /// </summary>
        /// <param name="i">The position of the Element in the List Component.</param>
        /// <returns>The value of the Element accessed.</returns>
        public unsafe T this[int i]
        {
            get
            {
                return UnsafeUtility.ReadArrayElement<T>( this.nested[entry].Ptr, i );
            }
            set
            {
                UnsafeUtility.WriteArrayElement<T>( this.nested[entry].Ptr, i, value );
            }
        }
        /// <summary>
        /// Read the value of a specific Element in the List Component.
        /// </summary>
        /// <param name="i">The position of the Element to read in the List Component.</param>
        /// <returns>The value of the Element read.</returns>
        public unsafe T ReadElement ( int i )
        {
            return this[i];
        }
        /// <summary>
        /// Write to a specific Element in the List Component.
        /// </summary>
        /// <param name="i">The position of the Element to write to in the List Component.</param>
        /// <param name="value">The new value to write to the Element.</param>
        public unsafe void WriteElement ( int i, T value )
        {
            this[i] = value;
        }
        /// <summary>
        /// Append a new Element to the List Component.
        /// </summary>
        /// <param name="value">The value of the new Element to append.</param>
        public unsafe void AppendElement ( T value )
        {
            UnsafeUtility.ArrayElementAsRef<UnsafeList<T>>( this.nested.GetUnsafePtr( ), entry ).Add( value );
        }
        /// <summary>
        /// Remove an Element from the List Component, and swap back to maintain close packing.
        /// </summary>
        /// <param name="i">The position of the Element to remove.</param>
        public unsafe void RemoveElement ( int i )
        {
            UnsafeUtility.ArrayElementAsRef<UnsafeList<T>>( this.nested.GetUnsafePtr( ), entry ).RemoveAtSwapBack( i );
        }
        /// <summary>
        /// Clear the List Component of all its Elements.
        /// </summary>
        public unsafe void ClearList ( )
        {
            UnsafeUtility.ArrayElementAsRef<UnsafeList<T>>( this.nested.GetUnsafePtr( ), entry ).Clear( );
        }

        /// <summary>
        /// Enumerates through all Element values in the accessed List Component.
        /// </summary>
        /// <returns>Each Element value in the List Component.</returns>
        public IEnumerator<T> GetEnumerator ( )
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