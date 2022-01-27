using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace VodeoECS
{
    /// <summary>
    /// Event Listeners are used to listen to Events of a given type.
    /// </summary>
    /// <typeparam name="T">The type of Events this Listener listens to.</typeparam>
    public struct EventListener<T> : IEnumerable<T> where T : unmanaged, IEventECS
    {
        private NativeList<T> eventList;

        /// <summary>
        /// The number of Events of type T currently buffered.
        /// </summary>
        public int Count { get { return eventList.Length; } }
        /// <summary>
        /// Constructor. For internal use by Event Pool.
        /// </summary>
        /// <param name="eventList"></param>
        public EventListener ( NativeList<T> eventList )
        {
            this.eventList = eventList;
        }

        /// <summary>
        /// Enumerates through access to all Events of type T.
        /// </summary>
        /// <returns>The value of each Event.</returns>
        public IEnumerator<T> GetEnumerator ( )
        {
            foreach ( T e in this.eventList )
            {
                yield return e;
            }
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return GetEnumerator( );
        }
    }
}