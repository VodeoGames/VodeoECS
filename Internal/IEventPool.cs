using System;
using System.Collections.Generic;


namespace VodeoECS.Internal
{
    /// <summary>
    /// Event Pools manage events. For internal use by the Event Manager.
    /// </summary>
    public interface IEventPool : IDisposable
    {
        /// <summary>
        /// Swap Event buffers, clearing the previous buffered Events and replacing them by the currently queued up Events.
        /// </summary>
        public void SwapBuffers ( );
        /// <summary>
        /// The Set of all Systems registered as Emitters for this pool.
        /// </summary>
        public HashSet<SystemECS> Emitters { get; }
        /// <summary>
        /// The Set of all Systems registered as Listeners for this pool.
        /// </summary>
        public HashSet<SystemECS> Listeners { get; }
    }
}