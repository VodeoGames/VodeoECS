namespace VodeoECS
{
    /// <summary>
    /// Schedule Queues are requested from the Schedule Queue manager and track the scheduled updates of Entities by Systems.
    /// </summary>
    public struct ScheduleQueue
    {
        /// <summary>
        /// The length of the Schedule Queue.
        /// </summary>
        public int Count { get { return this.queue.Length; } }
        private NativePriorityQueue<Entity> queue;
        /// <summary>
        /// For internal use by the Scheduling System and Schedule Queue Manager.
        /// </summary>
        /// <param name="queue">The NativePriorityQueue to wrap as a ScheduleQueue.</param>
        public ScheduleQueue ( NativePriorityQueue<Entity> queue )
        {
            this.queue = queue;
        }
        /// <summary>
        /// Schedule an Entity update on the Schedule Queue.
        /// </summary>
        /// <param name="entity">The Entity to update.</param>
        /// <param name="deadline">The time at which the update is scheduled.</param>
        public void Schedule ( Entity entity, float deadline )
        {
            queue.Push( entity, deadline );
        }
        /// <summary>
        /// For internal use by the System Manager and the base ScheduledSystem class. Returns the time at which the next update is scheduled.
        /// </summary>
        /// <returns>Returns the time at which the next update is scheduled.</returns>
        public float NextDeadline ( )
        {
            if ( queue.Length > 0 )
            {
                return queue.TopPriority( );
            }
            else
            {
                return float.MaxValue;
            }
        }
    }
}