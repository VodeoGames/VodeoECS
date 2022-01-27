using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace VodeoECS
{
    /// <summary>
    /// Event Emitters are used to create new Events of a given type.
    /// </summary>
    /// <typeparam name="T">The type of Events this Emitter creates.</typeparam>
    public struct EventEmitter<T> where T : unmanaged, IEventECS
    {
        private NativeList<T> eventQueue;

        /// <summary>
        /// Constructor. For internal use by Event pool.
        /// </summary>
        /// <param name="eventQueue"></param>
        public EventEmitter( NativeList<T> eventQueue )
        {
            this.eventQueue = eventQueue;
        }

        /// <summary>
        /// Create a new Event of type T.
        /// </summary>
        /// <param name="theEvent">The Event to create.</param>
        public void CreateEvent ( T theEvent )
        {
            this.eventQueue.Add( theEvent );
        }
    }
}