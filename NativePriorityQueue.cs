using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace VodeoECS
{
    /// <summary>
    /// A custom priority queue native container.
    /// </summary>
    /// <typeparam name="T">The type to store in the queue.</typeparam>
    [NativeContainer]
    public unsafe struct NativePriorityQueue<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// The number of elements in the NativePriorityQueue.
        /// </summary>
        public int Length { get { CheckRead( ); return LengthUnsafe; } }
        public int Capacity { get { CheckRead( ); return CapacityUnsafe; } }
        private int CapacityUnsafe => m_Buffer->Capacity;
        private int LengthUnsafe => m_Buffer->Length - 1;
        [StructLayout( LayoutKind.Sequential )]
        internal struct Item
        {
            public T data;
            public float priority;
            public int index;
        }

        [NativeDisableUnsafePtrRestriction] private UnsafeList<Item>* m_Buffer;

        private Item PreviousUnsafe => ReadUnsafe( m_Buffer->Length - 1 );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        [NativeSetClassTypeToNullOnSchedule] internal DisposeSentinel m_DisposeSentinel;
#endif
        private Allocator m_AllocatorLabel;
        /// <summary>
        /// Constructor for the NativePriorityQueue.
        /// </summary>
        /// <param name="capacity">Initial capacity of the NativePriorityQueue.</param>
        /// <param name="allocator">Memory Allocator to use.</param>
        public NativePriorityQueue ( int capacity, Allocator allocator )
        {
            long bytesize = UnsafeUtility.SizeOf<Item>( ) * ( long )capacity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ( allocator <= Allocator.None )
                throw new ArgumentException( "Allocator must be Temp, TempJob, or Persistent", nameof( allocator ) );
            if ( capacity < 0 )
                throw new ArgumentOutOfRangeException( nameof( capacity ) + " must be >= 0" );
            if ( !UnsafeUtility.IsBlittable<T>( ) )
                throw new ArgumentException( typeof( T ) + " used in " + nameof( NativePriorityQueue<T> ) + " must be blittable" );
            if ( bytesize > int.MaxValue )
                throw new ArgumentOutOfRangeException( nameof( capacity ) + " * sizeof(T) cannot exceed " + int.MaxValue + " bytes" );

            DisposeSentinel.Create( out m_Safety, out m_DisposeSentinel, 0, allocator );
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite( m_Safety, true );
#endif
            m_AllocatorLabel = allocator;
            m_Buffer = UnsafeList<Item>.Create( capacity, allocator );
            m_Buffer->Add( default );
        }

        /// <summary>
        /// Set the capacity of the NativePriorityQueue.
        /// </summary>
        /// <param name="capacity">The desired capacity.</param>
        public void SetCapacity ( int capacity )
        {
            CheckWrite( );
            m_Buffer->SetCapacity( capacity );
        }

        /// <summary>
        /// Pushes a new element onto the queue, with a given priority.
        /// </summary>
        /// <param name="value">The value of the new element to push.</param>
        /// <param name="priority">The priority associated with the pushed element.</param>
        public void Push ( T value, float priority )
        {
            Item item = new Item( ) { data = value };
            this.Push( item, priority );
        }

        /// <summary>
        /// Reads the priority of the top priority element in the queue (the one with the lowest priority value), without popping it.
        /// </summary>
        /// <returns>The priority of the top priority element in the queue (the one with the lowest priority value).</returns>
        public float TopPriority ( )
        {
            CheckRead( );
            if ( Length > 0 )
            {
                return ReadUnsafe( 1 ).priority;
            }
            else throw new Exception( "Priority Queue is empty and cannot have a top priority" );
        }

        /// <summary>
        /// Reads the value of the top priority element in the queue (the one with the lowest priority value), without popping it.
        /// </summary>
        /// <returns>The value of the top priority element in the queue (the one with the lowest priority value).</returns>
        public T Peek ( )
        {
            CheckRead( );
            if ( Length > 0 )
            {
                return ReadUnsafe( 1 ).data;
            }
            else throw new Exception( "Priority Queue is empty and cannot be peeked" );
        }


        /// <summary>
        /// Pops the top priority element from the queue (the one with the lowest priority value).
        /// </summary>
        /// <returns>The value of the top priority element in the queue (the one with the lowest priority value).</returns>
        public T Pop ( )
        {
            return this.PopItem( ).data;
        }

        /// <summary>
        /// Serializes this NativePriorityQueue to a byte array.
        /// </summary>
        /// <returns>The serialized byte array.</returns>
        public byte[] SerializeToBytes ( )
        {
            byte[] bytes = new byte[this.Length * sizeof( Item )];

            NativeArray<Item> items = new NativeArray<Item>( this.Length, Allocator.Temp );
            int n = 0;
            while ( this.Length > 0 )
            {
                Item entry = this.PopItem( );
                items[n] = entry;
                n++;
            }

            for ( int i = 0; i < items.Length; i++ )
            {
                this.Push( items[i].data, items[i].priority );
            }

            items.Reinterpret<byte>( sizeof( Item ) ).CopyTo( bytes );

            items.Dispose( );
            return bytes;
        }

        /// <summary>
        /// Deserializes a previously serialized NativePriorityQueue into this one. 
        /// </summary>
        /// <param name="bytes">The previously serialized NativePriorityQueue as a byte array.</param>
        public unsafe void DeserializeFromBytes ( byte[] bytes )
        {
            NativeArray<Item> items = new NativeArray<byte>( bytes, Allocator.Temp ).Reinterpret<Item>( sizeof( byte ) );
            while ( this.Length > 0 ) this.PopItem( ); //clear

            foreach ( Item item in items )
            {
                this.Push( item.data, item.priority );
            }

            items.Dispose( );
        }

        private Item PopItem ( )
        {
            CheckRead( );
            CheckWrite( );

            Item item = ReadUnsafe( 1 );

            if ( LengthUnsafe == 1 )
            {
                m_Buffer->RemoveAtSwapBack( 1 );
                return item;
            }

            Item previous = RemoveAtSwapBackUnsafe( 1 );

            BubbleDownUnsafe( previous );

            return item;
        }
        private Item ReadUnsafe ( int index ) => UnsafeUtility.ReadArrayElement<Item>( m_Buffer->Ptr, index );
        private void WriteUnsafe ( int index, Item data ) => UnsafeUtility.WriteArrayElement( m_Buffer->Ptr, index, data );
        private void Push ( Item item, float priority )
        {
            CheckWrite( );
            CheckRead( );

            item.priority = priority;
            item.index = LengthUnsafe + 1;

            // Add the node to the end of the list
            m_Buffer->Add( item );

            BubbleUpUnsafe( item );
        }

        private void CheckRead ( )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow( m_Safety );
#endif
        }

        private void CheckWrite ( )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow( m_Safety );
#endif
        }
        private void BubbleUpUnsafe ( Item item )
        {
            int parent;
            if ( item.index > 1 )
            {
                parent = item.index >> 1;
                Item parentNode = ReadUnsafe( parent );

                if ( HigherOrSamePriorityThan( parentNode, item ) )
                    return;

                WriteUnsafe( item.index, parentNode );
                parentNode.index = item.index;
                item.index = parent;
            }
            else
            {
                return;
            }
            while ( parent > 1 )
            {
                parent >>= 1;
                Item parentNode = ReadUnsafe( parent );
                if ( HigherOrSamePriorityThan( parentNode, item ) )
                    break;

                WriteUnsafe( item.index, parentNode );
                parentNode.index = item.index;
                item.index = parent;
            }
            WriteUnsafe( item.index, item );
        }

        private void BubbleDownUnsafe ( Item item )
        {
            int finalQueueIndex = item.index;
            int childLeftIndex = 2 * finalQueueIndex;

            if ( childLeftIndex > LengthUnsafe )
                return;

            int childRightIndex = childLeftIndex + 1;
            Item childLeft = ReadUnsafe( childLeftIndex );

            if ( HigherPriorityThan( childLeft, item ) )
            {
                if ( childRightIndex > LengthUnsafe )
                {
                    item.index = childLeftIndex;
                    childLeft.index = finalQueueIndex;
                    WriteUnsafe( finalQueueIndex, childLeft );
                    WriteUnsafe( childLeftIndex, item );
                    return;
                }
                Item childRight = ReadUnsafe( childRightIndex );
                if ( HigherPriorityThan( childLeft, childRight ) )
                {
                    childLeft.index = finalQueueIndex;
                    WriteUnsafe( finalQueueIndex, childLeft );
                    finalQueueIndex = childLeftIndex;
                }
                else
                {
                    childRight.index = finalQueueIndex;
                    WriteUnsafe( finalQueueIndex, childRight );
                    finalQueueIndex = childRightIndex;
                }
            }
            else if ( childRightIndex > LengthUnsafe )
            {
                return;
            }
            else
            {
                Item childRight = ReadUnsafe( childRightIndex );
                if ( HigherPriorityThan( childRight, item ) )
                {
                    childRight.index = finalQueueIndex;
                    WriteUnsafe( finalQueueIndex, childRight );
                    finalQueueIndex = childRightIndex;
                }
                else
                {
                    return;
                }
            }


            while ( true )
            {
                childLeftIndex = 2 * finalQueueIndex;

                if ( childLeftIndex > LengthUnsafe )
                {
                    item.index = finalQueueIndex;
                    WriteUnsafe( finalQueueIndex, item );
                    break;
                }

                childRightIndex = childLeftIndex + 1;
                childLeft = ReadUnsafe( childLeftIndex );
                if ( HigherPriorityThan( childLeft, item ) )
                {
                    if ( childRightIndex > LengthUnsafe )
                    {
                        item.index = childLeftIndex;
                        childLeft.index = finalQueueIndex;
                        WriteUnsafe( finalQueueIndex, childLeft );
                        WriteUnsafe( childLeftIndex, item );
                        break;
                    }
                    Item childRight = ReadUnsafe( childRightIndex );
                    if ( HigherPriorityThan( childLeft, childRight ) )
                    {
                        childLeft.index = finalQueueIndex;
                        WriteUnsafe( finalQueueIndex, childLeft );
                        finalQueueIndex = childLeftIndex;
                    }
                    else
                    {
                        childRight.index = finalQueueIndex;
                        WriteUnsafe( finalQueueIndex, childRight );
                        finalQueueIndex = childRightIndex;
                    }
                }
                else if ( childRightIndex > LengthUnsafe )
                {
                    item.index = finalQueueIndex;
                    WriteUnsafe( finalQueueIndex, item );
                    break;
                }
                else
                {
                    Item childRight = ReadUnsafe( childRightIndex );
                    if ( HigherPriorityThan( childRight, item ) )
                    {
                        childRight.index = finalQueueIndex;
                        WriteUnsafe( finalQueueIndex, childRight );
                        finalQueueIndex = childRightIndex;
                    }
                    else
                    {
                        item.index = finalQueueIndex;
                        WriteUnsafe( finalQueueIndex, item );
                        break;
                    }
                }
            }
        }

        private bool HigherPriorityThan ( Item higher, Item lower ) => higher.priority < lower.priority;
        private bool HigherOrSamePriorityThan ( Item higher, Item lower ) => higher.priority <= lower.priority;

        Item RemoveAtSwapBackUnsafe ( int index )
        {
            var previous = PreviousUnsafe;
            previous.index = index;
            WriteUnsafe( index, previous );
            m_Buffer->RemoveAtSwapBack( LengthUnsafe );
            return previous;
        }


        public void Dispose ( )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose( ref m_Safety, ref m_DisposeSentinel );
#endif
            UnsafeList<Item>.Destroy( m_Buffer );
            m_Buffer = null;
        }

        public JobHandle Dispose ( JobHandle inputDeps )
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Clear( ref m_DisposeSentinel );

            JobHandle disposeJob = new NativePriorityQueueDisposeJob
            {
                Data = new NativePriorityQueueDispose
                {
                    m_ListData = m_Buffer,
                    m_Safety = m_Safety
                }
            }.Schedule( inputDeps );

            AtomicSafetyHandle.Release( m_Safety );
#else
			JobHandle disposeJob = new NativePriorityQueueDisposeJob
			{
				Data = new NativePriorityQueueDispose
				{
					m_ListData = m_Buffer
				}
			}.Schedule(inputDeps);
#endif
            m_Buffer = null;

            return disposeJob;
        }

        [NativeContainer]
        internal unsafe struct NativePriorityQueueDispose
        {
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<Item>* m_ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif

            public void Dispose ( )
            {
                UnsafeList<Item>.Destroy( m_ListData );
            }
        }

        [BurstCompile]
        internal unsafe struct NativePriorityQueueDisposeJob : IJob
        {
            internal NativePriorityQueueDispose Data;

            public void Execute ( )
            {
                Data.Dispose( );
            }
        }
    }
}