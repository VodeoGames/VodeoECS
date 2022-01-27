using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VodeoECS.Internal
{
    /// <summary>
    /// A container that encapsulates a Nativelist of UnsafeLists, taking care of the appropriate safety checks. Useful because normally Nativelists cannot contain containers themselves.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to contain.</typeparam>
    [NativeContainer]
    public struct NativeNested<T> : IDisposable, IEnumerable<NativeSlice<T>> where T : unmanaged
    {
        /// <summary>
        /// Is the list created?
        /// </summary>
        public bool IsCreated { get { return lists.IsCreated; } }
        /// <summary>
        /// The number of nested sub-lists.
        /// </summary>
        public int Length { get { return lists.Length; } }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// Provides the Atomatic Safety Handle of the underlying NativeList.
        /// </summary>
        public AtomicSafetyHandle Safety { get { return NativeListUnsafeUtility.GetAtomicSafetyHandle( ref lists ); } }
#endif
        private NativeList<UnsafeList<T>> lists;
        Allocator m_AllocatorLabel;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="listsCapacity">Initial capacity for number of nested sub-lists.</param>
        /// <param name="allocator">Allocator to use.</param>
        public NativeNested ( int listsCapacity = 0, Allocator allocator = Allocator.Persistent )
        {
            lists = new NativeList<UnsafeList<T>>( listsCapacity, allocator );
            this.m_AllocatorLabel = allocator;
        }

        /// <summary>
        /// Returns the length of the nested sub-list at the given index.
        /// </summary>
        /// <param name="list">Index of the nested sub-list.</param>
        /// <returns>The requested length.</returns>
        public int GetListLength ( int list )
        {
            if ( !this.lists[list].IsCreated )
                return 0;
            else return this.lists[list].Length;
        }

        /// <summary>
        /// Read an element in one of the nested sub-lists.
        /// </summary>
        /// <param name="list">Index of the nested sub-list to read from.</param>
        /// <param name="index">Index of the element to read, in the given nested-sublist.</param>
        /// <returns>Value of the requested element.</returns>
        public unsafe T ReadAt ( int list, int index )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow( this.Safety );
#endif
            return UnsafeUtility.ReadArrayElement<T>( this.lists[list].Ptr, index );
        }

        /// <summary>
        /// Add a new nested sub-list to this NativeNested container.
        /// </summary>
        /// <param name="initialCapacity">Initial capacity of the new nested sub-list.</param>
        public void CreateNestedList ( int initialCapacity )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow( this.Safety );
#endif
            this.lists.Add( new UnsafeList<T>( initialCapacity, m_AllocatorLabel ) );
        }

        /// <summary>
        /// Set an element in one of the nested sub-lists.
        /// </summary>
        /// <param name="list">Index of the nested sub-list to write to.</param>
        /// <param name="index">Index of the element to replace, in the given nested-sublist.</param>
        /// <param name="value">Value to write into the element.</param>
        public unsafe void SetAt ( int list, int index, T value )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow( this.Safety );
#endif
            UnsafeUtility.WriteArrayElement<T>( this.lists[list].Ptr, index, value );
        }

        /// <summary>
        /// Appends a new element to a given nested sub-list.
        /// </summary>
        /// <param name="list">Index of the nested sub-list to append to.</param>
        /// <param name="value">Value to append to the nested sub-list.</param>
        public unsafe void AppendElement ( int list, T value )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow( this.Safety );
#endif
            UnsafeUtility.ArrayElementAsRef<UnsafeList<T>>( this.lists.GetUnsafePtr( ), list ).Add( value );
        }

        /// <summary>
        /// Clears a given nested sub-list.
        /// </summary>
        /// <param name="list">Index of the nested sub-list to clear.</param>
        public unsafe void ClearList ( int list )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow( this.Safety );
#endif
            UnsafeUtility.ArrayElementAsRef<UnsafeList<T>>( this.lists.GetUnsafePtr( ), list ).Clear( );
        }

        /// <summary>
        /// Moves a nested sub-list from one NativeNested container to another.
        /// </summary>
        /// <param name="toMove">Index of the nested sub-list to move.</param>
        /// <param name="other">The NativeNested container to move the nested-sublist to.</param>
        public void MoveNested ( int toMove, ref NativeNested<T> other )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow( this.Safety );
#endif
            UnsafeList<T> list = lists[toMove];
            lists.RemoveAtSwapBack( toMove );
            other.AddList( list );
        }

        /// <summary>
        /// Destroy a nested sub-list in the NativeNested container, and swap the last nested sub-list back to maintain close-packing.
        /// </summary>
        /// <param name="list">The index of the nested sub-list to destroy.</param>
        public void DestroyAtSwapBack ( int list )
        {
            lists[list].Dispose( );
            lists.RemoveAtSwapBack( list );
        }

        /// <summary>
        /// Removes an element from one of the nested sub-lists, and swap the last element to maintain close-packing.
        /// </summary>
        /// <param name="list">Index of the nested sub-list to remove an element from.</param>
        /// <param name="index">Index of the element to remove, in the given nested-sublist.</param>
        public void RemoveElement ( int list, int index )
        {
            UnsafeList<T> temp = this.lists[list];
            temp.RemoveAtSwapBack( index );
            this.lists[list] = temp;
        }

        /// <summary>
        /// Create a NativeSlice into a given nested sub-list.
        /// </summary>
        /// <param name="list">The index of the nested sub-list to create a NativeSlice into.</param>
        /// <returns>The requested NativeSlice.</returns>
        public unsafe NativeSlice<T> GetSlice ( int list )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow( this.Safety );
#endif
            NativeSlice<T> slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<T>( this.lists[list].Ptr, sizeof( T ), this.lists[list].Length );
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle<T>( ref slice, this.Safety );
#endif
            return slice;
        }
        /// <summary>
        /// Create a NativeSlice into this NativeNested container. This will be a NativeSlive of UnsafeLists.
        /// </summary>
        /// <returns>The requested NativeSlice.</returns>
        public NativeSlice<UnsafeList<T>> GetNestedSlice ( )
        {
            NativeSlice<UnsafeList<T>> slice = lists.AsArray( ).Slice( );
            return slice;
        }

        /// <summary>
        /// Dispose of the memory reserved for this container.
        /// </summary>
        public void Dispose ( )
        {
            int l = Length;
            for ( int i = 0; i < l; i++ )
            {
                if ( lists[i].IsCreated ) lists[i].Dispose( );
            }
            lists.Dispose( );
        }

        /// <summary>
        /// Enumerates through each nested sub-list in this NativeNested container.
        /// </summary>
        /// <returns>A Nativeslice into each nested sub-list.</returns>
        public IEnumerator<NativeSlice<T>> GetEnumerator ( )
        {
            for ( int i = 0; i < this.lists.Length; i++ )
            {
                yield return this.GetSlice( i );
            }
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return GetEnumerator( );
        }
        private void AddList ( UnsafeList<T> list )
        {
            lists.Add( list );
        }
    }
}