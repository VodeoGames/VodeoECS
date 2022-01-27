using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace VodeoECS.Internal
{
    /// <summary>
    /// Event Pools manage events of a given Event type. For internal use by the Event Manager.
    /// </summary>
    /// <typeparam name="T">The Event type implementing IEventECS.</typeparam>
    public class EventPool<T> : IEventPool where T : unmanaged, IEventECS
    {
        /// <summary>
        /// The Set of all Systems registered as Emitters for this pool.
        /// </summary>
        public HashSet<SystemECS> Emitters { get; }
        /// <summary>
        /// The Set of all Systems registered as Listeners for this pool.
        /// </summary>
        public HashSet<SystemECS> Listeners { get; }
        /// <summary>
        /// Number of Events buffered.
        /// </summary>
        public int Count { get { return eventQueue.Length; } }

        private NativeList<T> eventQueue;
        private NativeList<T> eventList;

        /// <summary>
        /// For internal use by the Event Pool Manager class. Event pools are normally created by requesting them from the World. 
        /// </summary>
        /// <param name="allocator"></param>
        public EventPool ( bool construct = true )
        {
            this.eventQueue = new NativeList<T>( Allocator.Persistent );
            this.eventList = new NativeList<T>( Allocator.Persistent );
            this.Emitters = new HashSet<SystemECS>( );
            this.Listeners = new HashSet<SystemECS>( );
        }

        /// <summary>
        /// Swap Event buffers, clearing the previous buffered Events and replacing them by the currently queued up Events.
        /// </summary>
        public void SwapBuffers( )
        {
            this.eventList.Clear( );
            this.eventList.CopyFrom( eventQueue.AsArray() );
            this.eventQueue.Clear( );
        }

        /// <summary>
        /// Get an accessor to read currently buffered Events.
        /// </summary>
        /// <returns>An accessor to read currently buffered Events.</returns>
        public EventListener<T> GetListenBuffer( )
        {
            return new EventListener<T>( this.eventList );
        }

        /// <summary>
        /// Get an accessor to wite Events to the queue.
        /// </summary>
        /// <returns>An accessor to wite Events to the queue.</returns>
        public EventEmitter<T> GetEmitQueue ( )
        {
            return new EventEmitter<T>( this.eventQueue );
        }

        /// <summary>
        /// For use by the ECS Serializer. Serializes this Pool.
        /// </summary>
        /// <returns>The serialized data for this pool.</returns>
        public SerializedPoolData SerializeToBytes ( )
        {
            int eventSize = Marshal.SizeOf( default( T ) );
            byte[] eventBytes = new byte[( this.eventQueue.Length ) * eventSize];
            this.eventQueue.AsArray().Reinterpret<byte>( eventSize ).CopyTo( eventBytes );

            return new SerializedPoolData( ) { components = eventBytes };
        }

        /// <summary>
        /// For use by the ECS Serialized. Deserializes pool data into this Pool.
        /// </summary>
        /// <param name="data">The serialized data to deserialize.</param>
        public void DeserializeFromBytes ( SerializedPoolData data )
        {
            eventQueue.Clear( );

            int eventSize = Marshal.SizeOf( default( T ) );

            T[] events = new T[data.components.Length / eventSize];
            NativeArray<byte> eventBytes = new NativeArray<byte>( data.components, Allocator.Temp );
            eventBytes.Reinterpret<T>( sizeof( byte ) ).CopyTo( events );

            for ( int i = 0; i < events.Length; i++ )
            {
                this.eventQueue.Add( events[i] );
            }

            eventBytes.Dispose( );
        }

        /// <summary>
        /// Dispose the native memory reserved by this Pool.
        /// </summary>
        public void Dispose ( )
        {
            this.eventQueue.Dispose( );
            this.eventList.Dispose( );
        }
    }
}